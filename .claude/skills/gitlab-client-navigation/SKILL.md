---
name: gitlab-client-navigation
description: >
  Find the correct GitLab.Client API among its 143 resource clients by grepping the package's
  XML documentation instead of guessing, and use what you find correctly.
  This is the reference and the recipes you run yourself; the gitlab-api-scout agent runs them
  and returns one compact answer.
  USE FOR: "which client does X live on", "what methods are on IPipelinesClient", finding the client
  that owns a GitLab REST route, reading a method's summary/params/exceptions, ProjectId / GroupId /
  ToRouteValue, IAsyncEnumerable vs Task return shapes and pagination cost, the GitLabApiException
  hierarchy, AddGitLabClient / GitLabClientOptions / GitLabAuthenticationMode, the missing Epics API.
  DO NOT USE FOR: writing the [McpServerTool] method itself, its [Description] text, its projection
  record or its JsonSerializerContext entry (use mcp-tool-authoring); deciding which profile a tool
  belongs to or how tools are gated (use mcp-profile-gating); IL2xxx/IL3xxx warning triage
  (use dotnet-upgrade:dotnet-aot-compat).
---

# GitLab Client Navigation

`GitLab.Client` 1.0.0 exposes **143 resource client interfaces** and ~1,230 public types. Nobody
memorises that. This skill is the lookup procedure: the package ships a 3.2 MB XML documentation file
next to its DLL, and grepping it answers "which client, which method, what does it take" in one
command. Guessing an API name here is the most common way to burn an hour on code that does not
compile.

