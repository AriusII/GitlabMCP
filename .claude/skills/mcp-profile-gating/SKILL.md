---
name: mcp-profile-gating
description: >
  The GitlabMCP profile model: the active profile decides which MCP primitives exist at all,
  not which calls get refused.
  USE FOR: granting a tool or prompt to a profile, adding a profile, wiring the gate,
  ProfileCatalog, Grant flags, ConfigureSessionOptions, HttpServerSessionMode, ToolCollection /
  PromptCollection removal, per-profile ServerInstructions, "why is my tool not in tools/list",
  FullPermission vs Maintainer vs Developer vs DevOps, 403 from GitLab vs an ungranted tool,
  golden per-profile tool lists, tools/list snapshot tests.
  DO NOT USE FOR: a tool body, its [Description], its return projection or its
  JsonSerializerContext entry (use mcp-tool-authoring); a prompt or resource body (use
  mcp-prompts-and-resources); wrapping GitLab-authored text (use mcp-untrusted-content); driving
  JSON-RPC by hand (use mcp-server-smoke-test); finding which I*Client has your method (use
  gitlab-client-navigation); IL2xxx/IL3xxx warnings (use dotnet-upgrade:dotnet-aot-compat).
---

# MCP Profile Gating

A profile decides **which MCP primitives the server has**, not which calls it refuses. `Program.cs`
registers every tool class unconditionally; a per-request hook then *removes* everything the active
profile does not grant, before the server answers `tools/list` and before it matches `tools/call`.
This skill is the spec for that mechanism and the procedures for extending it.

The profile architecture does not exist in the repo yet — `Program.cs` is still the template
bootstrap. Every path below is one you create.

## When to Use This Skill

- Adding a `[McpServerTool]` or `[McpServerPrompt]` and deciding which profiles see it.
- Adding a fifth profile, or a read-only variant of an existing one.
- Wiring the gate itself for the first time.
- A tool is missing from `tools/list` and you need to know whether that is the gate, the catalog, or the token.
- Writing or reviewing the gating tests.

## When Not to Use

