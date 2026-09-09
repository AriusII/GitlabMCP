# GitlabMCP architecture

Owner file for DEC-018 (project split), DEC-011/DEC-020/DEC-021 (the GraphQL path), and DEC-019/DEC-025
(tool surface). `DECISIONS.md` carries the *choice* for each; this file carries the *shape*. Read
`CLAUDE.md` first for the product framing (profiles, personas, commands) — this file is the "how the six
libraries plus the host actually fit together" reference.

## Project layout

Seven `src/*` projects (six class libraries + the host), plus `tests/`. See DEC-018 for why the count is
seven, not six, despite the "6 projects" phrasing the split was requested under.

```
GitlabMCP.slnx
Directory.Build.props / Directory.Build.targets / Directory.Packages.props
src/
  GitlabMCP.Abstractions/   McpProfile, Grant flags, options POCOs — no dependencies at all
  GitlabMCP.Contracts/      payload records, GitLabJson.Options, the GitLabContent envelope, the
                            JsonSerializerContext skeleton every domain's tools extend
  GitlabMCP.Mapping/        GitLab.Client DTO -> Contracts record projections, one file per domain
  GitlabMCP.GraphQL/        the hand-rolled GraphQL client (DEC-020/021) — work items / epics
  GitlabMCP.Profiles/       ProfileCatalog (DEC-024: partial per-domain fragments), ProfileGate,
                            AssertCatalogIsComplete
  GitlabMCP.Tools/          [McpServerToolType] classes, one per domain, plus [McpServerPrompt]s
  GitlabMCP/                the host: Program.cs, DI wiring, config binding, error-mapping filter
tests/
  GitlabMCP.Tests/
  snapshots/                golden per-profile tool/prompt lists (DEC-014)
```

Dependency direction is strictly downward — `Abstractions` has no references; `Contracts` references only
`Abstractions`; `Mapping` and `GraphQL` reference `Contracts`; `Profiles` references only `Abstractions`;
`Tools` references `Mapping`, `GraphQL`, `Contracts`, `Profiles`; the host references everything. No cycle
is possible because the `.csproj` files themselves forbid one. This is what makes CLAUDE.md's "keep the
profile boundary explicit and data-driven" and "inject the narrow interfaces" rules enforced by the
compiler rather than by review: `GitlabMCP.Profiles` cannot see a tool body, and `GitlabMCP.Contracts`
cannot see a `GitLab.Client` DTO.

Class libraries carry no `RuntimeIdentifier(s)`, no `PublishAot`/`SelfContained`/`PublishSingleFile`/
`InvariantGlobalization` — those five properties live only on the host (`Directory.Build.targets` enforces
this with a build-breaking `<Error>` if violated). Every library gets `IsAotCompatible=true` from
`Directory.Build.props`, so an IL2xxx/IL3xxx warning surfaces at *library* build time, not only at the
host's `dotnet publish` gate.

## The four layers, in the order data flows through them

1. **`GitLab.Client`** (external package) returns a DTO (`GitLab.Client.Models.*`) or throws a
   `GitLabApiException`.
2. **`GitlabMCP.Mapping`** projects that DTO into an owned record declared in `GitlabMCP.Contracts` —
   never returns the DTO itself, never leaks a field the projection doesn't name (this is where the
   closed inventory of secret-shaped fields — `RunnersToken`, `*.Value` on a variable, `IsAdmin`, etc. —
   is enforced by simply not being in the record).
3. **`GitlabMCP.Tools`** calls the narrow `I<Resource>Client` (constructor-injected, never the root
   `IGitLabClient`), calls the matching `Mapping` function, and returns either the bare projection record
   (every field a server/GitLab-produced non-string scalar) or `GitLabContent.Wrap`/`WrapText` around it
   (any GitLab-sourced string in the payload) — DEC-007 decides which, per tool, not per domain.
4. **The host**'s call-tool filter (`GitlabMCP/Errors/GitLabErrorMapping.cs`) catches
   `GitLabApiException` (REST) and `GitLabGraphQlException` (GraphQL) in one place and produces the
   `gitlab_*`-prefixed error vocabulary — no tool body ever catches either exception type itself.

## The GraphQL path (DEC-011=C / DEC-020 / DEC-021)