**Use it when** you need a GitLab operation and do not know which of the 143 clients owns it; you know
the route (`POST /projects/:id/merge_requests/:iid/approve`) and need the C# method; you need an exact
parameter list, documented exceptions, or whether a method streams; or you are about to write
`gitLab.Something.SomethingAsync(...)` from memory. **Not** for authoring the tool wrapper
(`mcp-tool-authoring`) — and if you only want to know whether epics are supported, jump straight to
[The Epics gap](#the-epics-gap).

## Critical Rules

| Rule | Why |
|---|---|
| **Never state an API name you have not grepped.** | 143 clients, ~1,630 methods. Plausible-sounding names are usually wrong. |
| **The XML file does not record return types.** | A `<member name="M:...">` key encodes *name + parameter types only*. `ListAsync(ProjectId,MilestoneListOptions,ct)` tells you nothing about `IAsyncEnumerable<GitLabMilestone>`. Use the naming rule in [Return shapes](#return-shapes-and-pagination), or reflect the DLL (R5). |
| **The XML is not complete either.** | One population throughout this file: **1,633 methods on the 143 resource clients** (the XML documents 20 more on `IGitLabApiConnection` — excluded everywhere below). Of those 1,633: `<summary>` **100%**, a literal `<c>VERB /route</c>` **35.9%** (587), `<param>` **6.4%** (104), `<returns>` 3.5% (57), `<remarks>` 2.0% (33), `<exception>` 0.7% (12). R4 usually gives you a summary and nothing else. A recipe returning nothing usually means *undocumented*, not *absent*. The assembly is the authority; the XML is the index. |
| **Never hand-roll URL encoding for a project or group path.** | `ProjectId` / `GroupId` already percent-encode via `ToRouteValue()`. Double-encoding gives a 404 that looks like a permissions problem. |
| **Never enumerate an `IAsyncEnumerable` to completion in a tool.** | It walks every `Link: rel="next"` page — unbounded GitLab calls and an unbounded token bill. |
| **Transport failures and cancellation are not wrapped.** | `catch (GitLabApiException)` does not catch them. See [Exceptions](#exceptions). |

When you re-measure any of that yourself, match `<param ` **with the trailing space** (or `<param name=`).
Bare `<param` also matches `<paramref`, a prose cross-reference tag carrying no parameter documentation —
84 member blocks use one — and inflates the `<param>` figure from 104 to 180.

**Count `<exception>` per member, and say which population you counted.** The file holds **26
`<exception>` tags on 24 members**: 12 resource-client methods, **10 of the 20 `IGitLabApiConnection`
methods**, and both `AddGitLabClient` overloads. So the same tags read out as **12 over the 1,633** used
here and **22 over the 1,653** that include `IGitLabApiConnection` — `gitlab-api-scout` quotes the 1,653
figure. Unlike `<param>` / `<returns>` / route coverage, where the 20-method delta moves only the last
digit, `IGitLabApiConnection` is documented at ~50% exception coverage against 0.7% for the resource
clients, so on this one row the population choice nearly doubles the answer. Both numbers are correct;
neither is interchangeable.

## Step 0: Locate the XML file

The package is **not referenced yet** — `GitlabMCP.csproj` carries one `PackageReference`,
`ModelContextProtocol.AspNetCore` 2.2.0. Add it first:

```bash
dotnet add C:/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP/GitlabMCP.csproj package GitLab.Client
```

Restore drops the XML beside the DLL in the global package cache. Cache folder names are lowercased;
the file name keeps the package's casing.

```bash
# Git Bash — set once per session; every recipe below uses $XML
XML=$(ls ~/.nuget/packages/gitlab.client/*/lib/net10.0/GitLab.Client.xml | tail -1)
echo "$XML"
```

```powershell
# PowerShell 7
$xml = (Get-ChildItem "$env:USERPROFILE\.nuget\packages\gitlab.client\*\lib\net10.0\GitLab.Client.xml" |
        Select-Object -Last 1).FullName
$xml
```

If that path does not resolve, do not proceed by guessing. The cache root is relocatable — `NUGET_PACKAGES`
or a `globalPackagesFolder` in `nuget.config` moves it — so ask the tooling where it is rather than
assuming `~/.nuget/packages`:

```bash
dotnet nuget locals global-packages --list   # -> global-packages: C:\Users\Arius\.nuget\packages\
```

If the package genuinely is not there, `dotnet restore` first. (A `.nupkg` extracted anywhere works just
as well: the file lives at `lib/net10.0/GitLab.Client.xml` inside the package.)

## Recipe R1 — list every client interface

```bash
grep -o 'name="T:GitLab\.Client\.Abstractions\.I[A-Za-z0-9]*Client"' "$XML" \
  | sed 's/.*Abstractions\.//; s/"$//' | sort -u
```

```powershell
Select-String -Path $xml -Pattern 'name="T:GitLab\.Client\.Abstractions\.(I\w+Client)"' -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
```

Both return **144** names: the 143 resource clients plus the root `IGitLabClient` (append
`| grep -v '^IGitLabClient$'` / `| Where-Object { $_ -ne 'IGitLabClient' }` for just the 143).
Reflection finds one more interface this grep misses — `IGitLabApiConnection`, the low-level transport
seam. It is not a resource client, is not reachable from `IGitLabClient`, and MCP tools have no
business touching it.

## Recipe R2 — every method on one client, with its parameter list

```bash
C=IPipelinesClient
grep -o "name=\"M:GitLab\.Client\.Abstractions\.$C\.[^\"]*\"" "$XML" \
  | sed "s/^name=\"M:GitLab\.Client\.Abstractions\.$C\.//; s/\"$//" \
  | sed 's/System\.Threading\.CancellationToken/ct/g; s/GitLab\.Client\.Domain\.//g;
         s/GitLab\.Client\.Models\.//g; s/System\.Nullable{\([^}]*\)}/\1?/g;
         s/System\.Int64/long/g; s/System\.String/string/g; s/System\.Boolean/bool/g'
```

```powershell
$c = 'IPipelinesClient'
Select-String -Path $xml -Pattern "name=`"M:GitLab\.Client\.Abstractions\.$c\.([^`"]*)`"" -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
  ForEach-Object { $_ -replace 'System\.Threading\.CancellationToken','ct' `
                      -replace 'GitLab\.Client\.(Domain|Models)\.','' `
                      -replace 'System\.Nullable\{([^}]*)\}','$1?' `
                      -replace 'System\.Int64','long' -replace 'System\.String','string' `
                      -replace 'System\.Boolean','bool' }
```

Real output — 13 methods, byte-identical from both shells (first 7 shown):

```text
ListAsync(ProjectId,PipelineListOptions,ct)
GetAsync(ProjectId,long,ct)
CreateAsync(ProjectId,CreatePipelineRequest,ct)
CancelAsync(ProjectId,long,ct)
RetryAsync(ProjectId,long,ct)
DeleteAsync(ProjectId,long,ct)
ListForCurrentUserAsync(UserPipelineListOptions?,ct)
```

The `?` on `UserPipelineListOptions?` is **not** reference nullability — it is `System.Nullable{T}`,
which means the options type is a **struct**. See [Naming conventions](#naming-conventions).

## Recipe R3 — find the owner of an operation by keyword

The interface name is carried in every member key, so a keyword search across all 143 clients is free.

```bash
grep -o 'name="M:GitLab\.Client\.Abstractions\.I[^"]*"' "$XML" \
  | sed 's/^name="M:GitLab\.Client\.Abstractions\.//; s/"$//' \
  | grep -i 'approve' \
  | sed 's/GitLab\.Client\.Domain\.//g; s/GitLab\.Client\.Models\.//g;
         s/System\.Threading\.CancellationToken/ct/g; s/System\.Int64/long/g'
```

```powershell
$kw = 'approve'
Select-String -Path $xml -Pattern 'name="M:GitLab\.Client\.Abstractions\.(I\w+Client\.[^"]*)"' -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
  Where-Object { $_ -match $kw } |
  ForEach-Object { $_ -replace 'System\.Threading\.CancellationToken','ct' `
                      -replace 'GitLab\.Client\.(Domain|Models)\.','' -replace 'System\.Int64','long' }
```

Eight hits, identical in both shells:

```text
IAccessRequestsClient.ApproveForProjectAsync(ProjectId,long,ApproveAccessRequestRequest,ct)
IAccessRequestsClient.ApproveForGroupAsync(GroupId,long,ApproveAccessRequestRequest,ct)
IDeploymentsClient.ApproveAsync(ProjectId,long,ApproveDeploymentRequest,ct)
IMembersClient.ApproveForGroupAsync(GroupId,long,ct)
IMembersClient.ApproveAllForGroupAsync(GroupId,ct)
IMergeRequestApprovalsClient.ApproveAsync(ProjectId,long,ApproveMergeRequestRequest,ct)
IMergeRequestApprovalsClient.UnapproveAsync(ProjectId,long,ct)
IUsersClient.ApproveAsync(long,ct)
```

`grep -i` is required in bash; PowerShell's `-match` is case-insensitive by default.

## Recipe R3b — find the owner of a REST route

When you know the GitLab endpoint, search the summaries rather than the names. Routes appear as
`<c>GET /projects/:id/...</c>` inside `<summary>`. The state machine below attributes each hit to the
member that actually contains it — a naive `grep -B` blames the wrong member.

```bash
route() {
  awk -v pat="$1" '
    /<member name="M:GitLab\.Client\.Abstractions\.I/ { n=$0; hit=0; next }
    /<member name="/                                  { n="";  hit=0; next }
    /<\/member>/ { if (n!="" && hit) print n; n=""; hit=0; next }
    { if (n!="" && $0 ~ pat) hit=1 }
  ' "$XML" | sed 's/.*Abstractions\.//; s/(.*//'
}
route 'merge_requests/:iid/approve'   # -> IMergeRequestApprovalsClient.ApproveAsync
route 'jobs/:job_id/trace'            # -> IJobsClient.GetTraceAsync
```

```powershell
function Find-GitLabRoute($pat) {
  $n = ''; $hit = $false
  Get-Content $xml | ForEach-Object {
    if     ($_ -match '<member name="M:GitLab\.Client\.Abstractions\.I') { $n = $_; $hit = $false }
    elseif ($_ -match '<member name="')  { $n = ''; $hit = $false }
    elseif ($_ -match '</member>')       { if ($n -and $hit) { $n -replace '.*Abstractions\.','' -replace '\(.*','' }; $n = ''; $hit = $false }
    elseif ($n -and $_ -match $pat)      { $hit = $true }
  }
}
Find-GitLabRoute 'merge_requests/:iid/approve'
```

**Only 35.9% of members carry a literal route**, so a miss here is inconclusive — fall back to R3 on a
noun lifted out of the route (`protected_branches`, `award_emoji`, `resource_label_events`).

## Recipe R4 — read one member's summary, params and exceptions

The XML is pretty-printed with one `<member>` per block, so a line-state machine beats an XML parser
and needs no Python.

```bash
member() { awk -v p="$1" '/<member name="/{keep = index($0,p)>0} keep' "$XML" | sed 's/^ *//'; }
member 'IMergeRequestsClient.MergeAsync'
```

```powershell
function Get-GitLabMember($want) {
  $keep = $false
  Get-Content $xml | ForEach-Object {
    if ($_ -match '<member name="') { $keep = $_ -like "*$want*" }
    if ($keep) { $_.Trim() }
  }
}
Get-GitLabMember 'IMergeRequestsClient.MergeAsync'
```

Real output (abridged — the real block also carries a `<param>` per parameter):

```xml
<member name="M:GitLab.Client.Abstractions.IMergeRequestsClient.MergeAsync(GitLab.Client.Domain.ProjectId,System.Int64,GitLab.Client.Models.MergeMergeRequestRequest,System.Threading.CancellationToken)">
<summary>
Merges a merge request (<c>PUT /projects/:id/merge_requests/:iid/merge</c>) and returns it in its
merged state.
</summary>
<exception cref="T:GitLab.Client.Abstractions.Exceptions.GitLabConflictException">
The merge request cannot be merged - it has conflicts, or the supplied Sha is no longer the head ...
</exception>
</member>
```

The same function works on a type key — `member 'T:GitLab.Client.Domain.ProjectId"'`; keep the closing
quote so it does not also match longer neighbours.

## Recipe R5 — the only reliable source of return types

Reflect the DLL. This is a file-based C# app (`dotnet run dump.cs`); see
`dotnet-advanced:csharp-scripts` if the form is unfamiliar. Point `dll` at the DLL beside the XML from
Step 0.

```csharp
#:package System.Reflection.MetadataLoadContext@10.0.0
using System.Reflection;

string dll = @"C:\Users\Arius\.nuget\packages\gitlab.client\1.0.0\lib\net10.0\GitLab.Client.dll";
var refDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var asm = new MetadataLoadContext(
        new PathAssemblyResolver([.. Directory.GetFiles(refDir, "*.dll"), dll]))
    .LoadFromAssemblyPath(dll);

static string N(Type t) =>
    t.IsGenericType
        ? $"{t.Name[..t.Name.IndexOf('`')]}<{string.Join(", ", t.GetGenericArguments().Select(N))}>"
        : t.Name switch { "Int64" => "long", "Int32" => "int", "String" => "string",
                          "Boolean" => "bool", "Void" => "void", _ => t.Name };

foreach (var t in asm.GetTypes()
             .Where(x => x.IsInterface && x.Namespace == "GitLab.Client.Abstractions")
             .OrderBy(x => x.Name))
{
    Console.WriteLine($"### {t.Name}");
    foreach (var m in t.GetMethods())
        Console.WriteLine($"  {N(m.ReturnType)} {m.Name}({string.Join(", ",
            m.GetParameters().Select(p => $"{N(p.ParameterType)} {p.Name}"))})");
}
```

Run it **once**, redirect into the scratchpad, and grep that file for the rest of the session rather
than re-reflecting. Reflection is also the only way to spot **overload pairs**, which the XML shows as
two indistinguishable keys — e.g. `IEnvironmentsClient.StopAsync(ProjectId, long)` and
`StopAsync(ProjectId, long, bool force)`.

**The resolver carries the shared framework and nothing else.** That is enough for the
`GitLab.Client.Abstractions` filter above, because every type in those signatures is either a framework
type or a `GitLab.Client` one. Widen the `Where` — e.g. to `GitLabClientServiceCollectionExtensions` —
and reading a signature throws
`System.IO.FileNotFoundException: Could not find assembly 'Microsoft.Extensions.Http, Version=10.0.0.0'`
(reproduced: `GetTypes()` and `GetMethods()` still succeed; it throws the moment you touch
`ReturnType` or `GetParameters()`). Fix by appending the dependency to the resolver's file list —
`~/.nuget/packages/microsoft.extensions.http/10.0.11/lib/net10.0/Microsoft.Extensions.Http.dll`.

## Recipe R6 — LSP, once the package is referenced

Once the `PackageReference` is in place and the project compiles, the `LSP` tool beats grep for "what is
this thing": `workspaceSymbol` (`query: "IPipelinesClient"`) locates the interface, `hover` on a call site
renders the full XML doc **including the return type grep cannot give you**, and
`goToDefinition` / `goToImplementation` work from your own `Tools/` code. All operations take `filePath`
plus **1-based** `line` and `character`. LSP needs a compiling project; grep needs nothing but the file.
**Grep for discovery, LSP for confirmation while editing.**

## Naming conventions

Measured across the 143 resource interfaces (1,633 methods by reflection):

| Convention | Evidence |
|---|---|
| `I<Resource>Client` | All 143, each individually injectable as well as reachable off `IGitLabClient`. **Inject the narrow interface** — the constructor then documents the profile boundary. |
| `...Async` suffix | **1,633 of 1,633.** There are no sync overloads anywhere. |
| `CancellationToken cancellationToken = default` last | **1,633 of 1,633**, always optional. |
| Route id first | `ProjectId` 885x, `GroupId` 306x, bare `long` 166x, `string` 74x, `CancellationToken` (instance-scoped, no id) 67x. |
| Verb prefixes | `Get`, `List`, `Delete`, `Create`, `Update`, `Download`, `Set`, `Add`, `Search`, `Upload`, `Revoke`, `Remove`, `Rotate`, `Import`. |
| Scope suffixes | `...ForGroupAsync` 154x, `...ForProjectAsync` 135x, `...ForUserAsync`, `...ForCurrentUserAsync`. Instance scope is the bare name or `...AllAsync` (`ListAsync` = what the caller can see; `ListAllAsync` = the admin view). There is no `ForInstanceAsync`. |
| `Iid` vs `Id` in the **parameter name** | `mergeRequestIid`, `issueIid`, `epicIid` are project/group-scoped iids; `...Id` is a global id. **Milestones are the trap** — `IMilestonesClient` takes `long milestoneId`, a global id. The `T:IMilestonesClient` summary says so ("Every milestone is addressed by its `id`, not its `iid`... Filter by `GitLabMilestone.Iid` through the list options instead") but no *method* block repeats it — `GetAsync`'s whole summary is "Gets one project milestone by id." If you have an iid, filter `ListAsync` with **`MilestoneListOptions.Iids`** (plural — "Return only the milestones with these iids") and read `Id` off the result. |
| `GitLab<Entity>` read models | **528** `GitLab*`-prefixed read models, out of **1,065** public types in `GitLab.Client.Models` (807 classes, 131 enums, 127 structs) — the rest are the `*Request`, `*Options` and enum types below. |
| `<Verb><Entity>Request` bodies | 364 types, **all classes**: `Create*` 101, `Update*` 94, `Add*` 11, `Set*` 9, `Import*` 9, `Protect*` 6, `Merge*` 4. |
| `<Entity>ListOptions` query filters | 142 list/get/statistics options types: **127 structs, 15 classes.** A struct appears in a signature as `Nullable{XListOptions}`; a class appears bare. |

The 15 class-shaped options types are the heavily-filtered resources: `Issue`, `MergeRequest`, `Project`,
`Pipeline`, `Milestone`, `Deployment`, `User`, `PersonalAccessToken`, `GroupIssue`, `GroupProject`,
`GroupSharedProject` and `GroupHierarchy` + `ListOptions`, plus `IssueStatisticsOptions`,
`GroupIssueStatisticsOptions` and `SidekiqQueueJobDeleteOptions`.

**Three clients carry several `<summary>` elements inside one `T:` entry**, because the interface is split
across partial files and each file documents itself: `IIntegrationsClient` (6), `IPackagesConanClient` (5),
`IPackagesDebianClient` (2). Still one client each, not several. R4 on their type key returns the whole pile
in declaration order and **the overview is not first** — for `IIntegrationsClient` it is the *third*
(the first two are the "part B" / "Part C" typed-setter fragments). Read the whole block and take the
summary that opens `Wraps the GitLab "..." API area`; that is the overview on every client.

## ProjectId and GroupId

`public readonly struct` in `GitLab.Client.Domain`. Verified surface, identical on both:

```csharp
static ProjectId FromId(long id);
static ProjectId FromPath(string namespacedPath);   // GroupId: FromPath(string fullPath)
static implicit operator ProjectId(long id);
static implicit operator ProjectId(string namespacedPath);
string ToRouteValue();
bool IsNumeric { get; }
```

> `ProjectId` — "Identifies a GitLab project either by its numeric ID or by its URL-encoded
> `namespace/project` path, as accepted by the `:id` route parameter across the GitLab REST API."
>
> `ToRouteValue` — "The value to substitute into the route, already percent-encoded when it is a
> namespaced path."

**The implicit conversions are the single most important fact for MCP tool authoring.** Declare one
`string` parameter and pass it straight through — no parsing, no branching, no encoding:

```csharp
// tool parameter: string project  ("278964" or "gitlab-org/gitlab")
await issuesClient.ListAsync(project, options, cancellationToken);
//                           ^^^^^^^ string -> ProjectId, implicitly
```

The tool signature around that call — `[McpServerTool]`, the `[Description]` text, the `limit`
parameter, the projection record — belongs to `mcp-tool-authoring`; the `Task<CallToolResult>` wrapper
that record is returned in belongs to `mcp-untrusted-content`.

Rules:

- **One string parameter per route id.** Do not expose `id` and `path` as two parameters, and do not parse.
- `"42"` as a *string* takes the path overload. If you specifically need the numeric route, take a `long`.
- Never call `Uri.EscapeDataString` on the value — `ToRouteValue()` already encodes it.
- Parameter naming is near-total (`ProjectId projectId` 887/888, `GroupId groupId` 294/306); the
  exceptions are `GroupId namespaceId`, `GroupId moduleNamespace`, `ProjectId forkedFromId`. Only two
  methods in the whole library take both a `ProjectId` and a `GroupId`.

## Return shapes and pagination

Only two shapes exist across all 1,633 resource-client methods: `Task` / `Task<T>` (1,317) and
`IAsyncEnumerable<T>` (316). No `ValueTask`, no `IEnumerable`, no `Task<IAsyncEnumerable<T>>`.

**The naming rule:** a method returns `IAsyncEnumerable<T>` **iff** its name starts with `List` or
`Search`. Measured: 344 so named, 315 correct, **29 false positives and 1 miss.**

- The 1 miss: `IRepositoryFilesClient.GetBlameAsync`.
- The 29 false positives are named `List*`/`Search*` but return `Task<T>`, because the endpoint is not
  `Link`-paged. They sit in exactly three families and nowhere else — **admin/ops**
  (`IActiveContextClient`, `IAdminMigrationsClient`, `ICodeSearchClient`, `IInternalClient`,
  `IKnowledgeGraphClient`, `ISearchClient`), **AI/ML** (`IDuoClient`, `IDuoWorkflowsClient`,
  `IMlExperimentsClient`, `IMlModelsClient`), and **package-registry protocol clients** (Conan, NuGet,
  Composer, Generic, Terraform modules — these speak the upstream ecosystem's own search protocol
  rather than GitLab's paged REST convention). `ISearchClient`'s real search methods
  (`SearchIssuesAsync`, `SearchProjectCommitsAsync`, …) *do* stream; only
  `SearchProjectSemanticCodeAsync` and `ListSearchMigrationsAsync` do not.

Regenerate the exact list at any time by adding this to the R5 loop:

```csharp
bool named = m.Name.StartsWith("List") || m.Name.StartsWith("Search");
bool ae    = m.ReturnType.Name.StartsWith("IAsyncEnumerable");
if (named != ae) Console.WriteLine($"{(ae ? "MISS" : "FP")} {t.Name}.{m.Name} -> {m.ReturnType.Name}");
```

**XML-only fallback** (no DLL to hand): a streaming method's `<summary>` opens with the word
"Streams" — *"Streams every milestone of a project, following GitLab's `Link` pagination."* 257
summaries say it, covering ~82% of the 316 streamers, with exactly two false friends:
`IRepositoriesClient.GetRawBlobAsync` and `IRepositoryFilesClient.GetRawAsync` say "Streams" about the
HTTP body but return `Task<GitLabFileResponse>`. `name starts List|Search` **AND** `summary says
"Streams"` is near-perfect; disagreement means check the DLL.

**Cost of enumeration.** Pagination is automatic and invisible: the enumerator follows RFC 5988
`Link: rel="next"` until GitLab stops offering one. A bare `await foreach` over `ListAsync(projectId)`
on a large project issues a request per page indefinitely.

```csharp
await foreach (var item in client.ListAsync(project, options, cancellationToken))
{
    ...
    if (--budget == 0) break;   // the break is the only thing that stops the next page being fetched
}
```

The `break` is load-bearing — call it out in review. Where the budget comes from, how truncation is
reported to the model, and sizing `PerPage` so the truncation probe is free are `mcp-tool-authoring`'s
"Bound every list". The `CancellationToken` is documented as cancelling "the enumeration, per page", so
it interrupts between pages, not mid-page.

Other shapes worth recognising:

| Shape | Meaning |
|---|---|
| bare `Task` (**345** methods) | GitLab answers HTTP 201 or 204 with no body. Common on `Delete*`, also `IMilestonesClient.PromoteAsync`. |
| `Task<GitLabFileResponse>` (128) | Binary or streamed download — artifacts, raw blobs, `GetRawDiffsAsync`. Needs truncation before it reaches a model. |
| `Task<JsonElement>` (**61 methods across 18 clients**) | Untyped escape hatch. **No schema to project — poor MCP tool candidates**, and the 18 include `IAnalyticsClient` and `IJobsClient`, which CLAUDE.md puts in three of the four profiles. Never work from a remembered list: `grep 'Task<JsonElement>' out.txt` on the R5 dump. |
| `Task<IReadOnlyList<T>>` | Buffered POST responses and unpaginated admin lists. |
| `Task<bool>` | e.g. `IBranchesClient.ExistsAsync`, which returns `false` rather than throwing `GitLabNotFoundException`. |

## Exceptions

Rooted at `GitLabApiException` in `GitLab.Client.Abstractions.Exceptions`. Quoted from the XML:

| Type | HTTP | What it means for the caller |
|---|---|---|
| `GitLabAuthenticationException` | 401 | "The token is missing, malformed, expired or revoked. Re-authenticating is the only useful response - retrying the same request is not." |
| `GitLabForbiddenException` | 403 | "Retrying will not help; the token needs more rights, or the caller needs a different one." **This is what a profile granting more than the token allows produces** — surface it as a token-rights problem, never as a tool bug (wording policy: `mcp-untrusted-content` §Step 7). |
| `GitLabNotFoundException` | 404 | "GitLab deliberately answers 404 rather than 403 for private resources the caller may not know about, so a 404 does not prove the resource is absent - only that this token cannot see it." Word tool errors as *"not found, or not visible to the configured token"* (wording policy: `mcp-untrusted-content` §Step 7, which forbids phrasing a 404 as proof of absence). |
| `GitLabConflictException` | 409 | "Re-read the resource and decide again; retrying the identical request will conflict identically." |
| `GitLabValidationException` | 400 / 422 | Fix the input. `Errors` maps GitLab's field names to its messages when GitLab returned its structured form; **empty, never null**, when GitLab returned a plain-string message (the common shape for 400). |
| `GitLabRateLimitExceededException` | 429 | "Wait `RetryAfter` and retry; the request itself was fine." `RetryAfter` handles both the delta-seconds and the HTTP-date forms. |
| `GitLabServerException` | any 5xx | "Retrying with backoff is usually appropriate." One type covers 500/502/503/504 on purpose — they differ in cause but not in what a caller can do. |

On the base type: `StatusCode`, `RequestMethod`, `RequestUri`, `ResponseBody`, `IsTransient` — "true
when the same request is worth retrying unchanged: HTTP 408, 429 and any 5xx. Lets a caller write one
retry policy without switching on `StatusCode` or on the exception type" — and an overridden
`ToString()`.

**Deliberately NOT wrapped**, from the base type's own `<remarks>`:

> "Transport-level failures are deliberately not wrapped: a DNS, TLS or socket failure still surfaces
> as `HttpRequestException`, and a cancelled or timed-out request still surfaces as
> `TaskCanceledException`, so cancellation and retry semantics stay intact."

So `catch (GitLabApiException)` does not catch a network outage or a client-side timeout. Note that
`TaskCanceledException : OperationCanceledException`, so a cooperative cancel and a `Timeout` expiry
arrive as the same type — distinguish them by checking whether the caller's token was actually
signalled.

Mapping all of this onto MCP results is a **cross-cutting concern: do it once, not per tool.** Ownership
of that mapping is split, and the halves live in different files:

- **The mapper — the code.** `GitLabErrorMapping.cs`, the one call-tool filter, the
  `Describe(GitLabApiException)` switch and the stable `gitlab_*` codes: **`mcp-tool-authoring` §Step 7.**
- **What those strings may say.** The may / must-never table — never `ToString()`, never `ResponseBody`,
  never a resolved `RequestUri`, `GitLabValidationException.Errors` keys unconditionally but its values
  only through `GitLabContent`, and the rule that a 404 is never phrased as proof of absence:
  **`mcp-untrusted-content` §Step 7.**

One hop to the wrong one and you get the mechanism without the policy, or the reverse.

## Registration and options

```csharp
builder.Services.AddGitLabClient(options =>
{
    options.BaseAddress = new Uri("https://gitlab.com/api/v4/");   // trailing slash REQUIRED
    options.AccessToken = builder.Configuration["GITLAB_TOKEN"]
                          ?? throw new InvalidOperationException("GITLAB_TOKEN is not set.");
    options.AuthenticationMode = GitLabAuthenticationMode.PersonalAccessToken;
});
```

Four public entry points on
`Microsoft.Extensions.DependencyInjection.GitLabClientServiceCollectionExtensions`:

| Method | Use |
|---|---|
| `AddGitLabClient(IServiceCollection, Action<GitLabClientOptions>)` | Code configuration. |
| `AddGitLabClient(IServiceCollection, IConfiguration, string sectionName = "GitLab")` | Config binding. AOT-safe because **GitLab.Client source-generates its own binder** — the binding happens inside the library's assembly, so nothing in `GitlabMCP.csproj` is doing that work. |
| `AddGitLabClientCore(IServiceCollection)` | Core plumbing only, no resource clients. |
| `AddResourceClients(IServiceCollection)` | Registers the 143 client interfaces. |

**`AddGitLabClient` throws `InvalidOperationException` if called twice on the same `IServiceCollection`** —
documented on both overloads: resource registrations use `TryAdd`, but "options and HTTP-client
configuration delegates accumulate, so a second call would otherwise become a silent last-writer-wins
across two different configurations." That is load-bearing for CLAUDE.md's still-open *profile source*
decision: a design that registers per profile or per request throws at composition. **Call it exactly
once**, whatever the profile design turns out to be.

Both `AddGitLabClient` overloads return an `IHttpClientBuilder`, so resilience handlers chain onto it.
`GitLabClientDefaults.HttpClientName` names the underlying `HttpClient`.

`GitLabClientOptions` (namespace `GitLab.Client.DependencyInjection`) has exactly five properties:
`Uri BaseAddress`, `string AccessToken`, `GitLabAuthenticationMode AuthenticationMode`,
`string UserAgent`, `TimeSpan Timeout`. **Only `BaseAddress` and `AccessToken` carry XML docs** — the
other three exist in metadata only. That is the general lesson restated: the XML indexes the library,
the assembly defines it.

`GitLabAuthenticationMode`, verbatim:

| Value | Header |
|---|---|
| `PersonalAccessToken` | "Sends the token via the `PRIVATE-TOKEN` header." |
| `OAuthBearer` | "Sends the token via `Authorization: Bearer`." |
| `JobToken` | "Sends the token via the `JOB-TOKEN` header, for use inside a GitLab CI/CD job." |

Two traps. **The trailing slash on `BaseAddress` is load-bearing** — `Uri` relative resolution drops the
last segment without it, so `https://gitlab.example.com/api/v4` silently resolves against
`https://gitlab.example.com/`. And there is a `GitLabClientOptionsValidator`, so a malformed
`BaseAddress` or a missing token fails at **startup**, not at the first call.