- The tool exists in the right profile but returns wrong data — that is the tool body, not the gate.
- GitLab answered 403. The gate did its job; see [The token is the real ceiling](#the-token-is-the-real-ceiling).
- You need a *client-side* allowlist. This server has no say in what the client chooses to call.

## Critical Rules

| # | Rule |
|---|---|
| 1 | **Gate by removal, never by filtering the listing.** A tool left in `ToolCollection` is callable even when hidden from `tools/list`. Re-verified on 2.2.0: a `WithRequestFilters(f => f.AddListToolsFilter(...))` that drops `bravo_one` from the listing still let `tools/call bravo_one` run and return `bravo_one result`. |
| 2 | **One catalog.** `Profiles/ProfileCatalog.cs` is the only file that maps a primitive name to profiles. No `if (profile == ...)` inside a `Tools/` class, ever. |
| 3 | **Ungranted is invisible, not refused.** Removal makes `tools/call` answer the protocol error `-32602 Unknown tool: 'x'`. That is the correct outcome — the model never learns the name existed. |
| 4 | **A profile narrows; it can never widen.** The GitLab token's scope and role are the ceiling. |
| 5 | **`ServerInstructions` and `[Description]` are static literals.** Never interpolate a request value or anything GitLab returned — those strings arrive at the client with system-message authority. |
| 6 | **Name every tool explicitly** (`[McpServerTool(Name = "list_issues")]`). The catalog is keyed on the wire name, and the SDK derives a snake_cased name with traps — see `mcp-tool-authoring` for the derivation table. |
| 7 | **Startup fails if a registered primitive has no catalog row.** Silence here means a new tool is dark in all four profiles and nobody notices. |

## Open Decisions — close these before writing tool registration

### D1. Where the profile comes from — and which `SessionMode` the answer depends on

**Per-request gating is possible over plain `WithTools<T>()` registration, and it is possible only in
`HttpServerSessionMode.Stateless`.** Nothing has to be built dynamically and no `ListTools` filter
handler is involved, so `CLAUDE.md`'s "a per-request profile is incompatible with static `WithTools<T>()`
registration — the tool collection has to be built dynamically" is wrong. That correction belongs in
`DECISIONS.md` at the repo root as a dated decision-log entry; this section records the mechanism and its
mode dependency, and does not restate the correction anywhere else.

2.2.0 replaced the single boolean with a three-valued `HttpServerTransportOptions.SessionMode`:
`HttpServerSessionMode.Stateless` (= 0, the default), `Stateful` (= 1), `StatefulForInitializeClients`
(= 2). `Stateless` survives as a documented **convenience proxy** over it, not as a deprecation — assigning
`true` selects `Stateless` and `false` selects `Stateful`; reading returns `true` only for `Stateless`, so
`StatefulForInitializeClients` reads `false`; and "because both properties update the same underlying
value, the last assignment wins when both are configured". All four behaviours confirmed at runtime.
`Program.cs`'s `options.Stateless = true` is therefore exactly
`SessionMode = HttpServerSessionMode.Stateless` and needs no change.

The gate's cadence is a property of that mode, and the SDK documents all three of them on
`ConfigureSessionOptions`: "In stateful mode, this callback is invoked once per session when the client
sends the `initialize` request. In `Stateless` mode, it is invoked on **every HTTP request** because each
request creates a fresh server context. In `StatefulForInitializeClients` mode, both apply."

Measured against 2.2.0 on this machine — one binary, `SessionMode` from an env var, an `X-Profile` header
driving exactly the removal pass `ProfileGate` uses below:

| `SessionMode` | Gate cadence | Per-request profile (option C) |
|---|---|---|
| **`Stateless`** — the default, and what `Stateless = true` selects | Every HTTP request. Seven requests produced **seven distinct `ToolCollection` instances**, each pre-populated from the static registration | **Works.** `X-Profile: alpha` → `["alpha_one"]`, `X-Profile: bravo` → `["bravo_one"]`, and a *following* header-less request still saw **both** — a removal never leaks forward. `tools/call bravo_one` under `alpha` → `-32602 Unknown tool`. `PromptCollection` behaved identically |
| `Stateful` — what `Stateless = false` selects | Once per session, at `initialize`. Two invocations for five HTTP requests | **Broken.** A session opened with `X-Profile: alpha` still answered `["alpha_one"]` to a `tools/list` carrying `X-Profile: bravo`, and `tools/call bravo_one` on it returned `-32602`. Per-*session* gating still works — a second session opened with `bravo` got `["bravo_one"]` |
| `StatefulForInitializeClients` | **Both, on one endpoint** — once per session for `initialize` clients, once per request for `2026-07-28` and later | **Never combine with option C.** Same process, seconds apart: `X-Profile: bravo` returned `["alpha_one"]` on the session path and `["bravo_one"]` on the `2026-07-28` path. Two callers presenting the same header get different surfaces |

Options A and B are indifferent to the mode. **Option C is a correctness claim about
`SessionMode.Stateless` specifically** — and `Stateless == false` no longer identifies the mode that breaks
it, because it is `false` for *both* other modes.

| Source | How | Buy | Cost |
|---|---|---|---|
| **A. Config / env at startup** (`GitLabMcp__Profile=DevOps`) | Resolve once in `Program.cs`, close over the value | One profile per container, so the container's GitLab token can be scoped to match it — the only option that gives per-profile least privilege | Changing profile means a redeploy |
| **B. Config with `IOptionsMonitor`** | Re-read per request | Flip profile without restart | A connected client's cached `tools/list` goes stale; in stateless mode there is no `notifications/tools/list_changed` to tell it, so a tool silently becomes `-32602` mid-conversation |
| **C. Per-request header** (`X-GitLab-Profile`) | `http.Request.Headers[...]` inside `ConfigureSessionOptions` | One deployment serves several personas | The profile becomes **caller-controlled**. It is a context-budget convenience, not a security boundary, unless the header is bound to an authenticated principal (`http.User`). And one process now needs a token that is the union of all profiles — the least-privilege story from A is gone |

**Default to A.** Take C only behind authentication *and* only in `Stateless` mode, and record in
`DECISIONS.md` that the token is then necessarily the union of every profile that deployment serves.

**Trap when you write the callback.** `ConfigureSessionOptions` is
`Func<HttpContext, McpServerOptions, CancellationToken, Task>` — a `Task`, not a `ValueTask`. Writing
`return default;` hands the SDK a **null `Task`**: the gate itself runs correctly and the request then
dies as a bodiless `HTTP 500` (`Content-Length: 0`, no JSON-RPC error object) with
`System.NullReferenceException ... at StreamableHttpHandler.CreateSessionAsync` in the server log — which
reads like an SDK bug. Under this repo's `<Nullable>enable</Nullable>` the compiler flags it as
`warning CS8603`, so the warning-free-build rule is what catches it. Return `Task.CompletedTask`, or make
the callback `async` as `ProfileGate.ApplyTo` does.

### D2. Is `FullPermission` a distinct registration or a union?

| Option | Consequence |
|---|---|
| **Union (recommended)** | `Grant.Full = Maintainer \| Developer \| DevOps \| AdminOnly`. One extra bit, `AdminOnly`, carries the instance-administration tools that belong to no persona. Nothing is duplicated and the four surfaces cannot drift apart. |
| Distinct set | A fourth hand-maintained list. It will diverge from the other three within a few commits, and the divergence is invisible until someone diffs two `tools/list` outputs. |

Accept the consequence of the union: `FullPermission` advertises the entire surface. With 143 resource
clients behind it that is a large `tools/list` and a real context cost — and a large tool list is widely
reported to degrade tool selection, though nothing in this repo has measured that. **Treat `FullPermission` as an administrative and debugging profile, not the
default.**

### D3. How is a read-only variant expressed?

| Option | Consequence |
|---|---|
| Eight enum members (`MaintainerReadOnly`, …) | Doubles the enum and every `switch`; the read/write axis is orthogonal to persona, so encoding it in the same enum is a category error |
| Two bits per persona in `Grant` | Doubles the catalog's vocabulary; every row must now be classified twice |
| **Orthogonal modifier (recommended)** | A separate `bool readOnly` alongside the profile. After the profile pass, remove any tool whose `ProtocolTool.Annotations?.ReadOnlyHint is not true`. Verified working: with two registered tools, the read-only pass left only the one declaring `ReadOnly = true` |

The recommended option leans on the annotation, and the MCP spec is explicit that annotations are
hints clients must not trust from *untrusted* servers. That caveat is about consuming someone else's
annotations; here we author them, so they are authoritative for our own surface — **provided a startup
assertion keeps them honest**. Add one: every tool the catalog marks as a writer must have
`ReadOnlyHint != true`, and vice versa. Without that assertion the read-only flag is decorative.

Also note the SDK's asymmetric defaults: `ReadOnly` defaults to `false` and `Destructive` to `true`.
An unset hint is omitted from the wire entirely, so a read tool that forgets `ReadOnly = true` is
indistinguishable from a destructive one — and invisible to a read-only profile.

## The C# shape

Four files under `GitlabMCP/Profiles/`, plus the `Program.cs` wiring. The sketch below was built,
AOT-published and then driven over HTTP as a single-file binary, against
`ModelContextProtocol.AspNetCore` 2.2.0 (the version in this repo's csproj) with this repo's AOT
properties: `dotnet build` gives 0 warnings and `dotnet publish -c Release -r win-x64` gives
0 IL2xxx/IL3xxx warnings.

### `Profiles/McpProfile.cs` — the axes

```csharp
namespace GitlabMCP.Profiles;

internal enum McpProfile { Maintainer, Developer, DevOps, FullPermission }

/// <summary>Which profiles a primitive is granted to. One bit per surface.</summary>
[Flags]
internal enum Grant
{
    None       = 0,
    Maintainer = 1 << 0,
    Developer  = 1 << 1,
    DevOps     = 1 << 2,

    /// <summary>Instance administration: reachable only from FullPermission.</summary>
    AdminOnly  = 1 << 3,

    Planning   = Maintainer | Developer,
    Delivery   = Developer | DevOps,
    Everyone   = Maintainer | Developer | DevOps,
    Full       = Everyone | AdminOnly,
}
```

### `Profiles/ProfileCatalog.cs` — the single source of truth

```csharp
using System.Collections.Frozen;

namespace GitlabMCP.Profiles;

internal static class ProfileCatalog
{
    private static readonly FrozenDictionary<string, Grant> ToolGrants =
        new Dictionary<string, Grant>(StringComparer.Ordinal)
        {
            ["list_issues"]           = Grant.Everyone,
            ["create_issue"]          = Grant.Planning,
            ["list_merge_requests"]   = Grant.Delivery,
            ["merge_merge_request"]   = Grant.Developer,
            ["retry_pipeline"]        = Grant.DevOps,
            ["get_instance_settings"] = Grant.AdminOnly,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, Grant> PromptGrants =
        new Dictionary<string, Grant>(StringComparer.Ordinal)
        {
            ["triage_issue_backlog"]     = Grant.Maintainer,
            ["review_merge_request"]     = Grant.Developer,
            ["diagnose_failed_pipeline"] = Grant.DevOps,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static Grant MaskFor(McpProfile profile) => profile switch
    {
        McpProfile.Maintainer     => Grant.Maintainer,
        McpProfile.Developer      => Grant.Developer,
        McpProfile.DevOps         => Grant.DevOps,
        McpProfile.FullPermission => Grant.Full,
        _ => Grant.None,
    };

    internal static bool GrantsTool(string name, McpProfile p) =>
        ToolGrants.TryGetValue(name, out var g) && (g & MaskFor(p)) != Grant.None;

    internal static bool GrantsPrompt(string name, McpProfile p) =>
        PromptGrants.TryGetValue(name, out var g) && (g & MaskFor(p)) != Grant.None;

    internal static bool KnowsTool(string name) => ToolGrants.ContainsKey(name);
    internal static bool KnowsPrompt(string name) => PromptGrants.ContainsKey(name);

    internal static IEnumerable<string> AllToolNames => ToolGrants.Keys;
    internal static IEnumerable<string> AllPromptNames => PromptGrants.Keys;

    /// <summary>Expected tools/list names for a profile. The snapshot test asserts against this.</summary>
    internal static IEnumerable<string> ToolsFor(McpProfile p) =>
        ToolGrants.Where(kv => (kv.Value & MaskFor(p)) != Grant.None)
                  .Select(kv => kv.Key)
                  .Order(StringComparer.Ordinal);
}
```

`FrozenDictionary` is deliberate: the lookup runs on every request for every primitive, and the table
never changes after startup.

### `Profiles/ProfileInstructions.cs` — the per-profile system message

This is where Critical Rule 5 is enforced structurally: one `const` per profile, nothing
interpolated, no way for a request value or a GitLab string to reach it.

```csharp
namespace GitlabMCP.Profiles;

internal static class ProfileInstructions
{
    private const string Maintainer =
        "This server exposes issue, milestone, label, board, wiki and membership management for a "
        + "GitLab project. Use it to triage and organise work, not to change code or run CI. "
        + "Merge requests and pipelines are readable only; branches, tags and deployments are absent.";

    private const string Developer =
        "This server exposes merge requests, reviews, branches, commits, files and pipeline results "
        + "for a GitLab project. Use it to move code through review and to see why a pipeline failed. "
        + "Runner, environment and instance administration are absent.";

    private const string DevOps =
        "This server exposes CI/CD, runners, environments, deployments, variables and registries for a "
        + "GitLab project. Use it to operate pipelines and infrastructure. Issues and merge requests "
        + "are readable for context only; nothing here creates or reviews code changes.";

    private const string FullPermission =
        "This server exposes the entire GitLab surface, including instance administration. It is an "
        + "administrative and debugging profile: prefer a narrower one for routine work. Every write "
        + "here is still bounded by the configured token's role and scope.";

    internal static string For(McpProfile profile) => profile switch
    {
        McpProfile.Maintainer     => Maintainer,
        McpProfile.Developer      => Developer,
        McpProfile.DevOps         => DevOps,
        McpProfile.FullPermission => FullPermission,
        _ => throw new InvalidOperationException(
            $"McpProfile.{profile} has no arm in ProfileInstructions.For."),
    };
}
```

The wording is illustrative — say what the surface is *for* and what it deliberately cannot do, and
keep it out of per-tool territory. Unlike `MaskFor`, this `switch` throws on an unmapped profile:
a missing instruction is a bug, and there is no safe-by-default string to fall back to.

### `Profiles/ProfileGate.cs` — the removal pass and the startup assertions

```csharp
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace GitlabMCP.Profiles;

internal static class ProfileGate
{
    /// <summary>Chains the gate onto any existing ConfigureSessionOptions; it never replaces it.</summary>
    internal static void ApplyTo(HttpServerTransportOptions transport, Func<HttpContext, McpProfile> resolve)
    {
        var previous = transport.ConfigureSessionOptions;

        transport.ConfigureSessionOptions = async (http, options, cancellationToken) =>
        {
            if (previous is not null)
            {
                await previous(http, options, cancellationToken);
            }

            var profile = resolve(http);

            if (options.ToolCollection is { } tools)
            {
                foreach (var tool in tools.ToArray())
                {
                    if (!ProfileCatalog.GrantsTool(tool.ProtocolTool.Name, profile))
                    {
                        tools.Remove(tool);
                    }
                }
            }

            if (options.PromptCollection is { } prompts)
            {
                foreach (var prompt in prompts.ToArray())
                {
                    if (!ProfileCatalog.GrantsPrompt(prompt.ProtocolPrompt.Name, profile))
                    {
                        prompts.Remove(prompt);
                    }
                }
            }

            options.ServerInstructions = ProfileInstructions.For(profile);
        };
    }

    /// <summary>Throws at startup on a dark profile, an uncatalogued primitive, or a dead catalog row.</summary>
    internal static void AssertCatalogIsComplete(IServiceProvider services)
    {
        foreach (var p in Enum.GetValues<McpProfile>())
        {
            if (ProfileCatalog.MaskFor(p) == Grant.None)
            {
                throw new InvalidOperationException(
                    $"McpProfile.{p} has no arm in ProfileCatalog.MaskFor - it would serve zero tools.");
            }
        }

        var registeredTools = services.GetServices<McpServerTool>()
                                      .Select(t => t.ProtocolTool.Name)
                                      .ToHashSet(StringComparer.Ordinal);
        var registeredPrompts = services.GetServices<McpServerPrompt>()
                                        .Select(p => p.ProtocolPrompt.Name)
                                        .ToHashSet(StringComparer.Ordinal);

        string[] orphanTools =
            [.. registeredTools.Where(n => !ProfileCatalog.KnowsTool(n)).Order(StringComparer.Ordinal)];
        string[] orphanPrompts =
            [.. registeredPrompts.Where(n => !ProfileCatalog.KnowsPrompt(n)).Order(StringComparer.Ordinal)];

        if (orphanTools.Length > 0 || orphanPrompts.Length > 0)
        {
            throw new InvalidOperationException(
                $"ProfileCatalog has no row for tools [{string.Join(", ", orphanTools)}] "
                + $"or prompts [{string.Join(", ", orphanPrompts)}]. "
                + "Add a Grant in Profiles/ProfileCatalog.cs - an ungranted primitive is invisible in every profile.");
        }

        string[] deadTools =
            [.. ProfileCatalog.AllToolNames.Where(n => !registeredTools.Contains(n)).Order(StringComparer.Ordinal)];
        string[] deadPrompts =
            [.. ProfileCatalog.AllPromptNames.Where(n => !registeredPrompts.Contains(n)).Order(StringComparer.Ordinal)];

        if (deadTools.Length > 0 || deadPrompts.Length > 0)
        {
            throw new InvalidOperationException(
                $"ProfileCatalog rows are dead - tools [{string.Join(", ", deadTools)}], "
                + $"prompts [{string.Join(", ", deadPrompts)}] are catalogued but never registered. "
                + "A dead row puts a name in a golden list that no client can ever see.");
        }
    }
}
```

`ToArray()` before the loop is required — you are mutating the collection you are walking.
`McpServerPrimitiveCollection<T>` is `ICollection<T>` and thread-safe, so `ToArray()` / `Remove` are both available.

`ApplyTo` **composes**: it captures whatever `ConfigureSessionOptions` already held and awaits it before
gating, so a later per-session concern (an auth principal, a per-session logger) cannot silently drop the
gate or be dropped by it, whichever registers last. Verified: with a prior callback setting `ServerInfo`,
`initialize` returned both that `serverInfo` and the gated tool set.

The three startup assertions close the three ways the catalog and the registration can disagree:

| Assertion | Failure it prevents |
|---|---|
| Every `McpProfile` has a non-`None` mask | Add-a-profile step 1 done, step 4 forgotten: the server boots, answers `initialize`, and serves an empty `tools/list` |
| Every registered primitive has a catalog row | A new tool is dark in all four profiles and nobody notices |
| Every catalog row names a registered primitive | A dead row puts a name in L1's golden list that no client can ever see, so L1 asserts a surface that cannot exist |

### `Program.cs` wiring

```csharp
var builder = WebApplication.CreateBuilder(args);

var configured = builder.Configuration["GitLabMcp:Profile"];
var startupProfile = string.IsNullOrWhiteSpace(configured)
    ? McpProfile.Maintainer                      // absent key: narrowest surface is the safe default
    : Enum.TryParse<McpProfile>(configured, ignoreCase: true, out var parsed)
        ? parsed
        : throw new InvalidOperationException(   // present but wrong: never guess the surface
            $"GitLabMcp:Profile='{configured}' is not a known McpProfile. "
            + $"Valid values: {string.Join(", ", Enum.GetNames<McpProfile>())}.");

builder.Services
    .AddMcpServer()
    .WithHttpTransport(transport =>
    {
        transport.Stateless = true;                            // == SessionMode = HttpServerSessionMode.Stateless
        ProfileGate.ApplyTo(transport, _ => startupProfile);   // decision D1, option A
    })
    .WithTools<IssueTools>(GitLabJson.Options)
    .WithTools<MergeRequestTools>(GitLabJson.Options)
    .WithPrompts<PlanningPrompts>(GitLabJson.Options)      // triage_issue_backlog, review_merge_request
    .WithPrompts<PipelinePrompts>(GitLabJson.Options);     // diagnose_failed_pipeline

var app = builder.Build();
ProfileGate.AssertCatalogIsComplete(app.Services);
app.MapMcp();
app.Run();
```

`GitLabJson.Options` (`GitlabMCP/Serialization/GitLabJson.cs`) is the chained `JsonSerializerOptions`
required by Native AOT, and the same instance `GitLabContent` serialises through — see
`mcp-tool-authoring` Step 6 for the registration and `mcp-untrusted-content` Step 1 for the source. One
shared instance, never a local: two instances silently disagree about casing.
Never `WithToolsFromAssembly()`; it is `[RequiresUnreferencedCode]` and will emit IL2026.

The gate is only correct while that transport stays in `Stateless` mode (D1). Assigning `SessionMode`
after the boolean would silently re-point it — last assignment wins — so set one or the other, not both.

Only the *absent* key defaults to `Maintainer`. A key that is present and unparseable (`DevOpss`,
`Devops-Full`, a member of a future enum you have not added yet) throws, because every other
mis-wiring in this file fails at startup and the one input that selects the entire surface must not be
the exception — a silent fallback here is discovered days later as "why is that tool missing".

Verified behaviour of exactly the wiring above, driven over HTTP against the AOT-published binary
with `GitLabMcp__Profile=DevOps`:

```text
tools/list    -> ["list_issues", "list_merge_requests", "retry_pipeline"]
prompts/list  -> ["diagnose_failed_pipeline"]
tools/call    -> {"error":{"code":-32602,"message":"Unknown tool: 'create_issue'"}}
prompts/get   -> {"error":{"code":-32602,"message":"Unknown prompt: 'triage_issue_backlog'"}}
initialize    -> "instructions":"This server exposes CI/CD, runners, environments, ..."
```

Both `-32602` lines name a primitive that *is* registered and *is* in the catalog, and is absent only
because the DevOps mask excludes it — which is the point. An unregistered name produces the identical
error, so a probe against a name the server never had proves nothing about the gate.

## Procedure: add a tool to profiles

1. Write the tool per `mcp-tool-authoring`, with an explicit `Name` and a correct `ReadOnly` hint.
2. Register its class with `WithTools<T>(GitLabJson.Options)` in `Program.cs` if the class is new.
3. Add exactly one row to `ProfileCatalog.ToolGrants`, keyed on the wire name. Reach for a named union
   (`Grant.Planning`, `Grant.Delivery`) before an ad-hoc `A | B` — the unions are how intent stays legible.
4. Update the golden list for every profile whose snapshot changes (see [Tests](#tests-that-must-exist)).
5. `dotnet build GitlabMCP.slnx`, then run the server and **`tools/call` the new tool**, not just
   `tools/list`. Startup assertions catch a missing catalog row, but a missing `[JsonSerializable]` entry
   for a `GitLabContent.Wrap<T>` payload does not surface until the first call — a clean `tools/list` is no
   longer evidence (`mcp-tool-authoring` §Verification).

Deciding the grant, in order:
- Does the persona *do* this in GitLab day to day? If no, stop — do not grant "just in case".
- Does it write? A writer belongs to at most the personas that own the object (`create_issue` is `Planning`, not `Everyone`).
- Is it instance-scoped administration? Then `Grant.AdminOnly`, regardless of who might want it.
- Every additional grant widens the blast radius of a successful prompt injection through that profile. See `mcp-untrusted-content`.

## Procedure: add a whole new profile

1. Add the member to `McpProfile`.
2. Add a bit to `Grant` — **the next free power of two**, never a renumber. Renumbering silently re-points every existing row.
3. Extend the named unions if the new profile shares an existing bundle.
4. Add the arm to `ProfileCatalog.MaskFor`. Leave the `_ => Grant.None` fallthrough so an unmapped profile is empty rather than full — `AssertCatalogIsComplete` sweeps every enum member and throws on a `Grant.None` mask, so forgetting this step fails at startup instead of serving a silently empty surface.
5. Add a `const string` to `ProfileInstructions` and an arm to `ProfileInstructions.For`.
6. Add prompt rows in `PromptGrants` — a profile with no prompts is legal but usually a smell.
7. Decide whether `Grant.Full` should include the new bit. If the profile is a *narrowing* of existing surface, no new bit is needed at all — reuse.
8. Add a golden list and a snapshot test for it.
9. If deployment is D1-option-A, add the container/env entry that selects it, and decide the token that container gets.

## Profile → GitLab.Client resource clients

Every interface named below was checked against `GitLab.Client` 1.0.0 and **exists**. Read the
`CLAUDE.md` mapping as the starting point; this table is that mapping after verification.
Legend: **W** = read and write, **R** = read-only tools, **–** = not in this profile.

| Area | `GitLab.Client.Abstractions` interfaces | Maintainer | Developer | DevOps |
|---|---|:--:|:--:|:--:|
| Work tracking | `IIssuesClient` `IMilestonesClient` `IIterationsClient` `ILabelsClient` `IBoardsClient` | W | W | R |
| Conversation | `INotesClient` `IDiscussionsClient` `IAwardEmojiClient` `IResourceEventsClient` `ITodosClient` | W | W | R |
| Knowledge | `IWikisClient` | W | W | – |
| Discovery | `ISearchClient` `IAnalyticsClient` | R | R | R |
| Code search | `ICodeSearchClient` | – | R | – |
| Membership | `IMembersClient` | R | R | W |
| MR lifecycle | `IMergeRequestsClient` `IMergeRequestApprovalsClient` `IApprovalRulesClient` `IDraftNotesClient` `ISuggestionsClient` | R | W | R |
| SCM | `IBranchesClient` `ICommitsClient` `ITagsClient` `IRepositoriesClient` `IRepositoryFilesClient` `ISnippetsClient` | R | W | R |
| Project / group | `IProjectsClient` `IGroupsClient` | R | W | W |
| CI execution | `IPipelinesClient` `IJobsClient` | – | R | W |
| CI configuration | `IPipelineSchedulesClient` `ITriggersClient` `IVariablesClient` `ICiLintClient` `ICiCatalogClient` `IJobTokenScopeClient` `ISecureFilesClient` | – | – | W |
| Runners | `IRunnersClient` `IRunnerControllersClient` | – | – | W |
| Deploy | `IEnvironmentsClient` `IDeploymentsClient` `IFreezePeriodsClient` | – | R | W |
| Protection | `IProtectedBranchesClient` `IProtectedTagsClient` `IProtectedEnvironmentsClient` | – | R | W |
| Infrastructure | `ITerraformStatesClient` `IClusterAgentsClient` `IFeatureFlagsClient` | – | – | W |
| Registries | `IContainerRegistryClient`, the 12 `IPackages*Client` | – | R | W |
| Integrations / hooks | `IIntegrationsClient` `IProjectHooksClient` `IGroupHooksClient` `ISystemHooksClient` | – | – | W |
| Credentials | `IDeployKeysClient` `IDeployTokensClient` `IServiceAccountsClient` | – | – | W |
| Instance admin | `IInstanceClient` `ILicensesClient` `IAuditEventsClient` `IUsersClient` | – | – | W |

Notes on the verification:

- **`Epics` in the profile table has no package backing.** `CLAUDE.md` lists epics in the Maintainer and
  Developer *Scope* column, but there is no `IEpicsClient` and no `IWorkItemsClient` — no type in the
  assembly has `Epic` or `WorkItem` in its name, so there is no `GitLabEpic`, no `CreateEpicRequest`, no
  way to list, search, create or read an epic. Only epic *sub-resources* exist, on four clients
  (`INotesClient`, `IDiscussionsClient`, `IAwardEmojiClient`, `IResourceEventsClient`), all keyed
  `(GroupId groupId, long epicIid)` — so an epic tool can only reach comments, threads, reactions and
  label/state history of an epic whose `iid` the caller already knows. **Do not put an epic row in the
  catalog until that gap is closed** by a GraphQL path or by dropping epics from the profile definitions.
- `Packages*` in `CLAUDE.md` resolves to exactly 12 interfaces (`Cargo`, `Composer`, `Conan`, `Debian`,
  `Generic`, `Helm`, `Npm`, `NuGet`, `PyPi`, `Rpm`, `RubyGems`, `TerraformModules`). It does **not** cover
  `IDependencyProxyClient` or the three `*ProtectionRules*Client` interfaces — grant those explicitly if wanted.
- Every one of the 143 clients is also a property on the root `IGitLabClient` under the same short name
  (`gitLab.Projects`). Inject the narrow interface anyway: the constructor is then a readable statement of
  which slice of the profile boundary this tool class sits behind.

## The token is the real ceiling

A profile is a **surface**, not a permission. GitLab's own authorization runs afterwards and wins.

- A `DevOps` profile on a `read_api` project token advertises `retry_pipeline` and gets 403 the moment
  the model uses it. That is correct behaviour of both layers and must not read as a bug.
- GitLab has no per-endpoint scope: the practical choice is `read_api` or `api`. **Scope cannot express
  a profile.** The lever that can is token *type* — a project access token is bounded to one project, a
  group token to one group, a PAT to everything its owner can reach. Prefer project < group < personal.
- Under D1-option-A each profile gets its own container and therefore its own token, and the two
  narrowings compose. That is the whole security argument for option A.

So the model must be told *which* layer refused it. That message contract has **two owners, and they are
different files**: the `Describe(GitLabApiException)` mapper, the one cross-cutting call-tool filter and the
stable `gitlab_*` codes are `mcp-tool-authoring` §Step 7; **what those strings may and may not say —
including the rule that a 404 is worded "not found, or not visible to the configured token" and never as
proof of absence — is `mcp-untrusted-content` §Step 7.** Do not restate it per tool, and do not restate it
here.

## Per-profile ServerInstructions and prompts

`McpServerOptions.ServerInstructions` is documented as being sent during the initialization handshake,
and "client applications typically use these instructions as system messages". So:

- Set it inside `ConfigureSessionOptions`, from `ProfileInstructions.For`, so each client is told how to
  use *that* surface rather than the union of four.
- Every value is a `const string` — that is the whole reason `ProfileInstructions` is a separate file
  with no parameters in scope. Interpolating a request value or GitLab-returned text here hands an
  attacker the system prompt. See `mcp-untrusted-content`.
- The SDK's own guidance: instructions "should not duplicate tool, prompt, or resource descriptions
  already exposed elsewhere". Say what the surface is *for* and what it deliberately cannot do —
  the per-tool contract belongs in `[Description]`.
- Prompts gate through `options.PromptCollection` in the same pass and behave identically — verified on
  2.2.0: an ungranted prompt is absent from `prompts/list` and `prompts/get` answers
  `-32602 Unknown prompt`. A profile's prompt set is the strongest signal of intended workflow, so give
  each profile at least one.
- **Prompts stay in scope, and this skill owns only their grant.** The `[McpServerPrompt]` body, its
  arguments, its `[Description]`, and the `WithPrompts<T>(GitLabJson.Options)` registration are
  `mcp-prompts-and-resources`. Here you add the `PromptGrants` row and nothing else — the *Procedure: add a
  tool to profiles* above applies verbatim with `PromptGrants` / `GrantsPrompt` in place of the tool pair.
  Until that skill's prompt classes exist, `PromptGrants` is a catalog for primitives nobody has written:
  keep it, but `AssertCatalogIsComplete`'s dead-row check will (correctly) refuse to start with rows for
  unregistered prompts, so add the row and the prompt in the **same** commit.
- **Resources are not gated yet, and the assertions do not notice.** `McpServerOptions.ResourceCollection`
  exists in 2.2.0 alongside the tool and prompt collections, but `ProfileCatalog` has no `ResourceGrants`
  and `AssertCatalogIsComplete` never sweeps `McpServerResource` — so the first resource added is granted to
  every profile silently. Extend the catalog, the removal pass and the assertion in the same commit as the
  first resource, and decide the catalog key then: a templated resource carries `ProtocolResourceTemplate`
  rather than `ProtocolResource`, so `.Name` is not automatically the right key. Body and registration:
  `mcp-prompts-and-resources`.

## Tests that must exist

There is no test project yet; when one is added, wire it into `GitlabMCP.slnx` explicitly (the
`<Project Path="..."/>` list — nothing is discovered implicitly). Use `dotnet-test:code-testing-agent`
to write them and `dotnet-test:run-tests` to run them. The two things people expect to block an
in-process gating test both turn out not to — measured against this tree and recorded in
`mcp-server-smoke-test` §Where Automated Tests Go: `WebApplicationFactory<Program>` works as-is even
though `Program.cs` is top-level statements (no `public partial class Program`, no `InternalsVisibleTo`),
and `NETSDK1151` does not fire, because `_CalculateIsVSTestTestProject` turns the self-contained check off
whenever `IsTestProject` is `true`. So L2 needs no csproj gymnastics.

**Where the golden lists live — settled here.** `mcp-server-smoke-test` §Step 4 leaves the snapshot
location open between repo-root files and embedded resources inside a future test project. This skill owns
the golden lists, so the decision is made here: **repo-root plain-text files**,
`tests/snapshots/<profile>.tools.txt` and `tests/snapshots/<profile>.prompts.txt`, one name per line,
ordinal-sorted — the order `ProfileCatalog.ToolsFor` already emits, so regenerating one is mechanical.
Reasons: L1, L2, L3 and the transport scripts in `mcp-server-smoke-test` must assert against the *same
bytes*; `tests/` sits outside `GitlabMCP/`, so nothing is swept into the Web SDK's content globs; and the
scripts run today with no test project, whereas an embedded resource forks the data the moment a script
needs it. The test project's only job is to get those files onto its output path. Do not straddle both.

Three layers, and the third is the one that actually proves the invariant:

**L1 — catalog snapshot (no server).** For each of the four profiles, assert
`ProfileCatalog.ToolsFor(profile)` equals the committed golden list. Cheap, and it makes every
widening of the surface show up as a reviewable diff instead of a silent behaviour change. Do the same
for prompts.

**L2 — `tools/list` snapshot (in-process HTTP).** Boot the app per profile, `POST` a `tools/list`
JSON-RPC request, assert the returned `name` set equals L1's golden list. This is what proves the gate is
actually wired to the transport; L1 alone passes even if `ProfileGate.ApplyTo` is never called.
The transport contract is in `mcp-server-smoke-test` (`Accept: application/json, text/event-stream`,
`MCP-Protocol-Version: 2025-11-25`). Then **`tools/call` every tool the profile grants**, once: a clean
`tools/list` no longer proves the serialization context is complete, because a missing `[JsonSerializable]`
entry for a `GitLabContent.Wrap<T>` payload type surfaces only on the first call
(`mcp-tool-authoring` §Verification).

**L3 — the anti-"advertise then refuse" test.** For each profile, for each tool *not* granted to it,
`tools/call` must come back as a **protocol error**, not a tool result:

```json
{"error":{"code":-32602,"message":"Unknown tool: 'retry_pipeline'"}}
```

A `CallToolResult` with `"isError": true`, or any success, is a failure of this test. An implementation
that filters `tools/list` instead of removing from `ToolCollection` passes L1 and L2 and fails only
here — which is exactly why L3 is not optional.

**L4 — invariants.** Assert each of the three startup throws, and assert the message names the offending
primitive or profile: a registered primitive with no catalog row, a catalog row with no registration, and
an `McpProfile` member whose `MaskFor` mask is `Grant.None`. If you took D3's recommended option, also
assert that the catalog's writer classification and each tool's `ReadOnlyHint` agree.

## Checklist

- [ ] The tool has an explicit `Name` and exactly one `ProfileCatalog` row keyed on that name.
- [ ] No `Tools/` file mentions `McpProfile`, `Grant`, or any profile name.
- [ ] Gating removes from `ToolCollection` / `PromptCollection`; no `AddListToolsFilter` is used for gating.
- [ ] `ToArray()` is taken before mutating either collection.
- [ ] `ProfileGate.AssertCatalogIsComplete(app.Services)` runs after `builder.Build()` and before `MapMcp()`, and catalog and registration agree in **both** directions.
- [ ] A present-but-unparseable `GitLabMcp:Profile` throws; only an absent key falls back to `Maintainer`.
- [ ] Every `McpProfile` member has an arm in both `ProfileCatalog.MaskFor` and `ProfileInstructions.For`.
- [ ] `ReadOnly = true` is set on every read tool (the SDK default is `false`).
- [ ] `ServerInstructions` for the profile is a `const string` with nothing interpolated.
- [ ] D1, D2 and D3 are decided and each decision is recorded in `DECISIONS.md`, not just implied by the code.
- [ ] The transport is in `HttpServerSessionMode.Stateless` (`Stateless = true`), and `SessionMode` is not also assigned — D1's per-request cadence holds in that mode only.
- [ ] Every `PromptGrants` row names a prompt that is actually registered, and its body was written per `mcp-prompts-and-resources`.
- [ ] Golden lists live in `tests/snapshots/` and are updated for every profile whose surface changed; L3 covers the newly ungranted names; L2 `tools/call`ed every granted tool.
- [ ] `dotnet build GitlabMCP.slnx` is warning-free and the AOT publish still emits zero IL2xxx/IL3xxx.