`GitLab.Client` has no `IEpicsClient`/`IWorkItemsClient` — verified by grep, zero matches. Epics and work
items are served by a small hand-rolled client in `GitlabMCP.GraphQL` against `POST {root}/api/graphql`,
which is a **sibling** of `/api/v4/`, not nested under it — derived by walking one path segment up from
`GitLabClientOptions.BaseAddress`, never by string-appending `graphql` (that 404s in a way that reads like
a permissions problem).

Neither `GraphQL.Client` nor StrawberryShake is AOT-clean (verified: the former's request envelope
inherits `Dictionary<string, object>`, structurally reflection-shaped; the latter's own maintainer states,
November 2025, 51 of 89 internal projects are still not AOT-compliant) — DEC-021 ratifies the hand-rolled
design. Shape: a typed `IGitLabGraphQlClient` (via `AddHttpClient<,>`), per-operation typed variables
records (never `object`/`Dictionary<string,object>`), `JsonTypeInfo<T>`-typed serialize/deserialize
overloads only, one shared `GitLabJson.Options` instance (no second `JsonSerializerOptions`), and a
parallel `GitLabGraphQlException` hierarchy feeding the same error-mapping filter the REST client uses.

**HTTP 200 is never success** for a GraphQL response: check the top-level `errors` array first (non-empty
⇒ failure, checked *before* looking at `data`), then, for a mutation, the payload's own `errors: [String!]!`
field. `GraphQLError.Message` is always free text (can echo caller-supplied variable content back verbatim)
and is **never** concatenated into the plain `gitlab_*` error string — only `GraphQLError.Path` (which
schema field failed) is safe to relay unconditionally; a message that must reach the model goes through
`GitLabContent.WrapText` as GitLab-sourced data. A work-item field resolving to `null` with zero `errors`
is never read as proof of absence — it can mean Premium/Ultimate tier-gating, visibility, or a missing id;
the client runs a `workItemTypes(name: EPIC)` check before create/update and fails with an explicit
tier-naming message rather than a null payload.