## The Epics gap

Verified against both the XML and the assembly. This is a hard constraint, not a maybe:

- **No `IEpicsClient`. No `IWorkItemsClient`.** Absent from the 144 XML type entries and from the 145
  interfaces reflection finds in `GitLab.Client.Abstractions`.
- **No type anywhere in the assembly with `Epic` or `WorkItem` in its name.** No `GitLabEpic`, no
  `CreateEpicRequest`, no `EpicListOptions`. You cannot read, create, update, delete, list **or search**
  an epic with this library.
- **Exactly 26 methods on exactly 4 interfaces** expose epic *sub-resources*, all keyed
  `(GroupId groupId, long epicIid)`:

| Interface | # | Names |
|---|---:|---|
| `IAwardEmojiClient` | 8 | `ListForEpicAsync`, `GetForEpicAsync`, `AddToEpicAsync`, `DeleteFromEpicAsync` plus the four `...EpicNote...` equivalents |
| `IDiscussionsClient` | 9 | `ListForEpicAsync`, `GetForEpicAsync`, `CreateForEpicAsync`, `ResolveForEpicAsync`, `ListNotesInEpicDiscussionAsync`, `AddNoteToEpicDiscussionAsync`, `GetNoteInEpicDiscussionAsync`, `UpdateNoteInEpicDiscussionAsync`, `DeleteNoteFromEpicDiscussionAsync` |
| `INotesClient` | 5 | `ListEpicNotesAsync`, `GetEpicNoteAsync`, `CreateEpicNoteAsync`, `UpdateEpicNoteAsync`, `DeleteEpicNoteAsync` |
| `IResourceEventsClient` | 4 | `ListEpicLabelEventsAsync`, `GetEpicLabelEventAsync`, `ListEpicStateEventsAsync`, `GetEpicStateEventAsync` |

