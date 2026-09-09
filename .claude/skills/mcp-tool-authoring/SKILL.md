---
name: mcp-tool-authoring
description: >
  Add or change an MCP tool in this GitLab MCP server so it compiles, publishes AOT-clean, and gives the
  model a usable contract. USE FOR: writing a new [McpServerTool] or its [Description] text, setting
  ReadOnly/Destructive/Idempotent/OpenWorld hints, injecting an I<Resource>Client, AddGitLabClient,
  projecting a GitLab DTO into a CallToolResult payload record, bounding an IAsyncEnumerable with a limit,
  WithTools<T> + [JsonSerializable], threading CancellationToken, mapping GitLabApiException, "why does my
  tool crash at startup", "JsonTypeInfo metadata for type ... was not provided". DO NOT USE FOR: which
  I<Resource>Client wraps a GitLab endpoint (use gitlab-client-navigation); which profiles advertise it
  (use mcp-profile-gating); the AOT publish (use aot-publish-gate); the Dockerfile or base images (use
  docker-aot-image); driving JSON-RPC by hand (use mcp-server-smoke-test); the untrusted-data envelope
  (use mcp-untrusted-content); an IL2xxx/IL3xxx warning (use dotnet-upgrade:dotnet-aot-compat).
---

# MCP Tool Authoring

How to add or change a tool in this server. Everything here is verified against `ModelContextProtocol` 2.2.0
and `GitLab.Client` 1.0.0 by compiling and running the code, not from the XML docs alone — where the two
disagree, this file says so.

The 2.1.0 → 2.2.0 bump changed nothing on this page. `ModelContextProtocol.Core.xml` and
`ModelContextProtocol.xml` are **byte-identical** between the two versions (md5 `e8cc4416…` and `e6cc4df6…`);
the entire delta is six added members on `ModelContextProtocol.AspNetCore`, all transport configuration.
Every measurement below was nonetheless re-run against 2.2.0.

Read `CLAUDE.md` for the project's standing rules; this skill is the procedure that implements them.

## When to Use This Skill

- Adding a method to an existing `Tools/` class, or adding a new tool class.
- Changing a tool's name, description, parameters, return shape or behavioural hints.
- Diagnosing a tool that builds cleanly but crashes the server at startup, or fails only on `tools/call`.
- Reviewing a tool someone else wrote.

## When Not to Use

- You do not yet know *which* GitLab client call to make — `gitlab-client-navigation` finds it first.
- The tool exists and the question is which profiles advertise it — `mcp-profile-gating`.
- The question is about RIDs, the ILCompiler pass or the native link step — `aot-publish-gate`.
- The question is about `Dockerfile`, base images or the container runtime — `docker-aot-image`.
- You are writing an MCP **prompt** or **resource** rather than a tool — `mcp-prompts-and-resources`. The
  attributes look alike and the AOT/JSON rules differ; do not extrapolate this skill onto them.

## Critical Rules

| Rule | Why |
|---|---|
| **Always set `Name` explicitly** on `[McpServerTool]` | The derived name is snake_cased and sometimes mangled. See the table below. |
| **Never `WithToolsFromAssembly()`** | `warning IL2026` on an ordinary `dotnet build`; it is annotated `[RequiresUnreferencedCode]`. Verified. |
| **Never `new JsonSerializerOptions { TypeInfoResolver = … }`** | Replacing the resolver silently drops the MCP web defaults, with no exception: return payloads serialize PascalCase, and any **nested object** parameter gets a PascalCase schema whose camelCase body then binds to defaults instead of erroring. Scalar parameters show no symptom at all, which is what makes it hard to spot. Copy `McpJsonUtilities.DefaultOptions` and `Insert(0, …)` into its chain. |
| **Every parameter type, return type *and* `GitLabContent.Wrap<T>` payload type needs `[JsonSerializable]`** | Missing entries never fail the build. A parameter or a record return type throws `NotSupportedException` at **server startup**; a `Wrap<T>` payload type throws on that tool's **first `tools/call`**, redacted on the wire. See *Step 6*. |
| **A tool whose payload holds any GitLab-authored string declares `Task<CallToolResult>`** | The projection record becomes the `GitLabContent.Wrap` payload instead of the return type. Measured free on the schema axis, and the envelope is the only greppable provenance boundary. See *Step 4*. |
| **Never set `UseStructuredContent` or `OutputSchemaType`** | On a `CallToolResult` tool the flag publishes a schema of the *envelope*, with no `structuredContent` to match it, at zero build warnings. See *Step 4*. |
| **Never return a `GitLab.Client.Models.*` DTO** | See *Step 4*. `GitLabIssue` reaches 141 properties across the nine model types in its transitive closure. |
| **Never enumerate an `IAsyncEnumerable<T>` to completion** | It follows `Link: rel="next"` forever. Bound it and report truncation. |
| **Catch `GitLabApiException` once, in the call-tool filter** | Not per tool. See *Step 7*. |
| **`[Description]` text is a static string literal, always** | It reaches the model with system authority. Never interpolate a runtime or GitLab-derived value into it. See `mcp-untrusted-content`. |

