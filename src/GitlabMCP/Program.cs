using System.Net.Http.Headers;
using GitLab.Client.DependencyInjection;
using GitlabMCP.Abstractions;
using GitlabMCP.Contracts.Serialization;
using GitlabMCP.Errors;
using GitlabMCP.GraphQL;
using GitlabMCP.GraphQL.Operations.WorkItems;
using GitlabMCP.GraphQL.Serialization;
using GitlabMCP.Http;
using GitlabMCP.Options;
using GitlabMCP.Profiles;
using GitlabMCP.Tools;
using GitlabMCP.Tools.Resources;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets, Tier 2 (DEC-013) — appended after the default env/cmdline/json chain, so a mounted
// file at /run/secrets wins over a `-e` of the same name. Absolute path required; optional:true only
// covers "the directory doesn't exist", not a relative path.
builder.Configuration.AddKeyPerFile("/run/secrets", true, false);

// --- Silences HttpClient's own Trace-level header logging unconditionally — must win even over an
// environment-set Logging__LogLevel__Default=Trace (mcp-untrusted-content §8).
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// --- Options: bind + validate + fail fast, before app.Run() (DEC-004/DEC-012).
builder.Services
    .AddOptionsWithValidateOnStart<GitLabMcpOptions, ValidateGitLabMcpOptions>()
    .Bind(builder.Configuration.GetSection(GitLabMcpOptions.SectionName));

// --- Resilience (DEC-021): hand-rolled, shared by the REST and GraphQL clients. Registered before
// AddGitLabClient so the handler type is resolvable when AddGitLabClient's own IHttpClientBuilder
// chains onto it below (CLAUDE.md: "Returns an IHttpClientBuilder, so resilience handlers chain onto it").
builder.Services.AddSingleton<GitLabCircuitBreaker>();
builder.Services.AddTransient<GitLabResilienceHandler>();

builder.Services.AddGitLabClient(builder.Configuration) // binds GitLab:* (DEC-012)
    .AddHttpMessageHandler<GitLabResilienceHandler>();
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IValidateOptions<GitLabClientOptions>, ValidateGitLabClientOptions>());
builder.Services.AddOptions<GitLabClientOptions>().ValidateOnStart();
builder.Services.PostConfigure<GitLabClientOptions>(o =>
{
    // Newline trap (DEC-013): `echo` (vs `printf '%s'`) into a mounted secret survives verbatim into
    // the PRIVATE-TOKEN header otherwise. BaseAddress binds as a Uri, which never carries a stray
    // trailing newline from a config value — only the token (a plain string) needs trimming.
    o.AccessToken = o.AccessToken?.Trim() ?? string.Empty;

    // DEC-034: let GitLab:BaseAddress be just the instance's own URL (self-hosted or not) — runs before
    // ValidateGitLabClientOptions, so the common case (a bare self-hosted domain) always passes startup
    // instead of failing on a missing "api/v4/" suffix the operator was never told to add.
    if (o.BaseAddress is { IsAbsoluteUri: true } baseAddress)
        o.BaseAddress = GitLabBaseAddressNormalizer.Normalize(baseAddress);
});