Naming is inconsistent across the four — `IAwardEmojiClient`/`IDiscussionsClient` suffix
`...ForEpicAsync`; `INotesClient`/`IResourceEventsClient` infix `...Epic...Async`. **Match `Epic`
anywhere in the name**, not as a suffix: pipe the R3 output through `grep -i epic`.

The library states its own reasoning:

> `IAwardEmojiClient.ListForEpicAsync` `<remarks>` — "Epics are a Premium feature and the epics
> themselves are deprecated in favour of work items, but the reaction routes are the only way to read
> an epic's reactions over REST today."

**This is an open decision and must be made deliberately, before any epic tool is written.** CLAUDE.md
lists epics under both the Maintainer and Developer profiles, and nothing satisfies that today.

| Option | Buys you | Costs |
|---|---|---|
| **A. Drop epics.** Build planning tools on `IIssuesClient` + `IMilestonesClient` + `IIterationsClient` + `IBoardsClient`. | No new dependency, entirely within the AOT/no-reflection rules, and aligned with where GitLab is heading. | The profile tables need amending — amend them rather than leaving a tool that cannot exist. |
| **B. Comment-only epic surface.** Ship the 26 sub-resource methods. | Real value for triage on an epic the caller already has open. | The caller must already know the `epicIid`; the server cannot discover, list or search epics, so these are unusable as an entry point. Only defensible alongside A. |
| **C. A separate GraphQL path** against `/api/graphql`. | The only way to get epic / work-item CRUD. | A second HTTP client and auth path, hand-written request/response DTOs each needing a `[JsonSerializable]` entry, and no `GitLabApiException` mapping to inherit. A project, not an afternoon. |