`[McpServerTool]` is discovered on **public and non-public, instance and static** methods of the registered
type. A `private` helper you accidentally attribute becomes a tool.

## Step 1: Place the class

Tool classes live under `GitlabMCP/Tools/`. One class per resource area, marked `[McpServerToolType]`.

`GitlabMCP/Tools/IssueTools.cs` — the namespaces every snippet in Steps 1-5 assumes (`ImplicitUsings`
covers none of them):

```csharp
using System.ComponentModel;         // [Description]
using GitLab.Client.Abstractions;    // IIssuesClient and the other 142 I<Resource>Client interfaces
using GitLab.Client.Domain;          // ProjectId, GroupId
using GitLab.Client.Models;          // GitLabIssue, IssueListOptions, GitLabIssueStateFilter
using ModelContextProtocol;          // McpException
using ModelContextProtocol.Protocol; // CallToolResult - the declared return type, see Step 4
using ModelContextProtocol.Server;   // [McpServerTool], [McpServerToolType]

namespace GitlabMCP.Tools;

[McpServerToolType]
internal sealed class IssueTools(IIssuesClient issues) { … }
```

`WithTools<T>()` does not require `[McpServerToolType]` (the SDK doc: *"the attribute is not necessary when
a reference to the type is provided directly to a method like `WithTools`"*), but keep it: it documents
intent, and it is the marker any assembly-scanning discovery would key on. It plays **no** part in profile
gating — `mcp-profile-gating` keys its catalog on the *wire tool name*, which is why `Name` is mandatory.

**Constructor-inject the narrow `I<Resource>Client`, never the root `IGitLabClient`.** Both are registered by
`AddGitLabClient`. The narrow interface makes the class's blast radius readable from its first line: a
reviewer can see `IIssuesClient` and know this class cannot touch runners or CI variables. `IGitLabClient`
gives every class all 143 clients and makes the profile boundary invisible.

The SDK constructs **a new instance per tool invocation** from DI (verified in the `WithTools<T>` doc:
*"For instance methods, an instance is constructed for each invocation of the tool"*). So instance state is
per-call scratch space, never a cache. Do not hold anything expensive in a field.

## Step 2: Attribute the method

```csharp
[McpServerTool(Name = "gitlab_list_issues", Title = "List GitLab issues", ReadOnly = true, OpenWorld = false)]
[Description("Lists issues in a GitLab project, newest first. Returns a compact summary per issue; call gitlab_get_issue for the full description of one issue.")]
public async Task<CallToolResult> ListIssuesAsync(
    [Description("Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
    string project,
    [Description("Filter by state: \"opened\", \"closed\", or omit for all.")]
    string? state = null,
    [Description("Maximum issues to return, 1-100. Default 20. The result reports whether more exist.")]
    int limit = 20,
    CancellationToken cancellationToken = default)
```

**The descriptions are the entire contract.** The model never sees your C# types, your XML docs, or GitLab's
API reference — it sees the tool description, the parameter descriptions, and the generated JSON schema.
Write for a caller who has never read the GitLab docs:

- Say what the tool returns and what it does *not* (the example above points at the sibling tool for full bodies).
- Spell out accepted id shapes. `ProjectId`/`GroupId` accept a numeric id **or** a `namespace/path`; a caller
  who doesn't know that will guess wrong half the time.
- Give ranges and defaults for numbers, and the exact allowed strings for enum-ish parameters.
- Data annotations (`[Required]`, `[MaxLength]`) shape the schema but **are not enforced at runtime by the
  SDK** — validate in the method body and throw `McpException`.

### Naming — set `Name`, always

Derived names are snake_cased, and the transformation has two traps. Verified against a live server:

| C# method | Return type | Emitted tool name |
|---|---|---|
| `TaskSuffixAsync` | `Task<string>` | `task_suffix` — `Async` stripped |
| `SyncSuffixAsync` | `string` | `sync_suffix_async` — **not** stripped; the suffix only goes when the method is actually awaitable |
| `GetMRDiff` | `string` | `get_mr_diff` |
| `ListMRs` | `string` | **`list_m_rs`** |
| `HTTPServer` | `string` | `http_server` |

An acronym followed by a lowercase letter mangles. Renaming a method must never rename a tool, and the tool
name is a public contract. Set `Name`.

Use a `gitlab_<verb>_<noun>` convention so the surface is legible when a client merges several MCP servers.

### Behavioural hints

The four hints are `bool`, but the SDK tracks set-vs-unset internally and **omits unset hints from the wire
entirely**. Verified: a tool with `ReadOnly = true, OpenWorld = false` emits exactly
`"annotations":{"title":…,"openWorldHint":false,"readOnlyHint":true}` — no `destructiveHint`, no `idempotentHint`.

| Property | C# default when you read it | What clients assume when the hint is absent |
|---|---|---|
| `ReadOnly` | `false` | may modify |
| `Destructive` | `true` | destructive |
| `Idempotent` | `false` | not idempotent |
| `OpenWorld` | `true` | open world |

The defaults are already the cautious answer for writes, so the work is on reads. Set them like this:

| Tool kind | Hints to set |
|---|---|
| Read (`Get*`, `List*`, `Search*`) | `ReadOnly = true, OpenWorld = false` |
| Create (`CreateAsync`, `CreateNoteAsync`) | `Destructive = false, Idempotent = false, OpenWorld = false` |
| Update in place (`UpdateAsync`, `CloseAsync`, `LabelAsync`) | `Destructive = false, Idempotent = true, OpenWorld = false` |
| Delete / merge / cancel / rotate | `Destructive = true, Idempotent = false, OpenWorld = false` (state them, don't inherit them) |

`OpenWorld = false` is correct for essentially every tool here: the domain is one GitLab instance, not the web.

These are **hints for the client's confirmation prompt**, not authorization. The SDK's own `ToolAnnotations`
doc: *"All properties … are hints. They are not guaranteed to provide a faithful description of tool behavior
… Clients should never make tool use decisions based on `ToolAnnotations` received from untrusted servers."*
Setting them wrong still matters, though — a read tool without
`ReadOnly = true` makes a client prompt on `get_issue`, which trains the user to click through everything.

## Step 3: Thread the CancellationToken

Declare a `CancellationToken` parameter. The SDK binds it and excludes it from the JSON schema. There is **no
`CancellationToken` property on `RequestContext<T>`** — the parameter is the only route. It respects the
client's `notifications/cancelled` for this request id.

Pass it to every GitLab call, including the `await foreach`. Other parameters bound from DI rather than
arguments (also schema-excluded): `IServiceProvider`, `McpServer` (the class — there is no `IMcpServer` in
2.2.0), `IProgress<ProgressNotificationValue>`, `RequestContext<CallToolRequestParams>`, and anything the
container reports as a registered service. Verified on 2.2.0: a tool declaring all five bound every one of
them non-null and published an `inputSchema` containing only its single real argument.

## Step 4: Project the result, then wrap it

Two decisions in order: what the record holds, and what the method declares.

### The projection record

Build a small record you own. Never hand back the library's DTO.

```csharp
internal sealed record IssueSummary(
    long Iid,
    string? Title,
    string? State,
    string? Author,
    IReadOnlyList<string> Labels,
    DateTimeOffset? UpdatedAt,
    string? WebUrl);

internal sealed record IssueListResult(IReadOnlyList<IssueSummary> Issues, bool Truncated);
```

**Before** — `IReadOnlyList<GitLabIssue>`: 45 direct properties, **nine** of which are themselves models
that expand again — `Author`, `Assignee`, `Assignees`, `ClosedBy`, `Milestone`, `Iteration`, `TimeStats`,
`References`, `TaskCompletionStatus`. Nine distinct model types are reachable in total (`GitLabIssue`
included) and their direct properties sum to **141**: `GitLabUser` alone is 57, and it drags
`GitLabUserIdentity` and `GitLabCustomAttribute` in behind it. Twenty issues is a wall of JSON the model must
read to find a title. (Counted by reflection over `GitLab.Client.dll` 1.0.0 this session. `Assignee` and
`Assignees` are separate properties — miss the singular and the embedded count reads as eight.)

**After** — 7 fields the caller asked for, one nested list of strings.

Three things improve at once: token cost, prompt-injection surface (fewer attacker-writable fields reach the
model — see `mcp-untrusted-content`), and secret exposure. That last one is not hypothetical: `GitLabGroup`
carries `RunnersToken`, `GitLabVariable` carries `Value`, and every `*WithSecret` type carries a live
credential. A projection cannot leak a field it does not name.

> **Measure, don't assume.** DTO sizes in this library vary wildly. `GitLabIssue` is 45 properties;
> `GitLabProject` is a deliberately lean **11** and `GitLabMilestone` is 13. Check the real shape via
> `gitlab-client-navigation` before deciding how much projecting a given tool needs — but project anyway,
> because the DTO is not your contract and can change under you.

### The declared return type

**A `[McpServerTool]` method declares `Task<CallToolResult>` and returns `GitLabContent.Wrap(payload, source)`
— or `WrapText` for a raw trace, blob or diff — if and only if any *string* in its payload came from GitLab.**
When every field is a non-string scalar that GitLab or the server produced as a number, bool, date or enum
code (a count, an iid, a timestamp, a `Truncated` flag), the method declares the bare record `Task<TResult>`.

The record above does not change either way. Under the wrapped form it simply stops being the return type and
becomes the `Wrap<T>` payload — and it still needs its `[JsonSerializable]` entry (*Step 6*).

```csharp
[McpServerTool(Name = "gitlab_list_issues", Title = "List GitLab issues", ReadOnly = true, OpenWorld = false)]
[Description("Lists issues in a GitLab project, newest first. …")]
public async Task<CallToolResult> ListIssuesAsync(…)
{
    …
    return GitLabContent.Wrap(new IssueListResult(collected, truncated), "projects/:id/issues");
}
```

Every tool returning issue, MR, note, commit, wiki, job, label, user or search data is in the first bucket;
the second is the rare all-scalar tool (`record IssueCount(int Open, int Closed)`) and must be justified in
review. One carve-out: the call-tool filter's own curated `CallToolResult` in *Step 7* is server-authored
text and must **not** be wrapped — `GitLabContent` is for GitLab-authored bytes only.

This step owns the record. `mcp-untrusted-content` **Step 1** owns the envelope: the `GitLabContent` source,
the per-response nonce, the preamble, and `GitLabJson.Options`. Neither file works without the other.

### What that return type costs — measured, not assumed

The SDK's own `[McpServerTool]` doc lists the conversions: a `CallToolResult` is *"Returned directly without
modification"*, while *"Other types"* are *"Serialized to JSON and returned as a single `ContentBlock` … with
`Type` set to `"text"`"*. `UseStructuredContent` is the switch that adds `structuredContent` **and** an
output schema; its documented default is `false`.

One server, four tools, driven over HTTP against 2.2.0 this session:

| declared return | `UseStructuredContent` | `outputSchema` in `tools/list` | `structuredContent` in `tools/call` |
|---|---|---|---|
| `Task<IssueListResult>` | omitted | **absent** | absent |
| `Task<IssueListResult>` | `true` | the record's real schema | present |
| `Task<CallToolResult>` | omitted | **absent** | absent |
| `Task<CallToolResult>` | `true` | `CallToolResult`'s own envelope schema | absent |

Rows 1 and 3 are identical on the schema axis. With the flag omitted — which is how this server ships — a
record and a `CallToolResult` publish the same `tools/list` schema (none) and the same `tools/call` shape
minus the envelope, so *"`CallToolResult` loses your output schema"* is false here. The envelope is free; all
`CallToolResult` gives up is the *option* of setting the flag, which must not be exercised on GitLab text.

**`UseStructuredContent` is absent from every tool in this server, and `OutputSchemaType` is never set.**
Row 4 is why: at zero build warnings it publishes
`{"type":"object","properties":{"content":{"type":"array","items":{}},"structuredContent":true,"isError":…}}`
— a schema of the envelope, not of the data — and emits no `structuredContent` to satisfy it.
`OutputSchemaType` is the obvious counter-move and is rejected too: `mcp-untrusted-content` Step 1 owns that
argument, because the only way to honour the schema it publishes puts the GitLab payload on the wire a second
time, outside the envelope.

## Step 5: Bound every list

Most `List*`/`Search*` methods on the 143 clients return `IAsyncEnumerable<T>` and page transparently through
`Link: rel="next"` — **315 of 344**, counted by reflection over the shipped assembly. The other 29 return a
plain `Task<…>` — `ICodeSearchClient.ListShardsAsync`, `IDuoWorkflowsClient.ListEventsAsync` and
`IAdminMigrationsClient.ListPendingAsync` among them. Check the signature with
`gitlab-client-navigation` before writing `await foreach`.

Unbounded enumeration is an unbounded token bill and an unbounded number of HTTP calls.

Every list tool takes an explicit `limit` and **tells the caller when it truncated**, so the model never
reasons over a silently-cut list.

```csharp
private const int MaxLimit = 100;

if (limit is < 1 or > MaxLimit)
{
    throw new McpException($"limit must be between 1 and {MaxLimit}.");
}

var options = new IssueListOptions
{
    State = state switch
    {
        "opened" => GitLabIssueStateFilter.Opened,
        "closed" => GitLabIssueStateFilter.Closed,
        null or "" => null,
        _ => throw new McpException("state must be \"opened\", \"closed\", or omitted."),
    },
    // +1 so the truncation probe usually rides inside the first page; at limit == MaxLimit
    // the clamp swallows it and the probe costs one extra request.
    PerPage = Math.Min(limit + 1, MaxLimit),
};

List<IssueSummary> collected = [];
var truncated = false;

await foreach (var issue in issues.ListAsync(project, options, cancellationToken))
{
    if (collected.Count == limit)
    {
        truncated = true;   // one extra item proves more exist; stop before adding it
        break;
    }

    collected.Add(new IssueSummary(
        Iid: issue.Iid,
        Title: issue.Title,
        State: issue.State,
        Author: issue.Author?.Username,
        Labels: issue.Labels ?? [],
        UpdatedAt: issue.UpdatedAt,
        WebUrl: issue.WebUrl?.ToString()));
}

return GitLabContent.Wrap(new IssueListResult(collected, truncated), "projects/:id/issues");
```

Notes on the snippet:

- `project` is a `string` MCP parameter passed straight into a `ProjectId` parameter — `ProjectId` has an
  implicit conversion from `string` (path form) and from `long` (numeric form). No parsing, no branch, no
  hand-rolled URL encoding.
- Breaking *before* adding the `limit`-th+1 item is what makes `Truncated` truthful rather than a guess.
  Sizing `PerPage` to `limit + 1` usually keeps that probe inside the first page instead of costing a second
  request — but not at `limit == MaxLimit`, where `Math.Min` clamps it straight back to `MaxLimit`. Unmeasured
  either way; correctness does not depend on it.
- Non-list payloads need bounding too. A wiki page, a job trace (`IJobsClient.GetTraceAsync`) or an MR diff
  can each fill a context window on its own. Truncate, and say in the result that you did. Those three are
  raw text rather than a projection, so they go back through `GitLabContent.WrapText`, not `Wrap`.
- `"projects/:id/issues"` is the **route shape**, never a resolved URI — it is the `source` label on the
  envelope, and a resolved URI would carry search terms and private namespace paths. See *Step 7*.

## Step 6: Register

Two registrations, not one: the tool class **and** the GitLab client it constructor-injects.
`GitLab.Client` is not a `PackageReference` in `GitlabMCP.csproj` yet — the project has exactly one
package, `ModelContextProtocol.AspNetCore` — so the first tool that injects an `I<Resource>Client` has to
add it:

```powershell
dotnet add C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP\GitlabMCP.csproj package GitLab.Client
```

`GitlabMCP/Program.cs`:

```csharp
using GitlabMCP.Serialization;  // GitLabJson

// Registers IGitLabClient AND all 143 I<Resource>Client interfaces. Omit it and every tool
// still builds, still starts, still lists — then fails on the first call. See Common Pitfalls.
builder.Services.AddGitLabClient(builder.Configuration, "GitLab");

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithGitLabErrorMapping()
    .WithTools<IssueTools>(GitLabJson.Options);
```

`GitLabJson.Options` is a `public static JsonSerializerOptions` on `internal static class GitLabJson`, in
`GitlabMCP/Serialization/GitLabJson.cs`: a copy of `McpJsonUtilities.DefaultOptions` with
`GitlabMcpJsonContext.Default` inserted at the head of its `TypeInfoResolverChain`, then `MakeReadOnly()`.
**Its source lives in `mcp-untrusted-content` Step 1**, because the same instance is what
`GitLabContent.Wrap<T>` serializes through. Do not build a local `var toolJson` in `Program.cs` instead: a
local there is unreachable from `Tools/GitLabContent.cs`, and two option instances silently reintroduce the
PascalCase bug this step's own Critical Rule warns about. Verified on 2.2.0 that `MakeReadOnly()` does not
break `WithTools<T>(options)` — the server starts, lists, and serves camelCase payloads through the wrapper.

`AddGitLabClient` is an extension on `IServiceCollection` in namespace
`Microsoft.Extensions.DependencyInjection`, so `Program.cs` needs no extra `using` for it. The section
name `"GitLab"` is not this skill's choice: `docker-aot-image` already ships the token as
`GitLab__AccessToken`, which binds to that section. The sibling overload
`AddGitLabClient(Action<GitLabClientOptions>)` configures the same options in code instead; either way
`BaseAddress` needs its trailing slash. The option surface (`BaseAddress`, `AccessToken`,
`AuthenticationMode`, `UserAgent`, `Timeout`) belongs to `gitlab-client-navigation`.

One `WithTools<T>()` call per tool class, each passed `GitLabJson.Options`. `WithTools<T>` is not annotated
`[RequiresUnreferencedCode]`; its type parameter carries `[DynamicallyAccessedMembers]`, which is the
contract that keeps the trimmer honest. `WithToolsFromAssembly` is the annotated one, and `dotnet build`
already reports it:

```text
warning IL2026: Using member '...WithToolsFromAssembly(IMcpServerBuilder, Assembly, JsonSerializerOptions)'
which has 'RequiresUnreferencedCodeAttribute' ... The non-generic WithTools and WithToolsFromAssembly methods
require dynamic lookup of method metadata and might not work in Native AOT. Use the generic WithTools method instead.
```

`GitlabMCP/Serialization/GitlabMcpJsonContext.cs` — **every** parameter type, return type and
`GitLabContent.Wrap<T>` payload type you own goes here. Under the wrapped contract the payload type is *not*
the return type, and it is the one people forget:

```csharp
[JsonSerializable(typeof(IssueSummary))]
[JsonSerializable(typeof(IssueListResult))]
internal sealed partial class GitlabMcpJsonContext : JsonSerializerContext;
```

Primitives, `string`, `int`, `DateTimeOffset` and the MCP protocol types come free from the two contexts
already in the chain (`McpJsonUtilities.JsonContext` and `Microsoft.Extensions.AI.AIJsonUtilities.JsonContext`).

**The failure mode, verified — and it has two different timings.** Forget an entry and the build stays at
*0 warnings, 0 errors* either way. What happens next depends on whether the type reaches schema generation.

**A parameter type, or a *record* return type.** Schema generation needs it, so the server dies before it
listens:

```text
Unhandled exception. System.NotSupportedException: JsonTypeInfo metadata for type
'GitlabMCP.Tools.IssueListResult' was not provided by TypeInfoResolver of type
'[GitlabMCP.Serialization.GitlabMcpJsonContext, ModelContextProtocol.McpJsonUtilities+JsonContext,
Microsoft.Extensions.AI.AIJsonUtilities+JsonContext]' ...
   at System.Text.Json.Schema.JsonSchemaExporter.GetJsonSchemaAsNode(...)
   at Microsoft.Extensions.AI.AIJsonUtilities.CreateJsonSchemaCore(...)
   at ModelContextProtocol.Server.AIFunctionMcpServerTool.Create(...)
   at Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions.<WithTools>b__0(IServiceProvider)
```

**A `GitLabContent.Wrap<T>` payload type.** The SDK never generates a schema for it, so nothing catches it up
front: the server **starts**, `tools/list` returns every tool, and the throw lands on the **first
`tools/call` of that one tool** — from `McpJsonUtilities.GetTypeInfo[T]` inside the wrapper, redacted on the
wire to:

```json
{"content":[{"type":"text","text":"An error occurred invoking 'gitlab_list_issues'."}],"isError":true}
```

The type name appears only in the server's own log. Both timings reproduced on 2.2.0 this session.

**So a clean build proves nothing, and a clean startup no longer proves the context is complete either.
Call every tool.**

> Do not add `[JsonSourceGenerationOptions]` to the context expecting it to set camelCase. It configures the
> context's own `Default` options; when the context is attached as a resolver to a foreign
> `JsonSerializerOptions`, naming policy and ignore conditions come from *that* instance — here, the
> `McpJsonUtilities.DefaultOptions` copy.

Do not reach for `dotnet11:system-text-json-net11` for source-generation help. It covers three APIs that are
new in .NET 11 and its own frontmatter excludes `net10.0` projects; this repo is `net10.0`. For IL2026/IL3050
warnings that survive the rules above, use `dotnet-upgrade:dotnet-aot-compat` (Strategy C is the
`JsonSerializerContext` migration), and note that its Step 1 advice to add `<IsAotCompatible>` is a no-op
here — `PublishAot=true` already enables the trim, AOT and single-file analyzers.

## Step 7: Errors — mapped once

Tools do **not** catch `GitLabApiException`. One call-tool filter wraps every tool, so the mapping is written
and reviewed in one place. Verified running: it caught a real `GitLabAuthenticationException` from
`GitLab.Client` and returned the curated string below.

`GitlabMCP/Errors/GitLabErrorMapping.cs`:

```csharp
using GitLab.Client.Abstractions.Exceptions;  // GitLabApiException and its seven subtypes
using ModelContextProtocol.Protocol;          // CallToolResult, TextContentBlock
// IMcpServerBuilder, WithRequestFilters and AddCallToolFilter are all in
// Microsoft.Extensions.DependencyInjection, which ImplicitUsings already imports.

public static IMcpServerBuilder WithGitLabErrorMapping(this IMcpServerBuilder builder) =>
    builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        try
        {
            return await next(context, cancellationToken);
        }
        catch (GitLabApiException ex)
        {
            return Fail(Describe(ex));
        }
        catch (HttpRequestException)
        {
            return Fail("gitlab_transport: could not reach the GitLab instance. Retry once; if it persists the server is misconfigured.");
        }
    }));

private static string Describe(GitLabApiException ex) => ex switch
{
    GitLabAuthenticationException =>
        "gitlab_unauthenticated: the configured GitLab token is missing, expired or revoked. Do not retry.",
    GitLabForbiddenException =>
        "gitlab_forbidden: the configured GitLab token lacks the rights for this operation. Do not retry; a different token or scope is required.",
    GitLabNotFoundException =>
        "gitlab_not_found: not found, or not visible to the configured token. Check the id, then consider that it may exist but be invisible.",
    GitLabConflictException =>
        "gitlab_conflict: the resource changed or is in a state that forbids this operation. Re-read it before retrying.",
    GitLabValidationException v =>
        "gitlab_validation: GitLab rejected the request. Invalid fields: " +
        string.Join(", ", v.Errors.Keys) + ". Correct them and retry.",
    GitLabRateLimitExceededException r =>
        $"gitlab_rate_limited: retry after {(int)(r.RetryAfter?.TotalSeconds ?? 60)} seconds. Do not retry immediately.",
    GitLabServerException =>
        "gitlab_server_error: GitLab returned a server error. This is transient; retry once after a short delay.",
    _ => $"gitlab_error: GitLab returned HTTP {(int)ex.StatusCode}.",
};

private static CallToolResult Fail(string message) =>
    new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
```

Its position in the builder chain relative to `WithTools<T>` is irrelevant — both calls register into DI, and
the filter catches tools registered either side of it (verified both orders). What *does* order is
filter-to-filter: the first filter added is the outermost. The filter runs for collection-registered tools;
primitive matching happens first, then the ordinary pipeline is wrapped.

**Ownership is split, and both halves are load-bearing.** *Step 7 here owns the mapper* — the file, the
`WithGitLabErrorMapping()` extension, the `Describe(GitLabApiException)` switch, `Fail()`, which exception
type gets which `gitlab_*` code, and the `McpException`-versus-redaction distinction.
**`mcp-untrusted-content` Step 7 owns what those strings may say** — the may / must-never table, the rule
that a 404 is worded *"not found, or not visible to the configured token"* and never as proof of absence,
`GitLabValidationException.Errors` **keys** unconditionally and values only through `GitLabContent`, and the
ban on `ex.ToString()`, stack traces, `ResponseBody` and the resolved `RequestUri`. The strings above are an
*instance* of that policy; change one and re-check it there, not here.

`Fail()`'s `CallToolResult` is server-authored and must **not** go through `GitLabContent` — the envelope is
for GitLab-authored bytes only. Verified on 2.2.0 that the filter is unaffected by the `Task<CallToolResult>`
return contract: an `HttpRequestException` thrown from a `CallToolResult`-returning tool and from a
record-returning tool both came back as the same curated `gitlab_transport: …` result with `isError: true`.

**In-tool validation is different** — that is the tool's own job, and `McpException` is the tool's own channel:

```csharp
throw new McpException($"limit must be between 1 and {MaxLimit}.");
```

Verified reaching the model as
`{"content":[{"type":"text","text":"An error occurred invoking 'gitlab_list_issues': limit must be between 1 and 100."}],"isError":true}`.
Any **other** exception type also produces an `isError` result, but with a generic message — the SDK redacts
it deliberately. So an unhandled `InvalidOperationException` tells the model nothing; use `McpException`
whenever the message is meant to be read.

Note both forms return a *result* with `isError: true`, not a JSON-RPC `error` frame. Errors are model-visible
context and are subject to every rule in `mcp-untrusted-content`.

## Step 8: Assign the tool to profiles

A tool that is not assigned to a profile is not finished. Profiles determine which tools are advertised at
all, and — critically — hiding a tool from `tools/list` does not stop `tools/call` reaching it. See
`mcp-profile-gating`; do not invent gating inside the tool body.

## Verification

Run in order. Each catches a different class of failure.

```powershell
# 1. Build. Catches CS errors, IL2026 from WithToolsFromAssembly, and the trim/AOT analyzers
#    (PublishAot=true enables them on ordinary builds — no publish needed).
dotnet build C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP.slnx
```

```bash
# Bash equivalent
dotnet build /c/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP.slnx
```

Expect `0 Avertissement(s), 0 Erreur(s)` (this machine's MSBuild UI is French; the diagnostic *codes* are
never localized, so match on `IL[0-9]{4}`, never on prose).

```powershell
# 2. Start the server. Catches a missing [JsonSerializable] on a parameter or a record return type.
dotnet run --project C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP
```

A `System.NotSupportedException: JsonTypeInfo metadata for type ...` here means Step 6 is incomplete. A clean
startup does **not** mean it is complete: a `GitLabContent.Wrap<T>` payload type never reaches schema
generation, so it survives startup and `tools/list` and fails on the first call.

3. Drive `tools/list` **and `tools/call` on every tool you wrote** — use `mcp-server-smoke-test`. Read the
   emitted schema, name and annotations: the name is what you set, `CancellationToken` is absent from the
   schema, every parameter carries its description, and a wrapped tool's result is the preamble block plus a
   `<gitlab-data nonce="…" source="…">` block, with **no** `structuredContent` on the call and **no**
   `outputSchema` in the listing. Steps 1 and 2 both pass on a tool whose `I<Resource>Client` was never
   registered *and* on a tool whose payload type is missing from the context; only a real call finds either.

4. Run the AOT publish gate — `aot-publish-gate`. The debug build does not exercise the ILCompiler pass or
   the native link step. Any IL2xxx/IL3xxx warning is a regression; hand the warning itself to
   `dotnet-upgrade:dotnet-aot-compat`.

## Checklist

Per `[McpServerTool]` method. Any unchecked box is a blocking finding.

- [ ] `Name` set explicitly, `gitlab_<verb>_<noun>`. It is a wire contract, so renaming a tool that already shipped breaks callers — and nothing enforces that here (`GitlabMCP.csproj` sets no `<Version>`, so `serverInfo.version` is the assembly default `1.0.0.0` and cannot mark a break). Review is the only guard: diff the name against the running server's `tools/list`.
- [ ] `[Description]` on the method **and every parameter**, written for a caller who cannot read the GitLab docs, naming accepted id shapes and value ranges.
- [ ] All `[Description]` text is a static literal — no interpolation of runtime or GitLab-derived values.
- [ ] `ReadOnly = true` on reads; `Destructive` and `Idempotent` stated explicitly on writes; `OpenWorld = false`.
- [ ] Constructor injects narrow `I<Resource>Client` interfaces, not `IGitLabClient`; no state held in fields.
- [ ] `CancellationToken` parameter declared and passed to every GitLab call including the `await foreach`.
- [ ] Declared return type is `Task<CallToolResult>` returning `GitLabContent.Wrap`/`WrapText` (a bare projection record only when every field is a non-string scalar); the payload is a record you own, not a `GitLab.Client.Models.*` DTO; no member named `Token`, `Secret`, `Password`, `Key`, `RunnersToken`; no `*WithSecret` type referenced.
- [ ] `UseStructuredContent` and `OutputSchemaType` are absent.
- [ ] Every list has an explicit `limit`, a server-enforced maximum, and a truncation flag in the result; large single payloads are truncated and say so.
- [ ] Arguments validated in the body; failures thrown as `McpException`.
- [ ] No `catch (GitLabApiException)` in the tool — the filter owns it.
- [ ] Every parameter type, return type **and `GitLabContent.Wrap<T>` payload type** has a `[JsonSerializable]` entry in `GitlabMcpJsonContext`.
- [ ] Registered with `WithTools<T>(GitLabJson.Options)`; no `WithToolsFromAssembly`; no second `JsonSerializerOptions` instance anywhere.
- [ ] `AddGitLabClient` is in `Program.cs` and `GitLab.Client` is a `PackageReference` — a build and a clean startup prove neither.
- [ ] Assigned to profiles per `mcp-profile-gating`.
- [ ] Build clean, server starts, `tools/list` shows the expected contract, **`tools/call` answered on every tool** (a clean `tools/list` no longer proves the serialization context is complete), AOT publish clean.

## Common Pitfalls

| Pitfall | Fix |
|---|---|
| `error CS0246: McpException` not found | It lives in the root `ModelContextProtocol` namespace, not `ModelContextProtocol.Server`. Add `using ModelContextProtocol;`. |
| Build clean, server dies at startup with `NotSupportedException` | Missing `[JsonSerializable]` for a parameter type or a *record* return type. The exception names the type. |
| Build clean, server starts, `tools/list` fine, one tool returns `"An error occurred invoking 'x'."` on every call | Missing `[JsonSerializable]` for a `GitLabContent.Wrap<T>` **payload** type. Nothing on the wire names it; the server log carries `NotSupportedException: JsonTypeInfo metadata for type 'X'` thrown from `McpJsonUtilities.GetTypeInfo[T]`. The wire text is identical to the missing-`AddGitLabClient` row below — that one breaks every tool in the class, this one breaks exactly one. Read the log. |
| Results serialize PascalCase, or a **nested object** parameter arrives with default values | You replaced the resolver instead of chaining, so the MCP web defaults are gone. Scalar-only tools show no symptom. Copy `McpJsonUtilities.DefaultOptions`, then `TypeInfoResolverChain.Insert(0, …)`. |
| Tool renamed itself after a refactor | `Name` was not set; the name was derived from the method. |
| `IMcpServer` not found | There is no such interface in 2.2.0 — the only `IMcpServer*` name in `ModelContextProtocol.Core` is `IMcpServerPrimitive`. The type you want is the abstract class `McpServer`. |
| `RequestContext<T>.CancellationToken` not found | It does not exist. Declare a `CancellationToken` parameter. |
| Tool hangs or bills thousands of tokens | An `IAsyncEnumerable` enumerated to completion. Bound it. |
| Client prompts for confirmation on a read tool | `ReadOnly` defaults to `false`; set `ReadOnly = true`. |
| Builds, starts and lists fine, but **every** call returns `"An error occurred invoking 'x'."` | The tool's `I<Resource>Client` is not in the container — `AddGitLabClient` is missing from `Program.cs`. The SDK redacts the construction `InvalidOperationException`, so nothing on the wire names the missing service; read the server's own log. Check this **before** suspecting the tool body. |
| One tool returns `"An error occurred invoking 'x'."` with no detail | A non-`McpException` escaped that tool body. The SDK redacts it on purpose. Throw `McpException`, or let the filter map it. |
| `[Required]` / `[MaxLength]` not enforced | They shape the schema only. Validate in the method body. |