// --- GraphQL client (DEC-020/021): typed client, base address derived from the REST options, same
// token via GitLabAuthHandler, same resilience handler.
builder.Services.AddTransient<GitLabAuthHandler>();
builder.Services.AddHttpClient<IGitLabGraphQlClient, GitLabGraphQlClient>((sp, client) =>
    {
        var gitlab = sp.GetRequiredService<IOptions<GitLabClientOptions>>().Value;
        client.BaseAddress = GitLabGraphQlEndpoint.Resolve(gitlab.BaseAddress);
        client.Timeout = gitlab.Timeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(gitlab.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    })
    .AddHttpMessageHandler<GitLabAuthHandler>()
    .AddHttpMessageHandler<GitLabResilienceHandler>();
builder.Services.AddSingleton<GitLabWorkItemsClient>();

// --- JSON (DEC-009/DEC-026): one shared JsonSerializerOptions instance. GitlabMcpJsonContext.Default
// (Ping + Epics) is already chained by GitLabJson's own static constructor; every OTHER context —
// GraphQL, and each of the 13 tool domains' own context (DEC-026: one JsonSerializerContext class per
// domain, never split across files of the same context) — is inserted here, once, before Seal() locks
// the options instance. Order among these 14 does not matter: each domain's records are disjoint.
GitLabJson.Options.TypeInfoResolverChain.Insert(1, GitLabGraphQlJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, AdminJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, CicdJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, CodeJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, DeployJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, DiscussionJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, InfraJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, LifecycleJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, MergeRequestsJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, PackagesJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, PeopleJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, PlanningJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, ProjectsJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, SearchJsonContext.Default);
GitLabJson.Options.TypeInfoResolverChain.Insert(1, ResourcesJsonContext.Default);
GitLabJson.Seal();
builder.Services.AddSingleton(GitLabJson.Options); // GitLabGraphQlClient takes this as a constructor dependency.

builder.Services.AddHealthChecks(); // liveness only — never GitLab reachability on this route.

builder.Services.AddMcpServer(options =>
    {
        // Placeholder; ProfileGate.Apply overwrites this per-request with the resolved profile's text
        // (DEC-004/mcp-profile-gating). Kept non-empty so an early failure before the gate still has
        // *some* instructions rather than none.
        options.ServerInstructions = "GitlabMCP is starting.";
    })
    .WithHttpTransport(transport =>
    {
        transport.Stateless = true; // DEC-003 — never also assign SessionMode.

        var previousConfigureSessionOptions = transport.ConfigureSessionOptions;
        transport.ConfigureSessionOptions = async (http, options, cancellationToken) =>
        {
            if (previousConfigureSessionOptions is not null)
                await previousConfigureSessionOptions(http, options, cancellationToken);

            // DEC-004=A: resolved once, from configuration — never from `http`. The gate still runs on
            // every request in Stateless mode (each gets a fresh, pre-populated collection).
            var mcpOptions = http.RequestServices.GetRequiredService<IOptions<GitLabMcpOptions>>().Value;
            ProfileGate.Apply(options, mcpOptions.ResolveProfile());
        };
    })
    .WithGitLabErrorMapping()
    .WithTools<PingTools>(GitLabJson.Options)
    .WithTools<EpicTools>(GitLabJson.Options)
    .WithTools<AdminTools>(GitLabJson.Options)
    .WithTools<CicdTools>(GitLabJson.Options)
    .WithTools<CodeTools>(GitLabJson.Options)
    .WithTools<DeployTools>(GitLabJson.Options)
    .WithTools<DiscussionTools>(GitLabJson.Options)
    .WithTools<InfraTools>(GitLabJson.Options)
    .WithTools<LifecycleTools>(GitLabJson.Options)
    .WithTools<MergeRequestsTools>(GitLabJson.Options)
    .WithTools<PackagesTools>(GitLabJson.Options)
    .WithTools<PeopleTools>(GitLabJson.Options)
    .WithTools<PlanningTools>(GitLabJson.Options)
    .WithTools<ProjectsTools>(GitLabJson.Options)
    .WithTools<SearchTools>(GitLabJson.Options)
    .WithPrompts<ProfilePrompts>(GitLabJson.Options)
    .WithPrompts<TaskPrompts>(GitLabJson.Options)
    // DEC-029: resources take no JsonSerializerOptions overload (mcp-prompts-and-resources §5) — every
    // parameter/return type here stays inside the SDK's own context or a type already registered above.
    .WithResources<ServerResources>()
    .WithResources<GitLabEntityResources>();

var app = builder.Build();

// --- DEC-016: fail closed on a wildcard/unset AllowedHosts before accepting any request.
var allowedHosts = app.Configuration["AllowedHosts"];
if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Contains('*'))
    throw new InvalidOperationException(
        "AllowedHosts is unset or wildcard. Set ASPNETCORE_ALLOWEDHOSTS, e.g. '127.0.0.1;localhost;[::1]'.");

// Startup fail-fast: an ungranted primitive, a dead catalog row, a dark profile, or a readOnly
// mismatch throws here, before app.Run() (DEC-006/mcp-profile-gating).
ProfileGate.AssertCatalogIsComplete(app.Services);

// --- Operability (DEC-032): the one line an operator needs from `docker logs` to confirm a container
// picked up the RIGHT profile and the RIGHT GitLab target without ever calling a tool. Never the token.
var startupMcpOptions = app.Services.GetRequiredService<IOptions<GitLabMcpOptions>>().Value;
var startupGitLabOptions = app.Services.GetRequiredService<IOptions<GitLabClientOptions>>().Value;
app.Logger.LogInformation(
    "GitlabMCP {Version} starting: profile={Profile} gitlab={GitLabBaseAddress}",
    startupMcpOptions.ResolveVersion(), startupMcpOptions.ResolveProfile(), startupGitLabOptions.BaseAddress);

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            report.Status == HealthStatus.Healthy ? """{"status":"Healthy"}""" : """{"status":"Unhealthy"}""");
    }
});
app.MapMcp();

app.Run();