If you take C: the endpoint is **`/api/graphql`**, *not* under `/api/v4`. GitLab's GraphQL API sits beside
the REST root, so it **cannot be reached by resolving a relative URI against
`GitLabClientOptions.BaseAddress`** (`https://gitlab.com/api/v4/`) — that yields
`https://gitlab.com/api/v4/graphql`, which does not exist, and the 404 looks like a permissions problem.
Build the absolute URI from the instance origin instead. GitLab.Client mentions GraphQL four times in its
XML and never as a URL: there is nothing in the package to inherit here.

Do not begin B or C without recording the decision.

## Domain map

The 143 clients grouped into 10 families — verified exhaustive and disjoint (143 assigned, 0 duplicates,
0 missing) — live in **[`references/domain-map.md`](references/domain-map.md)**. Read it when you have a
GitLab concept but no candidate client name and R3 keyword search has come up empty. Two placements
surprise people: `IReleasesClient` is under CI/CD (GitLab's own "Deploy" grouping), and dependency-list /
SBOM work is `IDependenciesClient`, filed under security.

Largest clients, if you are about to run R2 on one: `IPackagesConanClient` 59, `IIntegrationsClient` 49,
`IDiscussionsClient` 44, `IProjectsClient` 39, `IGroupsClient` 39, `IMergeRequestsClient` 36,
`INotesClient` 35, `IUsersClient` 34.


## Checklist

Before writing a line that calls `GitLab.Client`:

- [ ] The XML path from Step 0 resolves; you did not guess it.
- [ ] The client interface name came out of R1, R3 or R3b — not from memory.
- [ ] The method name and its full parameter list came out of R2, R3 or R3b.
- [ ] The **return type** came from R5 or LSP `hover` — never inferred from the XML, which cannot supply it.
- [ ] If the method is named `List*` / `Search*`, you checked it against the three false-positive families before assuming it streams.
- [ ] Every `IAsyncEnumerable` consumption `break`s on an explicit, server-clamped limit.
- [ ] Route ids are single `string` parameters relying on the implicit conversion; nothing is manually URL-encoded.
- [ ] You know whether your options type is a struct (`XListOptions?` in the signature) or one of the 15 classes.
- [ ] Failures go through the one cross-cutting `GitLabApiException` mapper (`mcp-tool-authoring` §Step 7; what its strings may say is `mcp-untrusted-content` §Step 7), with `HttpRequestException` and `TaskCanceledException` handled separately because they are not wrapped.
- [ ] `AddGitLabClient` is called exactly once — a second call on the same `IServiceCollection` throws.
- [ ] If epics are involved, option A / B / C is chosen and recorded.

Then hand off, to three files and not two:

- the tool signature, its `[Description]` text, the `limit` parameter, the projection record and the
  `JsonSerializerContext` entry → **`mcp-tool-authoring`**;
- the `Task<CallToolResult>` envelope that record is wrapped in, and what an error string may say →
  **`mcp-untrusted-content`**;
- which profile advertises the tool → **`mcp-profile-gating`**.