Every GraphQL list query caps `first`/`last` at ≤100 (GitLab's hard server-side per-connection cap) — no
list tool omits an explicit bound.

## Profile gating (`GitlabMCP.Profiles`)

Four profiles: `Maintainer`, `Developer`, `DevOps`, `FullPermission`. `Grant` is a flags enum;
`Grant.Full = Maintainer | Developer | DevOps | AdminOnly` (DEC-005) — a union, not a fourth
hand-maintained list. `AdminOnly` is the one extra bit for instance-administration tools that belong to no
persona.

The active profile is resolved **once at startup** (DEC-004=A) from configuration key `GitLabMcp:Profile`
(env `GitLabMcp__Profile`); absent falls back to `Maintainer` (narrowest surface), present-but-unparseable
throws before the server listens. Tool/prompt registration stays static (`WithTools<T>(GitLabJson.Options)`
per domain class); the gate removes ungranted primitives from `ToolCollection`/`PromptCollection` once,
covering every per-request session in `HttpServerSessionMode.Stateless` because each request's collection
starts as a fresh copy of the same statically-populated set.

`ProfileCatalog` is a `static partial class` (DEC-024): each tool domain contributes its grant rows in its
own file (`ProfileCatalog.Planning.cs`, `ProfileCatalog.MergeRequests.cs`, …), merged into one dictionary
by a single non-partial method in `ProfileCatalog.cs`. Only that merged dictionary is ever queried by
`ProfileGate` or by a tool class — no `Tools/` class references a fragment file directly.

`ProfileGate.AssertCatalogIsComplete` runs once at startup and fails fast if: any registered tool/prompt/
resource has no catalog row; any catalog row names nothing registered; any `Grant` member's mask is
`Grant.None`; or any tool's catalog `readOnly` flag disagrees with its registered `ReadOnlyHint` (DEC-006).
`ResourceGrants` exists from the very first `[McpServerResource]` (DEC-023) — there is no grace period
where the first resource is silently granted to every profile. A resource's registered name is read from
whichever of `ProtocolResource`/`ProtocolResourceTemplate` is non-null (DEC-029) — a direct resource
carries the former, a templated one the latter, never both; gating and the completeness sweep both check
both fields, since checking only `ProtocolResource` would silently drop every templated resource from
every profile (a real bug caught and fixed in the same commit as the first templated resource, DEC-029).

## Serialization (`GitlabMCP.Contracts`)

One process-wide `JsonSerializerOptions` instance, `GitLabJson.Options`: a copy of
`McpJsonUtilities.DefaultOptions` with `GitlabMcpJsonContext.Default` and `GitLabGraphQlJsonContext.Default`
inserted into `TypeInfoResolverChain`, then `MakeReadOnly()`. Every `WithTools<T>()`/`WithPrompts<T>()`
call and every `GitLabContent.Wrap<T>` call serializes through this one instance — never a second
`JsonSerializerOptions`, and never `SomeContext.Default.<T>` directly (that carries no naming policy and
silently emits PascalCase where the MCP wire and GitLab's GraphQL API are both camelCase).

**Not** one shared context split across per-domain files — that was the original design and it is
build-broken on this SDK (DEC-026): splitting `[JsonSerializable]` attributes for the *same* partial
`JsonSerializerContext` class across two or more files throws `CS8785` (`ArgumentException: hintName …
must be unique`), reproduced on two SDK patch versions. The actual, working shape is **one
`JsonSerializerContext` class per domain, each confined to its own single file**: `GitlabMcpJsonContext`
(Ping + Epics, `GitlabMCP.Contracts/Serialization/GitlabMcpJsonContext.cs`), thirteen per-tool-domain
contexts (`AdminJsonContext`, `CicdJsonContext`, …, `SearchJsonContext`), `ResourcesJsonContext` (the two
server-authored resource records, DEC-029), and `GitLabGraphQlJsonContext` (GraphQL request/response
types — its own two-file trick: a base file with zero `[JsonSerializable]` attributes and a
`.WorkItems.cs` file carrying all of them, still only one attribute-bearing file). `Program.cs` inserts
every one of these `.Default` instances into `GitLabJson.Options.TypeInfoResolverChain` before calling
`Seal()`; insertion order among them never matters, since each domain's record types are disjoint.
`GenerationMode = Metadata` is set explicitly on every context (this server needs schema generation and
deserialization everywhere, not just a serialize-only fast path).

## The envelope (`GitLabContent`, DEC-007/DEC-008/DEC-010/DEC-021/DEC-028)

A tool declares `Task<CallToolResult>` and returns `GitLabContent.Wrap`/`WrapText` **iff** any string in
its payload originated from GitLab; otherwise it declares the bare projection record. A resource whose
content originates from GitLab declares `ReadResourceResult` and returns
`GitLabContent.WrapResource`/`WrapResourceText` (DEC-028) — the identical preamble+nonce+delimiter
envelope, carried in a `TextResourceContents` instead of a `TextContentBlock`. One envelope per result
(not per field) — a `<gitlab-data nonce="…" source="…">` wrapper with a fresh, cryptographically-random
nonce per response, so a forged closing tag inside GitLab text cannot end the untrusted region early.
Server-authored, non-GitLab resource content (the `gitlab-mcp://server/*` resources) never goes through
this type. `UseStructuredContent` and `OutputSchemaType` are never set on any tool (DEC-008).

## Tool surface (DEC-019/DEC-022/DEC-025/DEC-029)

The research pass produced 722 verified REST candidates across the 13 domains below. DEC-025 first cut
that to a 180-tool implemented slice with the remaining 542 as a specified backlog
(`tool-catalog-backlog.json`); that backlog has since been fully implemented (DEC-025/DEC-029), so all
723 REST candidates (the original 722, plus one gap the post-implementation review pass found and filled — `gitlab_list_discussion_notes`, DEC-027/review-findings) plus 7 GraphQL epic/work-item tools plus the `gitlab_ping` canary are live — **731
tools total**. `tool-catalog-backlog.json` is retained as a historical record of the original research
pass, not a pending work list. Every tool's wire name is `gitlab_<verb>_<noun>` (DEC-022), set explicitly
— never the SDK-derived name.

The 13 domains (research-pass boundaries, not necessarily 1:1 with `Tools/` files, though each domain's
tools do live in their own `<Domain>Tools.cs`): planning, discussion, mergerequests, code, people,
projects, cicd, packages, search, infra, deploy, admin, lifecycle. Plus GraphQL work-items/epics as a
14th, non-REST-derived slice.

## Prompt and resource surface (DEC-029/DEC-030/DEC-031)

Two prompt classes: `ProfilePrompts` (one per-profile overview guide each — `gitlab_maintainer_guide`/
`gitlab_developer_guide`/`gitlab_devops_guide`) and `TaskPrompts` (six task-oriented workflows one level
more specific — `gitlab_triage_issue_backlog`, `gitlab_review_merge_request`, `gitlab_plan_iteration`,
`gitlab_investigate_pipeline_failure`, `gitlab_prepare_release`, `gitlab_audit_access_review`). Every
prompt argument is `string`/`int` (never `bool` — a measured interop trap where a JSON-string-sending
client's `"true"` is rejected) and every body is a static string literal naming only real tool names —
never GitLab-derived text, per `mcp-untrusted-content`'s rule for anything delivered with the server's own
authority.

Two resource classes under `GitlabMCP.Tools.Resources`: `ServerResources` (direct, parameterless,
server-authored — `gitlab-mcp://server/info` and `gitlab-mcp://server/profiles`, never
`GitLabContent`-wrapped) and `GitLabEntityResources` (templated, one GitLab entity by URI —
`gitlab://issue/{projectId}/{iid}`, `gitlab://merge_request/{projectId}/{iid}`,
`gitlab://epic/{groupId}/{iid}`, `gitlab://project/{projectId}/file/{filePath}/{refName}`,
`gitlab://wiki/{projectId}/{slug}`, `gitlab://pipeline/{projectId}/{pipelineId}`,
`gitlab://release/{projectId}/{tagName}` — each reusing the exact client call and mapper, and granted
identically to, its equivalent `gitlab_get_*` tool). `WithResources<T>()` has no `JsonSerializerOptions`
overload, so every resource parameter/return type stays inside the SDK's own context (`string`, `long`,
`ReadResourceResult`) — the payload record types themselves are already registered for the equivalent
tool, so no new `[JsonSerializable]` entries are needed for the GitLab-content resources; the two
server-authored ones get their own small `ResourcesJsonContext` (DEC-026's one-context-per-domain pattern
applied to a "resources" domain).

## Configuration (DEC-012/DEC-013/DEC-032)

Six keys, no `appsettings.json`: `GitLab:AccessToken`, `GitLab:BaseAddress`, `GitLabMcp:Profile`,
`GitLabMcp:Version`, `AllowedHosts`, `Mcp:AllowedOrigins`. Container env spelling is unprefixed with `__`
(`GitLab__AccessToken`), except `AllowedHosts` which the image sets as `ASPNETCORE_ALLOWEDHOSTS`. A Tier-2
secret (`/run/secrets/...`) is read via `AddKeyPerFile("/run/secrets", optional: true)` (DEC-013, option
A) — verified AOT-clean by this repo's own gate run, not assumed. `AddOptionsWithValidateOnStart` wires
both configured option types so a missing PAT or malformed `BaseAddress`/`Profile` fails before
`app.Run()`. `GitLabMcp:Version` is the one key an operator never sets directly — the Dockerfile bakes it
in from `ARG APP_VERSION` at image build time (DEC-032); absent, it resolves to `"dev"` exactly like an
absent `Profile` resolves to `Maintainer`. `GitLab:BaseAddress` (default `https://gitlab.com/api/v4/`)
already supports a self-hosted domain, a path-prefixed self-hosted install, and a non-standard port —
`GitLabGraphQlEndpointTests` pins this down and `README.md`/`compose.yaml` document it.

## Error mapping — one vocabulary, two transports

`GitlabMCP/Errors/GitLabErrorMapping.cs` is the single cross-cutting filter pair — one for
`tools/call`, one for `resources/read` (DEC-028). It maps `GitLabApiException` (REST:
`GitLabAuthenticationException` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409, `Validation`
400/422, `RateLimitExceeded` 429, `Server` 5xx), `HttpRequestException` (transport),
`OperationCanceledException` (cancellation), and `GitLabGraphQlException` (GraphQL — DEC-021) onto the
same `gitlab_*`-prefixed vocabulary. A tool returns it as a soft `CallToolResult.IsError`; a resource has
no such field on `ReadResourceResult`, so it is thrown as `McpException` instead — its `.Message` is
still relayed to the client, rather than collapsing to the SDK's generic redacted "An error occurred."
No tool or resource body ever writes its own `catch`.

## CI/CD (DEC-032)

`.github/workflows/ci.yml` (every push/PR to `main`, no secrets required) runs `dotnet build`+`dotnet test`
in Release, then a `docker build` for `linux/amd64` with `push: false` — that build stage's `dotnet
publish -r linux-musl-x64` is the real Native AOT gate, so every PR exercises it, not just a release.
`.github/workflows/release.yml` (a `vX.Y.Z` tag push) re-runs the same gate, then pushes one
`linux/amd64` image to Docker Hub as `<user>/gitlabmcp:X.Y.Z-amd64`/`:X.Y.Z`/`:latest-amd64`/`:latest`
(`provenance: false` — a flat manifest, never a multi-arch index) and publishes a GitHub Release.
`global.json` pins the SDK (`10.0.401`) so the local build, the Docker build stage, and
`actions/setup-dotnet@v4` all resolve the same toolchain. Neither workflow runs the `win-x64` publish
`aot-publish-gate`/`aot-gatekeeper` own — CI's AOT coverage is the Linux/musl publish inside Docker.

## What was actually built and verified (2026-09-09)

**731 tools** (723 REST across 13 domains + 7 GraphQL epic/work-item tools + the `gitlab_ping` canary),
**9 prompts**, **2 direct + 7 templated resources**, the full profile gate (covering all three primitive
kinds), the error-mapping filter pair, the hand-rolled GraphQL client, and the resilience handler are
implemented and live. Verified this session, in order:

1. `dotnet build GitlabMCP.slnx -c Release` — 0 warnings, 0 errors, all 7 `src/*` projects + `tests/`.
2. Every tool `tools/call`-ed at least once against a running server (DEC-007) — zero SDK-redacted
   generic failures; every failure was either the expected `gitlab_transport` (fake endpoint) or a
   correctly-worded `McpException` from the tool's own validation. An adversarial multi-dimension review
   pass (secret-field leakage, envelope-contract correctness, validation/hints, correctness/quality,
   profile-grant correctness) ran across every domain — see `docs/review-findings.md`.
3. Profile gating verified live across all four profiles for every primitive kind — golden lists captured
   to `tests/snapshots/` (DEC-014): `<profile>.tools.txt`, `.prompts.txt`, `.resources.txt`,
   `.resource_templates.txt`.
4. `resources/read` exercised on every direct resource and one templated resource per profile
   (`mcp-server-smoke-test`'s stronger guarantee for prompts/resources: a clean startup proves every
   parameter/return type is fully source-generated, not merely that the happy path was tried); an
   unauthenticated GitLab-content resource read confirmed to surface the mapped `gitlab_unauthenticated`
   message, not a generic redacted one (DEC-028).
5. `dotnet publish -c Release -r win-x64` — 0 IL2xxx/IL3xxx warnings, `Generating native code` present;
   the published single-file binary run standalone and driven over real HTTP, including `resources/read`
   and `prompts/get`.
6. `docker build` for `linux-musl-x64` (the actual container target) — 0 warnings; the resulting
   container run, `tools/call`-ed over its forwarded port, and its `/healthz` endpoint checked.
7. `tests/GitlabMCP.Tests/` (37 tests) built and passing — `GitlabMCP.slnx` had referenced this project
   before its files existed, so `dotnet build` was actually broken (`MSB3202`) until this session fixed it
   (DEC-032).
8. `docker build --build-arg APP_VERSION=9.9.9-test` plus `docker run -e GitLabMcp__Profile=Developer -e
   GitLab__BaseAddress=https://gitlab.example.internal/api/v4/` — startup log and a live `resources/read`
   on `server_info` both confirmed the version, profile, and self-hosted GitLab target end to end
   (DEC-032). `.github/workflows/*.yml` validated for YAML syntax and cross-checked by an independent
   multi-agent review pass; the workflows themselves have not yet executed on GitHub Actions infrastructure
   (they run on the next push/tag).

## What is NOT built

- A `FullPermission`-only raw-GraphQL escape hatch (mentioned in the GraphQL design as a future option,
  not built).
- Multi-tenancy (DEC-015 settled this repo as single-tenant; a broker design is out of scope by decision,
  not by omission).
- The `linux-x64` (glibc) Docker gate — `linux-musl-x64` (Alpine, the image this repo actually ships) was
  run; `linux-x64` uses the same Dockerfile with `--build-arg RID=linux-x64` and a glibc base image and
  was not separately exercised this session.
- Resource subscriptions (`subscribe`/`unsubscribe`/`list_changed` notifications) — the SDK exposes them,
  but `resources` never advertises `subscribe` under `HttpServerSessionMode.Stateless` (this server's
  mode), so there is nothing to subscribe to; unexercised by design, not by oversight.
- A group-scoped wiki-page resource (`gitlab_get_wiki_page`'s `group` branch) — the resource template
  covers only the project-scoped case, to keep one simple shape per template (DEC-031); the group case
  remains tool-only.
