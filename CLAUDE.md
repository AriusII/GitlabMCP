# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A GitLab-only MCP server: C# 14 / .NET 10, Native AOT, shipped as a Docker image, speaking MCP over HTTP. It wraps the `GitLab.Client` NuGet package and exposes GitLab's API to MCP clients as a **profile-gated** tool surface (see *Profiles*).

**Current state: implemented and verified.** The template is gone. The solution is seven `src/*` projects (DEC-018) implementing **731 tools** (722 REST across 13 domains, 7 GraphQL epic/work-item tools, 1 canary — the full original 722-tool research catalog, DEC-025's 180-tool slice plus its entire 542-tool backlog, both now implemented), **9 prompts** (3 per-profile guides plus 6 task-oriented workflows, DEC-030), and **2 direct + 7 templated resources** (server metadata plus one-entity-by-URI reads for issues/MRs/epics/files/wiki pages/pipelines/releases, DEC-028/DEC-029/DEC-031), profile gating (now covering all three primitive kinds), error mapping (tools and resources alike), and the full GitLab/GraphQL client wiring. `docs/architecture.md` is the up-to-date shape reference and `docs/tool-catalog.md` the tool list — both reflect what is actually in the tree, not intent. Verified this session: `dotnet build GitlabMCP.slnx` is 0 warnings/0 errors; every tool was `tools/call`-ed at least once against a running server with zero SDK-redacted failures; `dotnet publish -c Release -r win-x64` and `docker build` for `linux-musl-x64` are both 0 IL2xxx/IL3xxx warnings and the resulting binary/container both answer real MCP traffic, including `resources/read` and `prompts/get`; golden per-profile snapshots for all four primitive kinds live in `tests/snapshots/`. `docs/tool-catalog-backlog.json` is now historical — every candidate it listed is implemented (DEC-025/DEC-029). `tests/GitlabMCP.Tests/` is a real, building, passing xUnit project (37 tests) — `GitlabMCP.slnx` referenced it before the project files existed; that gap is closed. GitHub Actions CI/CD (DEC-032) is live: `ci.yml` gates every push/PR with build+test+a no-push Docker/AOT build, `release.yml` publishes versioned `version-amd64`/`version`/`latest-amd64`/`latest` images to Docker Hub on a `vX.Y.Z` tag. The GitLab target (`GitLab:BaseAddress`, default `https://gitlab.com/api/v4/`) already supported self-hosted/custom-domain/path-prefixed instances at the config layer — now covered by tests and documented in `README.md` and `compose.yaml`.

`DECISIONS.md` (the decision log — record decisions there, never inline in a skill) and eight project skills plus four agents under `.claude/` specify this architecture in detail. **Read the relevant skill before working in its area** — they carry verified command output, measured baselines and settled contracts that this file only summarises. Several skills/agents still describe the pre-implementation, single-project state or an open decision DECISIONS.md has since closed (DEC-004, DEC-011, DEC-018 in particular) — where a skill and DECISIONS.md disagree on a *choice*, DECISIONS.md wins; the skill still owns the *mechanism*. `.claude/skills/`: `mcp-tool-authoring` (the flagship — how to write a tool), `mcp-prompts-and-resources`, `gitlab-client-navigation`, `mcp-profile-gating`, `mcp-untrusted-content`, `aot-publish-gate`, `mcp-server-smoke-test`, `docker-aot-image`. `.claude/agents/`: `gitlab-mcp-tool-builder`, `gitlab-mcp-reviewer`, `gitlab-api-scout`, `aot-gatekeeper`.

The MCP package is `ModelContextProtocol.AspNetCore` **2.2.0**. This was bumped from 2.1.0 by an agent rather than by a deliberate decision — see `DECISIONS.md`; it builds and AOT-publishes clean, and the skills are verified against 2.2.0.

## Commands

```bash
dotnet build GitlabMCP.slnx                    # solution is .slnx (XML), not .sln — 7 src/* projects + tests
dotnet test GitlabMCP.slnx                     # pure unit tests only — no network, no live GitLab
dotnet run --project src/GitlabMCP             # http://localhost:9080 (profile "https" adds :9443)
```

Native AOT publish (the real gate — the debug build does *not* exercise it):

```powershell
# vswhere.exe must be on PATH or the ILCompiler link step fails with MSB3073 / exit code 123
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish src/GitlabMCP/GitlabMCP.csproj -c Release -r win-x64
```

Substitute any RID from `<RuntimeIdentifiers>` — `win-x64`, `linux-x64`, `linux-musl-x64`; `linux-musl-x64` is the one that matters for the container image. **arm64 is deliberately out of scope** (`win-arm64`, `osx-arm64` and `linux-arm64` were removed, which drops macOS as a target). Do not reintroduce an arm64 RID, an arm64 image, or a multi-arch build without being asked. A publish that emits **any** IL2xxx/IL3xxx warning is a regression — the current tree publishes clean with zero warnings, keep it that way.

Manual MCP smoke test: `GitlabMCP.http` posts a `tools/call` JSON-RPC request at `http://localhost:9080`. Note the required `MCP-Protocol-Version` header.

`tests/GitlabMCP.Tests/` exists and is wired into `GitlabMCP.slnx` — nothing is discovered implicitly there, so any *new* test project needs the same explicit `<Project Path="..."/>` entry. It carries pure unit tests only (`ResolveProfile`/`ResolveVersion`, `Grant.IsVisibleIn`, `GitLabGraphQlEndpoint.Resolve` against gitlab.com/self-hosted/path-prefixed/non-standard-port bases) — no DI container, no live GitLab, nothing that needs a network. `tests/snapshots/<profile>.tools.txt` / `.prompts.txt` / `.resources.txt` / `.resource_templates.txt` are the DEC-014 golden lists, captured from a live `tools/list`/`prompts/list`/`resources/list`/`resources/templates/list` against each profile — a manual, not CI-automated, artifact (DEC-032).

**`global.json` pins the exact SDK** (`10.0.401`, `rollForward: latestFeature`) so a local build, the Docker build stage, and GitHub Actions all resolve the same toolchain.

## Native AOT rules

`PublishAot`, `SelfContained`, `PublishSingleFile` and `InvariantGlobalization` are all on in `GitlabMCP.csproj`. Consequences that bite:

- **No reflection-based JSON.** Every DTO crossing the MCP boundary needs a `JsonSerializerContext` source-generated entry, chained onto `McpJsonUtilities.DefaultOptions` and passed as the `serializerOptions` argument to `WithTools<T>()` / `WithPrompts<T>()`. Complex tool return types are where this breaks first.
- **No assembly scanning.** Use `WithTools<T>()`, `WithPrompts<T>()`, `WithResources<T>()`. Never `WithToolsFromAssembly()` / `WithPromptsFromAssembly()` / `WithResourcesFromAssembly()` — they reflect over the whole assembly and are not trim-safe.
- `InvariantGlobalization=true`: no culture-sensitive parsing/formatting anywhere.
- Adding a RID to `<RuntimeIdentifiers>` means committing to publishing it — each target is built separately.

## The GitLab.Client dependency

Package id is **`GitLab.Client`** (capital L) by AriusII, `net10.0`, itself AOT/trim-clean. Referenced from `GitlabMCP.Mapping`, `GitlabMCP.GraphQL` and `GitlabMCP.Tools` (Central Package Management — the version is pinned once in `Directory.Packages.props`).

- Registration: `services.AddGitLabClient(options => ...)` or `services.AddGitLabClient(configuration, "GitLab")` (source-generated config binder, AOT-safe). Returns an `IHttpClientBuilder`, so resilience handlers chain onto it.
- `GitLabClientOptions`: `BaseAddress` (defaults to `https://gitlab.com/api/v4/`, **trailing slash required**), `AccessToken`, `AuthenticationMode` (`PersonalAccessToken` → `PRIVATE-TOKEN`, `OAuthBearer`, `JobToken`), `UserAgent`, `Timeout`.
- **143 resource clients** in `GitLab.Client.Abstractions`, each reachable two ways: off the root `IGitLabClient` (`gitLab.Projects`) *and* independently injectable (`IProjectsClient`). Inject the narrow interfaces — it keeps the profile boundary visible in the constructor.
- Errors: hierarchy rooted at `GitLabApiException` — `GitLabAuthentication` (401), `Forbidden` (403), `NotFound` (404), `Conflict` (409), `Validation` (400/422, carries per-field errors), `RateLimitExceeded` (429, carries `RetryAfter`), `Server` (5xx). Transport failures stay as raw `HttpRequestException`; cancellation as `OperationCanceledException`. Mapping these onto MCP error responses is a cross-cutting concern — do it once, not per tool.
- Lists return `IAsyncEnumerable<T>` following `Link: rel="next"`. Tools must bound them (take/limit) rather than enumerating to completion — an unbounded enumeration is an unbounded token bill for the caller.
- Project/group route params are `ProjectId` / `GroupId` value types accepting either the numeric id or the URL-encoded `namespace/path`.

**Known gap, closed by DEC-011 = C:** there is no `IEpicsClient` or `IWorkItemsClient` in `GitLab.Client`. GitLab 19 deprecates the Epics REST API in favour of Work Items (GraphQL), and the library deliberately parks deprecated surface. Epic *sub-resources* do exist, on `INotesClient`, `IDiscussionsClient`, `IAwardEmojiClient` and `IResourceEventsClient` (`...ForEpicAsync`), but epic/roadmap tooling is built on a **separate hand-rolled GraphQL path** against `/api/graphql` (`src/GitlabMCP.GraphQL/`, DEC-020/DEC-021) — see `DECISIONS.md`. Epic tools are in scope for the Maintainer and Developer profiles.

## Target architecture: profiles

A profile is selected at server startup and determines **which tools are advertised at all** — filtering happens before `tools/list`, not inside tool bodies. A tool the profile does not grant must not appear in the listing. The four profiles:

| Profile | Persona | Scope |
|---|---|---|
| `FullPermission` | — | Every tool from every profile, plus instance administration. Superset, not a separate surface. |
| `Maintainer` | Product Owner / Product Manager | Planning: ideas, roadmap, milestones, epics, issues, tasks. Read-mostly on code. |
| `Developer` | Developer | MR/PR lifecycle, epics, issues, tasks, and the code, group and project management that supports them. |
| `DevOps` | Platform / SRE | Administration plus settings, CI/CD, runners, Terraform, and the surrounding infrastructure. |

Indicative mapping onto `GitLab.Client` resource clients — refine as tools land, but keep the profile boundary explicit and data-driven rather than scattered across tool classes:

- **Maintainer** — `Issues`, `Milestones`, `Iterations`, `Labels`, `Boards`, `Notes`, `Discussions`, `ResourceEvents`, `Todos`, `AwardEmoji`, `Wikis`, `Search`, `Analytics`, `Members` (read).
- **Developer** — the above plus `MergeRequests`, `MergeRequestApprovals`, `ApprovalRules`, `DraftNotes`, `Suggestions`, `Branches`, `Commits`, `Tags`, `Repositories`, `RepositoryFiles`, `Projects`, `Groups`, `Snippets`, `Pipelines`/`Jobs` (read), `CodeSearch`.
- **DevOps** — `Runners`, `RunnerControllers`, `Pipelines`, `PipelineSchedules`, `Triggers`, `Variables`, `Environments`, `Deployments`, `ProtectedBranches`/`ProtectedTags`/`ProtectedEnvironments`, `FreezePeriods`, `TerraformStates`, `ClusterAgents`, `ContainerRegistry`, `Packages*`, `FeatureFlags`, `SecureFiles`, `JobTokenScope`, `CiLint`, `CiCatalog`, `Integrations`, `ProjectHooks`/`GroupHooks`/`SystemHooks`, `DeployKeys`/`DeployTokens`, `Instance`, `Licenses`, `AuditEvents`, `ServiceAccounts`, `Users`.

Design points, now decided — see `DECISIONS.md` for the reasoning behind each:

- **Profile source (DEC-004 = A):** resolved once at startup from configuration key `GitLabMcp:Profile` (env `GitLabMcp__Profile`). No per-request header, no `IOptionsMonitor` re-read. Tool registration stays static (`WithTools<T>()`); the profile gate runs once at startup over the built `ToolCollection`/`PromptCollection`. (A per-request profile *would have been* possible over plain static registration in `HttpServerSessionMode.Stateless` — confirmed at runtime — but it was not the option taken.)
- The GitLab token's own permissions are the real ceiling. A profile narrows the surface; it cannot widen it. A `DevOps` profile on a Developer-scoped PAT will still get 403s — surface that clearly instead of letting it look like a tool bug.
- Each profile should ship its own `McpServerOptions.ServerInstructions` and its own `[McpServerPrompt]` set, so the client is told how to use *that* surface rather than the union of all four.
- **`FullPermission` (DEC-005):** a union, `Grant.Full = Maintainer | Developer | DevOps | AdminOnly` — not a fourth hand-maintained list.
- **Read-only variant (DEC-006):** an orthogonal `bool readOnly` modifier filtered on `ReadOnlyHint`, not extra enum members.

## Tool authoring conventions

- Tool classes live under `Tools/`, marked `[McpServerToolType]`, methods `[McpServerTool]` with a `[Description]` on the method **and every parameter** — these descriptions are the entire contract the model sees, so write them for a caller who cannot read the GitLab docs.
- Prefer instance methods with constructor-injected `I*Client` interfaces; the SDK constructs an instance per invocation from DI.
- HTTP transport runs `Stateless = true` (`Program.cs`). In 2.2.0 that boolean is a convenience proxy over the three-valued `HttpServerTransportOptions.SessionMode` (`Stateless` = default, `Stateful`, `StatefulForInitializeClients`) — both write the same underlying value, so the last assignment wins if you set both. Keep stateless unless sampling/elicitation is actually needed: it is what makes horizontal scaling in Docker trivial, and per-request profile gating depends on it.

## Docker

A multi-stage `Dockerfile` and `.dockerignore` exist at the root and have been built and run end to end for both Linux RIDs. **`docker-aot-image` owns them** — read it before editing either.

The rule that governs everything: the AOT publish must happen *inside* a build stage matching the target libc (`linux-musl-x64` → Alpine, `linux-x64` → glibc), because a Native AOT binary is linked against one and will not exec on the other. The published output is a single self-contained executable needing no .NET runtime in the final stage. The server binds port 9080, must bind `0.0.0.0` inside a container, and the GitLab token arrives by environment or mounted secret — never an `ARG`, which is visible in image history.

`ARG APP_VERSION` (default `0.0.0-dev`) is the one non-secret exception — it flows into `dotnet publish -p:Version=... -p:InformationalVersion=...` and is re-exported as `ENV GitlabMcp__Version`, purely a label the `server_info` resource and the startup log line report. It drives no build behaviour and is safe to bake in because it is not sensitive. `compose.yaml` at the repo root is a ready-to-edit example container (profile, GitLab target, token-as-secret).

## CI/CD (DEC-032)

Two GitHub Actions workflows, both `.github/workflows/*.yml`:

- **`ci.yml`** — every push/PR to `main`: `dotnet build`+`dotnet test` in Release, then a `docker build` (`linux/amd64`, no push) — that build stage's `dotnet publish` for `linux-musl-x64` is the real Native AOT gate, so every PR exercises it. Needs no secrets; runs the same on a fork's PR.
- **`release.yml`** — push a tag `vX.Y.Z`: re-runs build+test, then builds and pushes the image to Docker Hub as `<user>/gitlabmcp:X.Y.Z-amd64`, `:X.Y.Z`, `:latest-amd64`, `:latest` (single linux/amd64 image, `provenance: false` — never a multi-arch manifest, arm64 stays out of scope), then publishes a GitHub Release. Needs repository secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a Docker Hub PAT, Read & Write) — README.md's "Required repository secrets" is the authoritative list.

Neither workflow runs the `aot-publish-gate` skill's Windows-host `win-x64` publish — that is a local/dev-box gate the skill's own agent (`aot-gatekeeper`) owns; CI's AOT coverage is the Linux/musl publish inside the Docker build.
