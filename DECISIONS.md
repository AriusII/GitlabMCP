# DECISIONS.md

The repo's decision log. It exists because the design is spread across eleven files under `.claude/`
plus `CLAUDE.md`, and a decision that touches two of them has no home in either — which is how the
`2.1.0` → `2.2.0` stamp drifted across five files, and how the tool return-type contract ended up
stated two ways at once. **A decision recorded only inside the skill that happens to mention it is not
recorded.** It goes here, and the skill keeps the mechanism.

This file is the destination `mcp-profile-gating` §Open Decisions and `gitlab-mcp-tool-builder`
§Step 8 already name.

## Format

One `###` entry per decision. Seven fields, in this order, no others:

```markdown
### DEC-0NN · <one-line title>

- **Status** DECIDED | OPEN | SUPERSEDED · **Date** YYYY-MM-DD · **Owner file** `<path>` §<section>
- **Decision.** What was chosen. One sentence where possible.
- **Context.** Why the question existed and what the alternatives were.
- **Consequences.** What is now true, including what it costs.
- **Closes when.** *(OPEN only)* The specific observation or artifact that would flip it to DECIDED.
```

Rules:

- **Status** may carry the suffix `— UNRATIFIED`: an agent took the decision without an owner sign-off.
  Those are live in the tree and need confirming, not re-litigating from scratch.
- **Date** is the date this entry was written or last changed, not necessarily the date the decision was
  taken. Every entry below was seeded on **2026-09-09**. Where the decision itself predates the log and
  its date is unrecorded, the entry says so rather than guessing one.
- **Owner file** is the artifact that carries the *mechanism*. This log carries the *choice*. If those two
  ever disagree, the owner file is wrong about the choice and this file is wrong about the mechanism.
- **Append, never rewrite or reorder.** Superseding an entry means adding a new one and editing only the
  old one's **Status** line to `SUPERSEDED` with a pointer.
- Ids are `DEC-0NN`, allocated in order. `mcp-profile-gating`'s own `D1`/`D2`/`D3` labels are a separate
  numbering that predates this file; the entries below name the correspondence explicitly.

`gitlab-mcp-tool-builder` §Step 8 prescribes a shorter append shape for tool-building agents
(`## <date> — <wire name>` + a bullet). Those land under [Agent appends](#agent-appends) at the bottom
and are promoted to a numbered entry by whoever reviews the change. That divergence is known and
deliberate: an agent mid-task should not have to fill seven fields to record that it took a default.

## Index

| Id | Status | Decision | Owner file |
|---|---|---|---|
| [DEC-001](#dec-001--target-rids-are-x64-only-macos-is-not-a-target) | DECIDED | x64-only RID set; macOS dropped | `CLAUDE.md` |
| [DEC-002](#dec-002--modelcontextprotocolaspnetcore-210--220) | DECIDED — UNRATIFIED | SDK bumped to 2.2.0 | `GitlabMCP/GitlabMCP.csproj` |
| [DEC-003](#dec-003--the-http-transport-stays-in-httpserversessionmodestateless) | DECIDED | Transport stays `Stateless` | `mcp-profile-gating` §D1 |
| [DEC-004](#dec-004--where-the-active-profile-comes-from-d1) | DECIDED | Profile source: **A**, config at startup | `mcp-profile-gating` §D1 |
| [DEC-005](#dec-005--fullpermission-is-a-union-not-a-fourth-list-d2) | DECIDED | `FullPermission` = union + `AdminOnly` bit | `mcp-profile-gating` §D2 |
| [DEC-006](#dec-006--how-a-read-only-variant-is-expressed-d3) | DECIDED | Read-only as orthogonal modifier | `mcp-profile-gating` §D3 |
| [DEC-007](#dec-007--a-tool-declares-taskcalltoolresult-when-any-payload-string-came-from-gitlab) | DECIDED | `Task<CallToolResult>` + `Wrap` | `mcp-tool-authoring` §Step 4 |
| [DEC-008](#dec-008--usestructuredcontent-and-outputschematype-are-absent-from-every-tool) | DECIDED | Neither flag is ever set | `mcp-untrusted-content` §`UseStructuredContent`… |
| [DEC-009](#dec-009--one-jsonserializeroptions-instance-gitlabjsonoptions) | DECIDED | `GitLabJson.Options`, one instance | `mcp-untrusted-content` §Step 1 |
| [DEC-010](#dec-010--one-provenance-envelope-per-result-not-per-field) | DECIDED | Envelope granularity: per result | `mcp-untrusted-content` §Open decision… |
| [DEC-011](#dec-011--epics-drop-comment-only-or-graphql) | DECIDED | Epics: **C**, a separate GraphQL path | `gitlab-client-navigation` §The Epics gap |
| [DEC-012](#dec-012--configuration-key-namespace-and-provider-precedence) | DECIDED | `GitLab:*` / `GitLabMcp:*`, unprefixed env | `docker-aot-image` §6 |
| [DEC-013](#dec-013--how-the-app-reads-runsecrets-tier-2) | DECIDED | `AddKeyPerFile`, verified at the AOT gate | `docker-aot-image` §6 |
| [DEC-014](#dec-014--golden-per-profile-tool-lists-live-in-testssnapshots) | DECIDED | `tests/snapshots/*.txt` | `mcp-profile-gating` §Tests that must exist |
| [DEC-015](#dec-015--single--vs-multi-tenant) | DECIDED | Single-tenant | `mcp-untrusted-content` §Step 5 |
| [DEC-016](#dec-016--aspnetcore_allowedhosts-is-set-in-the-image) | DECIDED — UNRATIFIED | Host allowlist baked into the image | `docker-aot-image` §5 |
| [DEC-017](#dec-017--a-per-request-profile-requires-dynamic-tool-registration) | SUPERSEDED | — superseded by DEC-004 | `CLAUDE.md` |
| [DEC-018](#dec-018--the-solution-is-six-projects-under-src-plus-tests) | DECIDED | Six projects under `src/` | `docs/architecture.md` |
| [DEC-019](#dec-019--the-tool-surface-is-exhaustive-not-curated-small) | DECIDED | Exhaustive tool surface (150+) | `docs/tool-catalog.md` |
| [DEC-020](#dec-020--the-graphql-path-is-hand-rolled-not-a-graphql-client-library) | DECIDED | Hand-rolled GraphQL client | `src/GitlabMCP.GraphQL/` |
| [DEC-021](#dec-021--graphql-error-message-is-never-relayed-verbatim-ratifies-dec-020) | DECIDED | GraphQL client design ratified; `Message` never relayed raw | `src/GitlabMCP.GraphQL/` |
| [DEC-022](#dec-022--wire-naming-convention-gitlab_verb_noun) | DECIDED | Wire name convention: `gitlab_<verb>_<noun>` | `docs/tool-catalog.md` |
| [DEC-023](#dec-023--resource-level-profile-gating-resourcegrants) | DECIDED | `ResourceGrants` exists from the first resource | `mcp-profile-gating` §Resource gating |
| [DEC-024](#dec-024--profilecatalog-is-partitioned-by-domain-partial-fragments) | DECIDED | `ProfileCatalog` split via `partial` per-domain fragments | `src/GitlabMCP.Profiles/` |
| [DEC-025](#dec-025--the-722-tool-catalog-is-cut-to-a-180-tool-implemented-slice-plus-a-documented-backlog) | DECIDED | 722-tool catalog cut to ~180 implemented + backlog | `docs/tool-catalog.md` |
| [DEC-026](#dec-026--each-tool-domain-gets-its-own-jsonserializercontext-class-not-a-shared-partial-fragment) | DECIDED | Per-domain JsonSerializerContext classes | `src/GitlabMCP.Contracts/Serialization/` |
| [DEC-027](#dec-027--grantadminonly-is-never-combined-with-a-persona-bit-on-the-same-catalog-row) | DECIDED | `Grant.AdminOnly` used bare, never combined | `src/GitlabMCP.Profiles/` |

---

## Entries

### DEC-001 · Target RIDs are x64-only; macOS is not a target

- **Status** DECIDED · **Date** 2026-09-09 (decision itself predates this log; its date is unrecorded)
  · **Owner file** `CLAUDE.md` §Commands, `.claude/skills/aot-publish-gate/SKILL.md` §The RID matrix
- **Decision.** `<RuntimeIdentifiers>` is exactly `win-x64;linux-x64;linux-musl-x64`. `win-arm64`,
  `linux-arm64` and `osx-arm64` were removed, and with `osx-arm64` went macOS as a target. No arm64 RID,
  arm64 base image, `--platform` flag, multi-arch manifest or QEMU stage, in any file, ever — including
  as a "for completeness" aside.
- **Context.** Each RID in the list is a commitment to publish it: Native AOT builds every target
  separately, and the container gate has to run per libc flavour. Three x64 targets already cost two
  ILCompiler runs in CI plus one on Windows.
- **Consequences.** A macOS developer cannot produce a native local binary and must use the container.
  The rule is restated as a prohibition in six artifacts (`aot-publish-gate`, `docker-aot-image`,
  `aot-gatekeeper`, `gitlab-mcp-reviewer`, `gitlab-mcp-tool-builder`, `CLAUDE.md`) because agents
  reintroduce arm64 helpfully and unprompted.

### DEC-002 · `ModelContextProtocol.AspNetCore` 2.1.0 → 2.2.0

- **Status** DECIDED — UNRATIFIED · **Date** 2026-09-09 · **Owner file** `GitlabMCP/GitlabMCP.csproj`
- **Decision.** The single `PackageReference` is `ModelContextProtocol.AspNetCore` **2.2.0**. 2.2.0 is
  the version of record; every artifact is stamped and verified against it.
- **Context.** An agent performed the bump as a side effect of another task, without an owner asking for
  it. That is outside any agent's mandate here — the csproj is edited only via `dotnet add package`, for
  a package the task named. It is recorded rather than reverted because the tree is clean on it: the
  build produces zero warnings, the `win-x64` Native AOT publish succeeds with zero IL2xxx/IL3xxx, and
  the delta is provably confined to transport configuration.
- **Consequences.**
  - The public API delta is **6 members added, 0 removed**, all in `ModelContextProtocol.AspNetCore`:
    the type `HttpServerSessionMode` with fields `Stateless` / `Stateful` /
    `StatefulForInitializeClients`, the property `HttpServerTransportOptions.SessionMode`, and the
    internal `StreamableHttpHandler.IsStatelessOnly`. Diffed from the two XML docs; both versions are
    still in the NuGet cache.
  - `ModelContextProtocol.Core.xml` is **byte-identical** between the two versions
    (md5 `e8cc4416c32e4747d44838110bed1a14`), so no tool-, prompt-, resource- or JSON-authoring claim in
    any skill changed. Only the version stamp was wrong, and it has been corrected everywhere: no stale
    `2.1.0` stamp remains under `.claude/` or in `CLAUDE.md`, and the three surviving mentions of `2.1.0`
    are deliberate claims about *both* versions.
  - `Program.cs`'s `options.Stateless = true` still compiles and still means exactly the same thing —
    see DEC-003.
  - **There is no commit to diff.** `git log` holds one commit (`a99a774`) containing only `.gitignore`,
    `LICENSE` and `README.md`; `GitlabMCP/` is untracked. No commit records either version, so the
    working tree is the only record of the bump and this entry is the only record of who made it.
- **Ratify by:** the owner confirming 2.2.0 is wanted, or asking for a revert to 2.1.0 — in which case
  every `2.2.0` stamp and all the `HttpServerSessionMode` material (`mcp-profile-gating` §D1,
  `mcp-server-smoke-test` §`HttpServerSessionMode` — the three modes) has to come back out.

### DEC-003 · The HTTP transport stays in `HttpServerSessionMode.Stateless`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-profile-gating/SKILL.md` §D1; security half in
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Step 5
- **Decision.** `Program.cs` keeps `options.Stateless = true` and does not also assign `SessionMode`.
  `Stateful` and `StatefulForInitializeClients` are both rejected.
- **Context.** 2.2.0 (DEC-002) replaced the boolean with a three-valued property. The 2.2.0 XML
  documents `Stateless` as *"a convenience proxy over `SessionMode`"* whose assignment of `true` selects
  `HttpServerSessionMode.Stateless` exactly, and warns that *"because both properties update the same
  underlying value, the last assignment wins when both are configured"* — so assigning both is how you
  get a mode nobody chose.
- **Consequences.**
  - Horizontal scaling in Docker needs no session affinity, which was the original reason.
  - It is also a security property: the MCP spec forbids using sessions for authentication, and
    stateless sidesteps session hijacking. That argument attaches to the **mode**, not to the property.
  - Sampling, elicitation and roots are unavailable — the server cannot make server-to-client requests.
    If one is ever needed, prefer MRTR over giving up the mode.
  - `Stateless` is a two-valued view of a three-valued property: it reads `false` for *both* other modes.
    Any code or diagnostic that needs to know the mode must read `SessionMode`, not the boolean.
  - DEC-004 option C is a correctness claim about this mode specifically, so changing this entry
    invalidates that one.

### DEC-004 · Where the active profile comes from (D1)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-profile-gating/SKILL.md` §D1
- **Decision.** **A.** The active profile is resolved **once at startup** from configuration key
  `GitLabMcp:Profile` (environment `GitLabMcp__Profile=DevOps`), alongside the two other values the
  operator supplies the same way: `GitLab:BaseAddress` and `GitLab:AccessToken`. **B** and **C** are
  both rejected. There is no `X-GitLab-Profile` header, no `IOptionsMonitor` re-read, and no per-request
  profile resolution of any kind — `ConfigureSessionOptions` does **not** read the profile.
  Rejected candidates, retained for the record:
  **B.** config re-read per request via `IOptionsMonitor`;
  **C.** a per-request header (`X-GitLab-Profile`) read inside `ConfigureSessionOptions`.
- **Context.** A is the only option that gives per-container least privilege, because a container with
  one profile can carry a token scoped to match it. B flips profile without a restart but leaves a
  connected client's cached `tools/list` stale, and in stateless mode there is no
  `notifications/tools/list_changed` to correct it — a tool silently becomes `-32602` mid-conversation.
  C serves several personas from one deployment, at the cost of making the profile caller-controlled and
  forcing one process to hold a token that is the union of every profile it serves.
- **Consequences.** Blocking: `Program.cs` tool registration cannot be written until this is settled, and
  `docker-aot-image` §7 has already committed the container half to A's shape
  (`-e GitLabMcp__Profile=…`, one profile per container, no per-profile image). Taking B or C means
  revisiting that section. If C is taken, it is valid **only** in `HttpServerSessionMode.Stateless`
  (DEC-003) and **only** behind authentication that binds the header to `http.User`; the union-token
  consequence must be recorded here in the same change.
- **Taken because.** The owner asked, in as many words, to be able to supply *the GitLab server URL, the
  PAT, and the profile wanted*. That is the operator-supplied triple A describes, and it is the only
  option that keeps per-container least privilege: one container, one profile, one token scoped to match
  it. It also leaves `docker-aot-image` §7 correct as written rather than needing revision.
- **Now settled.** Tool registration is static — `WithTools<T>(GitLabJson.Options)` for every tool type
  — and the gate runs **once**, at startup, over the built `ToolCollection` / `PromptCollection`.
  `ProfileGate.AssertCatalogIsComplete` runs in the same startup path and fails fast. An **absent**
  `GitLabMcp:Profile` still falls back to `Maintainer` per DEC-012; a **present but unparseable** value
  throws before the server listens.
- **What this does not settle.** DEC-015 (tenancy) is untouched: A is single-tenant-shaped, and choosing
  A does not by itself answer whether the server may ever listen off loopback.

### DEC-005 · `FullPermission` is a union, not a fourth list (D2)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-profile-gating/SKILL.md` §D2
- **Decision.** `Grant.Full = Maintainer | Developer | DevOps | AdminOnly`, where `AdminOnly` is one extra
  bit carrying the instance-administration tools that belong to no persona. The recommendation is taken
  as-is; no fourth hand-maintained list.
- **Context.** The alternative is a fourth hand-maintained list, which diverges from the other three
  within a few commits and stays invisible until someone diffs two `tools/list` outputs.
- **Consequences.** Under the union, `FullPermission` advertises the entire surface. With 143 resource
  clients behind it that is a large `tools/list` and a real context cost, so it is an administrative and
  debugging profile, not the default (DEC-019, hard rule 61). Note the honest gap: tool-list size is
  *widely reported* to
  degrade tool selection, and nothing in this repo has measured it.
- **Closes when.** `Profiles/McpProfile.cs` exists with a `Grant` flags enum, and DEC-014's golden list
  for `FullPermission` is committed — the diff of that file is the decision made visible.

### DEC-006 · How a read-only variant is expressed (D3)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-profile-gating/SKILL.md` §D3
- **Decision.** An **orthogonal modifier** — a `bool readOnly` alongside the profile; after the profile
  pass, remove any tool whose `ProtocolTool.Annotations?.ReadOnlyHint` is not `true`. Rejected: eight enum
  members, and two bits per persona in `Grant`.
- **Context.** The read/write axis is orthogonal to persona, so encoding it in the persona enum is a
  category error that doubles the enum and every `switch`; two bits per persona doubles the catalog's
  vocabulary instead.
- **Consequences.** The modifier leans on an annotation the server authors, which is fine — the MCP
  spec's warning about annotations is about consuming *someone else's*. But it is only honest if a
  startup assertion keeps it so: every tool the catalog marks a writer must have `ReadOnlyHint != true`
  and vice versa. Without that assertion the flag is decorative. Watch the SDK's asymmetric defaults:
  `ReadOnly` defaults to `false`, `Destructive` to `true`, and an unset hint is omitted from the wire —
  so a read tool that forgets `ReadOnly = true` is indistinguishable from a destructive one and is
  invisible to a read-only profile.
- **Now settled.** The `readOnly` pass and its paired startup assertion live in `GitlabMCP.Profiles`:
  `ProfileGate.AssertCatalogIsComplete` additionally asserts every tool's `readOnly` catalog flag agrees
  with its registered `ReadOnlyHint` (both true, both not-true — never one without the other), closing the
  gap `gitlab-mcp-reviewer` §B6 named (a `Grant` value or hint mismatch reaching no check at all).
  DEC-014's golden lists cover a read-only profile once one is exercised.

### DEC-007 · A tool declares `Task<CallToolResult>` when any payload string came from GitLab

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-tool-authoring/SKILL.md` §Step 4 (the projection record);
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Step 1 (the envelope it is wrapped in)
- **Decision.** A `[McpServerTool]` method declares `Task<CallToolResult>` and returns
  `GitLabContent.Wrap(payload, source)` — or `WrapText` for a raw trace, blob or diff — **iff any
  *string* in its payload originated from GitLab.** Only when every field is a non-string scalar the
  server or GitLab produced as a number, bool, date or enum code does the method declare the bare
  projection record `Task<TResult>`. The projection record is mandatory either way: under the wrapped
  form it stops being the return type and becomes the `Wrap<T>` payload, and it still needs its
  `[JsonSerializable]` entry. One carve-out: the call-tool filter's own curated `CallToolResult` is
  server-authored and must **not** be wrapped.
- **Context.** Two skills taught this two ways — `mcp-tool-authoring` §Step 4 said "return a small record
  you own", `mcp-untrusted-content` said the wrapped form "supersedes" it — and the trade-off both
  assumed turned out not to exist. Measured on 2.2.0 over the wire, a bare record with
  `UseStructuredContent` omitted and a `CallToolResult` publish **the same** `tools/list` schema (none)
  and the same `tools/call` shape minus the envelope. So *"`CallToolResult` loses your output schema"*
  is false as this repo is configured; the envelope is free on the schema axis and buys the only
  greppable provenance boundary. The matrix is recorded in `mcp-tool-authoring` §What that return type
  costs — measured, not assumed (four rows) and `mcp-untrusted-content`
  §`UseStructuredContent` and `OutputSchemaType` are absent from every tool here (five rows).
- **Consequences.**
  - The string-valued test replaces "nearly all of them" as the rule, and is greppable in review:
    `gitlab-mcp-reviewer` §D0 is 🔴 on a payload with a GitLab string behind a bare return type.
  - **The missing-`[JsonSerializable]` failure moved from startup to first call.** A record return type
    still kills the process while `WithTools<T>` builds the tool. A type reached only through
    `Wrap<T>` never reaches schema generation: the server starts, `tools/list` lists everything, and the
    `NotSupportedException` lands on the **first `tools/call` of that one tool**, redacted on the wire to
    `An error occurred invoking 'gitlab_x'.` with the type name only in the server log. So
    **`tools/call` every tool before shipping** — a clean `tools/list` is no longer evidence. This is the
    single price of the contract, and it is why `mcp-server-smoke-test` now makes calling every advertised
    tool a mandatory step.
  - Supersedes the bare-record-return-type instruction that `mcp-tool-authoring` §Step 4 carried, and the
    "supersedes … for nearly all of them" framing that `mcp-untrusted-content` carried. Both files now
    teach this rule directly; neither defers to the other for it.

### DEC-008 · `UseStructuredContent` and `OutputSchemaType` are absent from every tool

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-untrusted-content/SKILL.md`
  §`UseStructuredContent` and `OutputSchemaType` are absent from every tool here
- **Decision.** Neither is ever set. The SDK default for `UseStructuredContent` is `false`; leave it.
- **Context.** On a `CallToolResult` tool the flag is not inert — it publishes a schema of the
  *envelope* (`content` / `structuredContent` / `isError` / `_meta` / `resultType`) and then emits no
  `structuredContent` to match it, at **zero build warnings**. `OutputSchemaType` is the SDK's own
  documented remedy for exactly this case, and it does publish the payload's real schema — but the tool
  still returns no `structuredContent`, so the server advertises structured output it never delivers.
  The only way to honour that promise is to fill `CallToolResult.StructuredContent` in the wrapper,
  which puts the whole GitLab payload on the wire a second time, bare, **outside the nonce envelope and
  outside the preamble** — strictly worse than setting neither, for the exact threat the envelope exists
  to bound.
- **Consequences.** No tool in this server publishes an `outputSchema`. That is the accepted cost of
  DEC-007 and it is not a regression, because the bare-record path as this repo writes it publishes none
  either. `gitlab-mcp-reviewer` §D7 is 🔴 on either attribute. Because the flag produces no build
  warning, the smoke test asserts its absence on the wire instead: no `outputSchema` in `tools/list`, no
  `structuredContent` in `tools/call`.

### DEC-009 · One `JsonSerializerOptions` instance: `GitLabJson.Options`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Step 1 (the source);
  `.claude/skills/mcp-tool-authoring/SKILL.md` §Step 6 (registration)
- **Decision.** `GitlabMCP/Serialization/GitLabJson.cs` holds
  `internal static class GitLabJson { public static JsonSerializerOptions Options { get; } }`, built as a
  copy of `McpJsonUtilities.DefaultOptions` with `GitlabMcpJsonContext.Default` inserted at chain
  position 0, then `MakeReadOnly()`. It is the single symbol passed to every `WithTools<T>()` and
  `WithPrompts<T>()` call and the single symbol `GitLabContent.Wrap<T>` serializes through, via
  `McpJsonUtilities.GetTypeInfo<T>(GitLabJson.Options)`. No local `var toolJson` in `Program.cs`, and no
  second `JsonSerializerOptions` instance anywhere.
- **Context.** `mcp-tool-authoring` §Step 6 previously built the chained options as a local in
  `Program.cs`, which `Tools/GitLabContent.cs` cannot reach — following both skills literally gave you
  either a compile error or two option instances.
- **Consequences.** Two instances are not a cosmetic problem: serializing through a source-generated
  context's own `Default` (`Context.Default.<T>`) compiles, is AOT-clean, and silently emits
  **PascalCase** (`{"Issues":[{"Iid":7,…`) where the SDK's marshalling emits camelCase
  (`{"issues":[{"iid":7,…`) — same payload, same build, same process. Nothing in the build catches it.
  `gitlab-mcp-reviewer` §A1 is 🔴 on the `Context.Default.<T>` branch for this reason. `MakeReadOnly()`
  does not break `WithTools<T>(options)`, under JIT or under the `win-x64` Native AOT binary.

### DEC-010 · One provenance envelope per result, not per field

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Open decision: where the envelope goes
- **Decision.** One `<gitlab-data nonce="…" source="…">` envelope per `tools/call` result: one nonce, one
  preamble, one grep target, and the payload stays clean JSON. Not per field, and not both.
- **Context.** Per-field wrapping buys field-granularity provenance and pays in markup inside JSON string
  values, a nonce per field, and token count. Doing both turns the preamble into boilerplate nobody
  reads. Per-result wins because nearly every tool returns a homogeneous GitLab payload.
- **Consequences.** Provenance is stated at result granularity. The escape hatch for the mixed case is
  structural, not markup: **if a tool mixes server-computed facts with GitLab text in one record, split
  it into two content blocks** rather than wrapping fields. Its owner section is still headed *"Open
  decision"* although its body settles the question — the heading is stale, this entry is the status.

### DEC-011 · Epics: drop, comment-only, or GraphQL

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/gitlab-client-navigation/SKILL.md` §The Epics gap; the mechanism now lives in
  `src/GitlabMCP.GraphQL/` and `docs/architecture.md` §The GraphQL path
- **Decision.** **C.** Epics and work items are served by a **separate GraphQL path** against
  `/api/graphql`, in its own project, beside the REST `GitLab.Client`. Epic-shaped tools are unblocked
  and will be written. Rejected: **A.** Drop epics; build planning tools on `IIssuesClient` +
  `IMilestonesClient` + `IIterationsClient` + `IBoardsClient`. **B.** Ship the comment-only epic surface —
  the 26 sub-resource methods on `IAwardEmojiClient`, `IDiscussionsClient`, `INotesClient`,
  `IResourceEventsClient`. **C.** A separate GraphQL path against `/api/graphql`.
- **Context.** `GitLab.Client` has no `IEpicsClient`, no `IWorkItemsClient`, and no type anywhere in the
  assembly with `Epic` or `WorkItem` in its name — you cannot read, create, update, delete, list or
  search an epic with it. The library parks the surface deliberately: GitLab 19 deprecates the Epics REST
  API in favour of Work Items over GraphQL. Meanwhile `CLAUDE.md` lists epics under **both** the
  Maintainer and Developer profiles, and nothing satisfies that today.
- **Consequences.** A means amending the two profile tables in `CLAUDE.md` rather than leaving a tool
  that cannot exist. B is only defensible *alongside* A: the caller must already know the `epicIid`,
  because the server cannot discover, list or search epics, so B is unusable as an entry point. C is a
  project, not an afternoon — a second HTTP client and auth path, hand-written DTOs each needing a
  `[JsonSerializable]` entry, no `GitLabApiException` mapping to inherit, and the endpoint sits *beside*
  the REST root, so it cannot be reached by resolving a relative URI against
  `GitLabClientOptions.BaseAddress` (`https://gitlab.com/api/v4/` + `graphql` yields a 404 that reads
  like a permissions problem).
- **Taken because.** The owner named C and asked for the maximal surface. GitLab 19's direction is Work
  Items over GraphQL, so C is the only option that does not have to be redone; A and B both leave the
  Maintainer profile without the planning primitive its persona is defined by.
- **Now settled, and its price is accepted in full.** The four costs the Context paragraph names are now
  work items, not objections:
  1. A second `HttpClient` and auth path — registered off the same `IHttpClientFactory` and carrying the
     same token and resilience handler as the REST client.
  2. Hand-written DTOs, each needing a `[JsonSerializable]` entry in `GitlabMcpJsonContext`.
  3. No `GitLabApiException` mapping to inherit — GraphQL errors arrive as **HTTP 200 with a populated
     `errors` array**, so the GraphQL path needs its own detection and must funnel into the *same* MCP
     error shape the REST mapping produces. One error path for tools, two producers behind it.
  4. The endpoint sits **beside** the REST root: `/api/graphql`, not under `/api/v4/`. It is derived from
     `GitLabClientOptions.BaseAddress` by walking one segment up, and that derivation must hold for a
     self-hosted instance and for one under a path prefix. Naively appending `graphql` to the base
     address yields a 404 that reads like a permissions problem — see DEC-020.
- **Consequence for `CLAUDE.md`.** The two profile tables listing epics under Maintainer and Developer
  are now correct rather than aspirational, and the *Known gap* paragraph is superseded by this entry.

### DEC-012 · Configuration key namespace and provider precedence

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/docker-aot-image/SKILL.md` §6 (the key index, and where a value can come from)
- **Decision.** Five keys, and no `appsettings.json`: `GitLab:AccessToken`, `GitLab:BaseAddress`
  (bound by `AddGitLabClient(configuration, "GitLab")`), `GitLabMcp:Profile`, `AllowedHosts`,
  `Mcp:AllowedOrigins`. In a container they arrive as **unprefixed** environment variables with the `__`
  separator (`GitLab__AccessToken`), except `AllowedHosts`, which the image sets as
  `ASPNETCORE_ALLOWEDHOSTS` (DEC-016). Set one spelling per key, never both.
- **Context.** `WebApplication.CreateBuilder(args)` builds a provider chain in which **later wins**, and
  the dump recorded in the owner file puts the unprefixed environment provider *after* the
  `ASPNETCORE_`-prefixed one — so with both set, `AllowedHosts=x` beats `ASPNETCORE_ALLOWEDHOSTS=y`. The
  four JSON-file slots (`appsettings*.json`, `GitlabMCP.settings*.json`) do not exist in this repo, and
  adding one for the token would ship the secret as an image layer.
- **Consequences.**
  - **User secrets are not a container mechanism.** `secrets.json` joins the chain only under
    `ASPNETCORE_ENVIRONMENT=Development`; a container defaults to `Production`. The csproj's
    `UserSecretsId` is for `dotnet run` on the host and nothing else.
  - A runtime `-e` keeps the token out of the image but puts it in `docker inspect` on the host;
    `--env-file` is no different. Anyone with the docker socket reads it. That is what DEC-013 exists
    to improve.
  - Four of the five keys are **inert today** — no `GitLab.Client` reference, no `AddGitLabClient` call,
    no profile wiring, nothing reading `Mcp:AllowedOrigins`. Only `AllowedHosts` does anything, because
    `HostFilteringMiddleware` is already in the pipeline. The container half is nonetheless settled and
    does not change when the app halves land.
  - An **absent** `GitLabMcp:Profile` is specified to fall back to `Maintainer` (narrowest surface) and
    only a *present but unparseable* value throws. So an unset profile is silent, not loud — set it
    explicitly in every `docker run` and compose service. Making absence fail fast would be a change to
    `mcp-profile-gating`, not to the Dockerfile.

### DEC-013 · How the app reads `/run/secrets` (Tier 2)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/docker-aot-image/SKILL.md` §6 (✅ Right — the token arrives at run time)
- **Decision.** **A.** `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)` — a file
  named `GitLab__AccessToken` becomes the key `GitLab:AccessToken`. Rejected: **B.** An explicit
  `File.ReadAllText(path).Trim()` into `GitLabClientOptions.AccessToken`.
- **Context.** The container half of Tier 2 works today and is verified: compose mounts the secret at
  `/run/secrets/<name>`, readable by the app user, byte-exact. Nothing in `Program.cs` reads it. A ships
  in the ASP.NET Core shared framework (no `PackageReference` needed) and composes with the same
  `AddGitLabClient(configuration, "GitLab")` binding as Tier 1, and is appended *after* the environment
  provider, so a mounted file beats `-e` (measured). B is trivially AOT-safe and obviously correct but
  does not generalise to a second secret.
- **Consequences.** Either way the token must never reach a log line, a tool result or a diagnostic
  endpoint. Two traps that apply to both: write the file with `printf '%s'`, not `echo`, because the
  trailing newline survives into the `PRIVATE-TOKEN` header; and A's path must be **absolute** —
  `optional: true` does not save a relative path, which throws
  `ArgumentException: The path must be absolute. (Parameter 'root')` before the server starts.
- **Taken because.** A ships in the ASP.NET Core shared framework (no extra `PackageReference`) and
  composes with the same `AddGitLabClient(configuration, "GitLab")` binding as Tier 1; B does not
  generalise to a second secret and this repo already has more than one (token, and potentially a
  self-hosted CA bundle later).
- **Verification is not yet closed.** A has never been through a Native AOT publish before this build;
  its IL-cleanliness is confirmed or refuted the moment `aot-publish-gate` runs against the host project
  that calls it — treat a clean gate run as closing this entry's remaining open question, and a warning as
  cause to fall back to B without re-litigating the choice above.

### DEC-014 · Golden per-profile tool lists live in `tests/snapshots/`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-profile-gating/SKILL.md` §Tests that must exist
- **Decision.** Repo-root plain-text files: `tests/snapshots/<profile>.tools.txt` and
  `tests/snapshots/<profile>.prompts.txt`, one name per line, ordinal-sorted — the order
  `ProfileCatalog.ToolsFor` already emits, so regenerating one is mechanical. Not embedded resources
  inside a future test project, and not both.
- **Context.** `mcp-server-smoke-test` §Step 4 deliberately leaves the location open; `mcp-profile-gating`
  owns the golden lists, so it is settled there and recorded here.
- **Consequences.** The catalog test (L1), the in-process `tools/list` test (L2), the
  advertise-then-refuse test (L3) and the standalone transport scripts in `mcp-server-smoke-test` all
  assert against the *same bytes*. `tests/` sits outside `GitlabMCP/`, so nothing is swept into the Web
  SDK's content globs. The scripts work today with no test project; an embedded resource would fork the
  data the moment a script needed it. A future test project's only job is to get those files onto its
  output path. Do not straddle both.

### DEC-015 · Single- vs multi-tenant

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Step 5 (Loopback for local use, authenticated ingress
  for shared use)
- **Decision.** Single-tenant. One deployment, one GitLab identity, supplied by configuration
  (DEC-012/DEC-013). Rejected: multi-tenant, where the caller's identity would determine which GitLab
  credential is used.
- **Context.** It is listed as precondition 6 of the six that gate the server listening on anything but
  loopback, and it must be settled **before** per-request auth is written, because it decides what that
  code is allowed to do.
- **Consequences.** If multi-tenant: a design where the caller passes their own GitLab PAT through is the
  **token-passthrough anti-pattern the MCP spec forbids** — the answer has to be a credential the server
  holds and maps to the authenticated principal, not one the caller hands over. Multi-tenant also
  interacts with DEC-004 option C: a caller-controlled profile plus a caller-supplied credential is two
  compounding trust failures, not one. Single-tenant keeps DEC-004 option A's per-container
  least-privilege story intact and is the cheaper default.
- **Taken because.** DEC-004=A already committed to one profile, one container, one token — a
  single-tenant identity model is what makes that combination coherent rather than aspirational. The
  owner's own framing of the deployment ("give it the GitLab URL, the PAT, and the profile") describes
  configuring one identity for one running instance, not a broker serving several.
- **Consequences.** The token-passthrough anti-pattern the MCP spec forbids cannot arise by design — there
  is no caller-supplied-credential code path to write in the first place. This keeps DEC-004 option A's
  per-container least-privilege story intact. It remains true that the server must not bind off loopback
  without the rest of `mcp-untrusted-content` §Step 5's preconditions (`AllowedHosts`, an `Origin` check,
  authentication) — single-tenancy answers *whose* credential is used, not *whether* it is safe to expose
  the port.

### DEC-016 · `ASPNETCORE_ALLOWEDHOSTS` is set in the image

- **Status** DECIDED — UNRATIFIED · **Date** 2026-09-09 · **Owner file**
  `.claude/skills/docker-aot-image/SKILL.md` §3 and §5; policy in
  `.claude/skills/mcp-untrusted-content/SKILL.md` §Step 5
- **Decision.** The `Dockerfile` sets `ENV ASPNETCORE_ALLOWEDHOSTS="127.0.0.1;localhost;[::1]"`.
- **Context.** `HostFilteringMiddleware` is already in the pipeline and defaults to `*`, and this repo has
  no `appsettings.json`, so the image is the only place the key can be set. Left unset, the container
  accepted any `Host` header — a DNS-rebinding surface. All three loopback spellings are required:
  Kestrel binds `[::]`, so a genuine IPv6 loopback client sends `Host: [::1]:port` and the two-value
  form prescribed by `mcp-untrusted-content` returned `400` to it.
- **Consequences.** This is a live behaviour change, not future-proofing. A container reached under any
  other name — a compose service name, the bridge IP — now gets `400` where it previously got `200`, and
  the rejection is Kestrel's HTML *"Bad Request - Invalid Hostname"* page, **not** a JSON-RPC error
  object, so an MCP client sees a transport failure rather than a protocol one. The escape hatch is
  `docker run -e ASPNETCORE_ALLOWEDHOSTS="<name>;127.0.0.1"`; `-e` fully replaces the image `ENV`.
  Matching is on the host part only and case-insensitive — never include a port.
- **Ratify by:** the owner confirming loopback-only is the intended default for the image, or naming the
  hostnames a shared deployment needs.

### DEC-017 · A per-request profile requires dynamic tool registration

- **Status** SUPERSEDED by [DEC-004](#dec-004--where-the-active-profile-comes-from-d1) · **Date**
  2026-09-09 · **Owner file** `CLAUDE.md` §Target architecture: profiles
- **Decision.** *(Withdrawn.)* `CLAUDE.md` states: *"A per-request profile is incompatible with static
  `WithTools<T>()` registration — if profiles must vary per request, the tool collection has to be built
  dynamically."* That is wrong.
- **Context.** `HttpServerTransportOptions.ConfigureSessionOptions` is documented as running *"on every
  HTTP request because each request creates a fresh server context"* in `Stateless` mode, and it does:
  a probe against 2.2.0 produced a distinct `ToolCollection` per request, each pre-populated from the
  static registration, with `X-Profile`-driven removals that never leaked forward onto the next request.
  Nothing has to be built dynamically and no `ListTools` filter is involved.
- **Consequences.** Per-request gating is available over plain `WithTools<T>()` — which means DEC-004
  option C is a real option rather than an architectural impossibility, and its cost is the one DEC-004
  actually records (caller-controlled profile, union token), not a registration rewrite. The claim
  survives verbatim in `CLAUDE.md`; that file is edited under a separate mandate, and this entry is the
  correction of record until it is.

### DEC-018 · The solution is six class libraries plus the host — seven `src/*` projects, plus `tests/`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `docs/architecture.md`; MSBuild mechanism in
  `Directory.Build.props` / `Directory.Packages.props` / `GitlabMCP.slnx`
- **Decision.** The single `GitlabMCP/` project is split into six, under `src/`, with the host moving from
  `GitlabMCP/` to `src/GitlabMCP/`:

  | Project | Kind | References |
  |---|---|---|
  | `GitlabMCP.Abstractions` | classlib | — |
  | `GitlabMCP.Contracts` | classlib | Abstractions, `ModelContextProtocol.Core` |
  | `GitlabMCP.Mapping` | classlib | Contracts, `GitLab.Client` |
  | `GitlabMCP.GraphQL` | classlib | Contracts, Abstractions |
  | `GitlabMCP.Profiles` | classlib | Abstractions, `ModelContextProtocol.Core` |
  | `GitlabMCP.Tools` | classlib | Mapping, GraphQL, Contracts, Profiles |
  | `GitlabMCP` (host) | `Microsoft.NET.Sdk.Web` | all of the above, `ModelContextProtocol.AspNetCore` |

  **The count is seven, not six.** The "6 projects" phrasing this decision was requested under (and that
  `CLAUDE.md`'s prose may still carry) counts the six *class libraries*; `GitlabMCP.GraphQL` was already a
  row in this table from this entry's first draft — DEC-011=C/DEC-020 require it — so the honest total
  including the host is **seven `src/*` projects**. Any skill or doc that says "six projects" means these
  six libraries, not six-including-host; a doc that separately concludes "seven" (several downstream
  research reports did, independently) is not in conflict with this entry — it is counting the host too.
  Nothing folds `GitlabMCP.GraphQL` into `Mapping` or `Tools`: it stays its own project so its
  `JsonSerializerContext` partial files and its hand-rolled client (DEC-020) are reviewable in isolation
  from REST-shaped code.

  `tests/` sits outside `src/` and holds `tests/GitlabMCP.Tests/` and the `tests/snapshots/` golden lists
  DEC-014 already located there. Package versions are centralised
  (`Directory.Packages.props`, `ManagePackageVersionsCentrally`).
- **Context.** The owner asked for a real decomposition — folders, sub-folders, and the project references
  that make the seams enforced rather than advisory. A single project makes every boundary in this design
  a convention: nothing stops a tool class from reaching a `GitLab.Client` DTO directly and skipping the
  Mapping layer, and nothing stops the profile catalog from taking a dependency on a tool body. The
  alternatives considered were three projects (Core / Tools / Host) and one project with strict folders.
- **Consequences.**
  - **The compiler enforces the seams.** `GitlabMCP.Contracts` has no `GitLab.Client` reference, so a
    `GitLab.Client.Models` type physically cannot leak into an MCP payload record. `GitlabMCP.Profiles`
    has no reference to `GitlabMCP.Tools`, so the catalog is data about tool *names*, not about tool
    types.
  - **Every path in every skill that starts `GitlabMCP/` is now stale.** `GitlabMCP/Serialization/GitLabJson.cs`
    (DEC-009), `GitlabMCP/Program.cs` (DEC-003, DEC-013), `Tools/GitLabContent.cs` (DEC-009),
    `GitlabMCP/GitlabMCP.csproj` (DEC-001, DEC-002). The *rules* are unchanged; only their addresses move.
    The re-verify commands at the bottom of this file are updated in the same change.
  - **The `Dockerfile` build context changes.** It copies `GitlabMCP/GitlabMCP.csproj` and publishes it;
    it must now copy every `src/*/*.csproj` plus `Directory.*.props` for the restore layer, and publish
    `src/GitlabMCP/GitlabMCP.csproj`. `docker-aot-image` owns the file and is edited under its rules.
  - **Only the host publishes.** The five class libraries carry no `RuntimeIdentifiers` and no
    `PublishAot`; they set `IsAotCompatible`/`IsTrimmable` so IL2xxx/IL3xxx surface at *library* build
    time instead of only at host publish time. The AOT gate is unchanged in what it asserts.
  - `UserSecretsId 49084b7c-a0aa-4904-ab77-c4b617082b5b` moves with the host csproj and keeps working for
    `dotnet run` on the host machine. It remains irrelevant in a container (DEC-012).

### DEC-019 · The tool surface is exhaustive, not curated-small

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `docs/tool-catalog.md`; gating mechanism in
  `.claude/skills/mcp-profile-gating/SKILL.md`
- **Decision.** The server exposes an **exhaustive** tool surface — 150+ tools — spanning all four
  profiles and reaching into packages, registries, ML, imports and integrations, not only the central
  planning/code/CI cases. `GitLab.Client` 1.0.0 offers ~1800 methods across 143 resource clients; a tool
  per method is still explicitly excluded. Tools are designed around what a model would *call*, not
  around API coverage.
- **Context.** The owner asked for maximum coverage. The counter-argument is real and is recorded rather
  than dismissed: DEC-005 already notes that tool-list size is *widely reported* to degrade tool
  selection, and nothing in this repo has measured it.
- **Consequences.**
  - The risk lands almost entirely on `FullPermission`, which under DEC-005's union is the only profile
    that advertises everything at once. The three persona profiles each stay at a fraction of the total,
    which is the whole point of gating happening before `tools/list`.
  - `FullPermission` is therefore an administrative and debugging profile and **must not** be recommended
    as a default in any README, prompt, or `ServerInstructions` text.
  - DEC-007's price scales with the catalog: every tool must be `tools/call`-ed once before shipping,
    because a missing `[JsonSerializable]` entry surfaces on first call rather than at startup. With 150+
    tools that is a script, not a manual pass — `mcp-server-smoke-test` owns it.

### DEC-020 · The GraphQL path is hand-rolled, not a GraphQL client library

- **Status** DECIDED — UNRATIFIED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.GraphQL/`;
  research in `docs/architecture.md` §The GraphQL path
- **Decision.** DEC-011's GraphQL path is implemented as a small hand-written client in
  `GitlabMCP.GraphQL`: a typed request/response pair per operation, every DTO carrying a
  `[JsonSerializable]` entry, over an `HttpClient` registered off the same `IHttpClientFactory` as the
  REST client and carrying the same token and resilience handler. No `GraphQL.Client`, no StrawberryShake,
  no schema-codegen step in the build.
- **Context.** The constraint that decides it is `PublishAot` with zero tolerated IL2xxx/IL3xxx: a GraphQL
  library that serialises through reflection, or a code generator that emits reflection-based binding,
  fails the gate — and the gate is the repo's hardest rule. The GraphQL surface actually needed is narrow
  (work items: list, get, create, update, delete, hierarchy, notes), so the cost of hand-rolling is
  bounded in a way that "wrap all of GraphQL" would not be.
- **Consequences.**
  - The endpoint is derived from `GitLabClientOptions.BaseAddress` by walking one segment up from
    `/api/v4/` to `/api/graphql` — never by appending, which yields a 404 that reads like a permissions
    problem (DEC-011). The derivation must hold for `https://gitlab.com/api/v4/`, for a self-hosted
    `https://git.example.com/api/v4/`, and for a path-prefixed `https://example.com/gitlab/api/v4/`.
  - **HTTP 200 is not success.** A GraphQL error arrives as 200 with a populated `errors` array, and a
    mutation can additionally report failure inside its own payload's `errors` field. The client detects
    both and funnels them into the same MCP error shape the `GitLabApiException` mapping produces, so
    tools keep one error path.
  - Epics are a Premium/Ultimate feature. On a Free instance the query does not 403 — it returns nulls or
    a schema error — so the client must turn that into a message naming the tier, not a null payload.
  - Every GraphQL response DTO is a payload record under DEC-007's rule: its strings come from GitLab, so
    a tool returning one returns `Task<CallToolResult>` and wraps.
- **Ratify by:** the owner confirming a hand-rolled client is acceptable, or naming a library to evaluate
  against the AOT gate instead.

### DEC-021 · GraphQL error `Message` is never relayed verbatim; ratifies DEC-020

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.GraphQL/GitLabGraphQlClient.cs`,
  `GitLabGraphQlException.cs`; design research in `docs/architecture.md` §The GraphQL path
- **Decision.** DEC-020's hand-rolled design is **ratified as researched**: neither `GraphQL.Client` nor
  StrawberryShake is AOT-clean (the former's `GraphQLRequest : Dictionary<string, object>` shape is
  structurally reflection-based and its STJ serializer package has never targeted a TFM where `PublishAot`
  exists; the latter's own maintainer states, November 2025, that 51 of its 89 internal projects are still
  not AOT-compliant). On top of that ratification, one content-handling rule that has no other home:
  **`GraphQLError.Message` is always free text and is never concatenated into the plain `gitlab_*` error
  string a tool returns.** Only `GraphQLError.Path` (which schema field failed) may be relayed
  unconditionally; `Message` values that must reach the model at all go through `GitLabContent.WrapText`
  as GitLab-sourced data, never through the terminal error-mapping filter's bare string.
- **Context.** `GitLabValidationException.Errors` on the REST side has a safe/unsafe split — the
  dictionary's **keys** are a closed, GitLab-authored field-name vocabulary safe to relay raw, its
  **values** are free text and go through `GitLabContent`. GraphQL's `{ message, locations, path,
  extensions }` error shape has no equivalent split: `message` is uniformly free text and can echo
  caller-supplied variable content back verbatim (e.g. `Variable "$title" got invalid value "<script>...":
  Expected type "String"`). Writing the GraphQL error arm by direct analogy with the REST arm
  (`string.Join(", ", errors.Select(e => e.Message))`) is the exact wrong move this entry exists to head
  off before anyone writes it that way.
- **Consequences.**
  - The error-mapping filter (`mcp-tool-authoring` §Step 7's `WithGitLabErrorMapping`) gets one added
    `catch (GitLabGraphQlException ex)` arm, extending the same `gitlab_*` vocabulary and the same `Fail()`
    helper the REST arms use — one filter, two producers, per DEC-011's own consequence bullet.
  - A null GraphQL field with zero `errors` is never read as proof of absence — the same tier/visibility/
    missing-id ambiguity the REST 404 case already carries (DEC per `ClientNav` hard rule 25) applies
    identically here; `GitLabGraphQlUnavailableFeatureException` is worded "this tier does not expose X,"
    never "X does not exist."
  - Variables are typed per-operation records by default; `JsonObject` (never `object`/
    `Dictionary<string,object>`) is reserved for one explicitly-scoped, `FullPermission`-only raw-GraphQL
    escape hatch, if one is ever built — not the general path.
  - `Microsoft.Extensions.Http.Resilience`'s AOT-cleanliness is unverified upstream (no dated
    IsAotCompatible statement found); it is tried first and the AOT gate is the actual verdict — a hand-
    written `DelegatingHandler` behind the same `AddGitLabResilience()` extension-method signature is the
    fallback if it is not IL-clean, so no calling code changes either way.

### DEC-022 · Wire naming convention: `gitlab_<verb>_<noun>`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `docs/tool-catalog.md`;
  `.claude/skills/mcp-tool-authoring/SKILL.md`, `.claude/skills/mcp-untrusted-content/SKILL.md`,
  `.claude/skills/mcp-prompts-and-resources/SKILL.md`
- **Decision.** Every tool's wire `Name` is `gitlab_<verb>_<noun>` (e.g. `gitlab_list_issues`,
  `gitlab_create_merge_request`), set explicitly at registration — never the SDK-derived name.
- **Context.** Three skills mandate and demonstrate the prefixed form; one skill
  (`mcp-profile-gating`'s own worked catalog and verified wire-output examples) uses unprefixed names
  (`list_issues`, `retry_pipeline`). Three files to one, and the prefixed form is also what every research
  report produced when independently designing the 722-tool candidate catalog against the domain research
  in this pass — the convention was never actually in live dispute, only inconsistently exampled.
- **Consequences.** `mcp-profile-gating`'s D1-adjacent worked examples are stale on this point and read as
  illustrative of the *gating mechanism*, not the naming convention — they are superseded here rather than
  edited line-by-line. The golden snapshot lists (DEC-014) are generated from the implemented catalog, so
  this decision costs nothing to apply retroactively; it would have cost a full snapshot regeneration if
  settled after `tools/list` had shipped.

### DEC-023 · Resource-level profile gating (`ResourceGrants`)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Profiles/ProfileCatalog.cs`
- **Decision.** `ProfileCatalog` carries a `ResourceGrants` dictionary alongside `ToolGrants` and
  `PromptGrants` from the first `[McpServerResource]` ever added, and
  `ProfileGate.AssertCatalogIsComplete` sweeps registered resources exactly as it already sweeps tools and
  prompts (every resource has a row; every row names something registered).
- **Context.** `ProfileCatalog` had no `ResourceGrants` concept before this pass — the **first** resource
  added would have been granted to every profile by construction, silently, contradicting the
  profile-gated design intent `CLAUDE.md` states for tools. Two skills (`mcp-prompts-and-resources`,
  `mcp-untrusted-content`) each named this as the other's open problem without either allocating a `DEC`
  id, which is exactly the failure mode this file's own header warns about ("a decision recorded only
  inside the skill that happens to mention it is not recorded").
- **Consequences.** The first resource this server ships lands in the same commit as its `ResourceGrants`
  row — there is no grace period. The provenance-envelope question for `ReadResourceResult` (whether a
  resource returning GitLab-authored text needs a `GitLabContent`-equivalent wrapper) is adjacent but
  separate and remains open; it is scoped to whichever resource is built first rather than decided
  speculatively here.

### DEC-024 · `ProfileCatalog` is partitioned by domain (`partial` fragments)

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Profiles/`
- **Decision.** `ProfileCatalog` is a `static partial class`. Each tool domain contributes its grant rows
  in its own file (`ProfileCatalog.Planning.cs`, `ProfileCatalog.MergeRequests.cs`, …), each declaring an
  ordinary `private static IReadOnlyList<ToolGrant> <Domain>Rows() => [...]` method — a plain method with
  a body in its own partial-class file, not a partial method or partial property, so no C# feature more
  exotic than "multiple files contribute members to one partial class" is required of an implementer. A
  single `Lazy<>`-backed dictionary in `ProfileCatalog.cs` calls every domain's method by name and merges
  the results into the one dictionary `ToolGrants` actually is; it is that merged dictionary — not any
  individual fragment file — that `ProfileGate` and `AssertCatalogIsComplete` read. `PromptGrants` and
  `ResourceGrants` (DEC-023) follow the same shape (`PromptRows()`/`ResourceRows()` in a shared
  `ProfileCatalog.Common.cs` fragment).
- **Context.** `mcp-profile-gating` Critical Rule 2 ("`ProfileCatalog.cs` is the only file that maps a
  primitive name to profiles") was written for a small catalog and, taken literally, forbids splitting the
  file at all — in direct tension with DEC-019's exhaustive surface. No skill offered a partitioning
  mechanism as sanctioned before this entry.
- **Consequences.** Critical Rule 2 is satisfied in spirit, not letter: **the merged dictionary** is the
  only source of truth a tool class or `ProfileGate` may query, and no `Tools/` class may reference a
  domain fragment file directly — only the compiler-enforced project-reference boundary
  (`GitlabMCP.Tools` has no reference to `GitlabMCP.Profiles`'s internals beyond the public gate API)
  makes this a rule rather than a convention. A new domain's tools land with their own new fragment file,
  reviewable in isolation, exactly as `GitlabMCP.GraphQL`'s per-domain `JsonSerializerContext` partial
  files already do (DEC-020's design doc, §3).

### DEC-025 · The 722-tool catalog is cut to a 180-tool implemented slice, plus a documented backlog

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `docs/tool-catalog.md`,
  `docs/tool-catalog-backlog.json`
- **Decision.** Of the 722 tool candidates the domain research produced (13 domains, each independently
  curated by a domain-expert research pass, 100% verified against the real `GitLab.Client` XML with zero
  hallucinated method names and zero unresolved cross-domain name collisions after one rename), **180**
  are implemented in this pass — chosen to cover every one of the 13 domains and all four profiles, at
  roughly a 60/40 read/write split — and the remaining **542** are retained as a fully-specified,
  ready-to-implement backlog rather than discarded.
- **Context.** DEC-019 set an exhaustive **floor** of 150+ tools, not a ceiling; the research pass,
  encouraged to be generous per domain, produced 722 verified candidates. Implementing all 722 — each
  needing a projection record, a mapping function, a `[McpServerTool]` registration, a
  `[JsonSerializable]` entry and a `tools/call` smoke-test pass (DEC-007, hard rule 94) — was judged, and
  confirmed by the owner, to exceed what a single implementation pass can deliver at the quality bar this
  repo holds (zero IL warnings, every tool verified callable, no fabricated API surface) within the
  session's budget.
- **Consequences.**
  - Every domain and every profile has real, working, callable tools — nothing is architecturally thin.
    `docs/tool-catalog.md` documents the selection algorithm (proportional per-domain targets, read/write
    balance, round-robin over resource clients for diversity within each domain) so a later pass can
    extend it mechanically rather than re-deriving the approach.
  - `docs/tool-catalog-backlog.json` holds the 542 unimplemented candidates verbatim (name, client,
    method, return type, profiles, readOnly/destructive, purpose) — each already verified against the XML,
    so adding one later is implementation work, not re-research.
  - This is a real, disclosed scope reduction from "every generous domain proposal," not from DEC-019's
    floor — 180 still clears 150+ comfortably. It is recorded here because the repo's own convention is
    that a scope decision of this size belongs in this file, not only in conversation.

---

### DEC-026 · Each tool domain gets its own `JsonSerializerContext` class, not a shared partial fragment

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Contracts/Serialization/`
- **Decision.** Every tool domain (planning, discussion, mergerequests, code, people, projects, cicd,
  packages, search, infra, deploy, admin, lifecycle, epics) declares its own, wholly separate
  `JsonSerializerContext`-derived class (`PlanningJsonContext`, `DiscussionJsonContext`, …), each
  carrying `[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]` and every
  `[JsonSerializable]` attribute for that domain's own payload records, in one file. The host chains
  every one of them into `GitLabJson.Options.TypeInfoResolverChain`. This **replaces**, for newly-added
  domains, the single shared `GitlabMcpJsonContext` partial-class approach `mcp-tool-authoring` §Step 6
  and this repo's own earlier `GitlabMcpJsonContext.cs` (which still holds the `PingResult` and Epic
  types written before this decision) describe.
- **Context.** `mcp-tool-authoring` and the GraphQL client design (DEC-020's research) both describe
  splitting one context's `[JsonSerializable]` attributes across multiple partial-class files as
  supported and safe — Microsoft's own docs say a `JsonSerializerContext` partial class's attributes "can
  be spread across as many partial declarations (files) as you like." **Verified false on this SDK, in
  this repo, this session:** with `GitlabMcpJsonContext.Common.cs` (`[JsonSerializable(typeof(PingResult))]`)
  and `GitlabMcpJsonContext.Epics.cs` (five more types, including two `bool` properties) both present, `dotnet
  build` failed with `CS8785: JsonSourceGenerator ... ArgumentException: The hintName
  'GitlabMcpJsonContext.Boolean.g.cs' of the added source file must be unique in a generator.` Each file
  built cleanly **alone**; only the combination failed. Reproduced identically after a full `obj/`/`bin/`
  clean, and again after forcing the build onto SDK **10.0.303** via a temporary `global.json` (this repo
  normally runs 10.0.401) — not a version-specific regression, a real generator limitation triggered when
  two-or-more files each carry `[JsonSerializable]` attributes for the same context and a reachable
  primitive type (here, `bool`) collides between them. `GitLabGraphQlJsonContext` (DEC-020) never hit
  this: its base file carries zero attributes and `GitLabGraphQlJsonContext.WorkItems.cs` carries all of
  them — one attribute-bearing file, not two, which is the actual safe boundary this decision generalises.
- **Consequences.**
  - The 180-tool implementation pass (DEC-025) fans out across ~14 domains in parallel; each domain
    agent writing to its own private context file is what makes that parallelism safe — the alternative
    (14 agents editing one shared `GitlabMcpJsonContext.cs`) would race on the same file and, per this
    entry's own finding, still likely fail to build even if the races were somehow avoided.
  - `mcp-tool-authoring` §Step 6's "one shared `GitlabMcpJsonContext`" text and this repo's own
    `docs/architecture.md` §Serialization paragraph describing one context "split across per-domain
    files" are both superseded for **new** domains by this entry; they remain accurate for what
    `GitlabMcpJsonContext.cs` already holds (`PingResult`, the Epic records) as of this decision, which is
    not moved retroactively.
  - `GitLabJson.Options`/`GitLabJson.Seal()` (DEC-009) is unaffected in spirit — there is still exactly
    **one** `JsonSerializerOptions` instance for the whole process — only the number of
    `JsonSerializerContext` classes chained into its `TypeInfoResolverChain` grows from two (MCP payloads,
    GraphQL) to one-per-domain-plus-GraphQL. Naming policy still resolves from the ambient options
    instance, not from any context's own `Default`, exactly as DEC-009 already requires.
  - A future reduction of dotnet/runtime's underlying generator bug (if one ships) does not obligate
    reverting this — one context class per domain is also a reasonable steady-state design on its own
    merits (smaller, independently-reviewable compilation units), not only a workaround.

### DEC-027 · `Grant.AdminOnly` is never combined with a persona bit on the same catalog row

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Profiles/*.cs`;
  found and fixed via `docs/review-findings.md`
- **Decision.** A `ToolGrant`'s `Grant` value never combines `Grant.AdminOnly` with a persona bit
  (`Maintainer`/`Developer`/`DevOps`). A row is either bare `Grant.AdminOnly` (reachable only under
  `McpProfile.FullPermission`) or a plain persona combination with no `AdminOnly` bit at all. Which one
  a given tool gets is decided by its own `[Description]`: language stating "requires administrator
  access"/"administrators only" means bare `Grant.AdminOnly`; anything else keeps its persona bit(s).
- **Context.** `Grant.IsVisibleIn` (DEC-005/`Grant.cs`) checks each profile's bit independently —
  `grant.HasFlag(Grant.DevOps)` for `McpProfile.DevOps`, entirely unconditioned on any other bit also set
  on that same `Grant` value. So `Grant.DevOps | Grant.AdminOnly` is reachable under bare
  `McpProfile.DevOps` already, because the `DevOps` bit alone satisfies the check — the added
  `AdminOnly` bit is inert. This is an OR-of-independent-bits model, not an AND-gate: combining
  `AdminOnly` with a persona bit can never *restrict* access beyond what the persona bit alone already
  grants, and since `McpProfile.FullPermission`'s own check is `grant != Grant.None`, the persona bit
  alone is already sufficient for `FullPermission` visibility too — `AdminOnly` adds nothing in
  combination, ever. An adversarial review pass caught this first in `ProfileCatalog.Search.cs` and
  `ProfileCatalog.Admin.cs` (`ProfileCatalog.Admin.cs`'s own header comment asserted the opposite —
  that combining the two was how admin-only rows stayed admin-only, which the code contradicted); a
  follow-up sweep of every other `ProfileCatalog.*.cs` file found the identical pattern, independently
  introduced, in `Deploy`, `Lifecycle`, `People` and `Projects` — six of the 13 domain-implementation
  agents made the same mistake, which is itself evidence the distinction needed to be a rule, not an
  inference each implementer was expected to make correctly.
- **Consequences.**
  - 12 tools that were intended to require instance-administrator access (fleet-wide runner listings,
    instance Pages domains, service accounts, custom member roles, storage-limit exclusions, project
    aliases, topic deletion, and others) were, before this fix, silently reachable by a bare `DevOps`
    profile — a real, live over-grant, not a theoretical one. Golden snapshots
    (`tests/snapshots/DevOps.tools.txt`) moved from 111 to 92 tools as a direct, measured consequence of
    closing it; `Maintainer.tools.txt` moved from 53 to 52 for the same reason
    (`gitlab_list_member_roles`).
  - `ProfileGate.AssertCatalogIsComplete`'s existing three checks (catalog completeness, no
    `Grant.None`, `ReadOnly`-vs-`ReadOnlyHint` agreement — DEC-006) do **not** catch this class of bug:
    a row combining `AdminOnly` with a persona bit is not `Grant.None`, has a complete catalog entry, and
    its `ReadOnly` flag is unaffected. This is a fourth, distinct failure mode the startup assertion does
    not (and structurally cannot, without knowing a tool's *intended* administrative scope) enforce —
    catching it requires either this rule being followed at write time, or a review pass reading each
    tool's own `[Description]` against its `Grant` value, as this pass did.
  - Every future `ProfileCatalog.*.cs` fragment (new domains, or the 542-tool backlog in
    `tool-catalog-backlog.json` as it gets implemented) follows this rule from the start — `docs/architecture.md`
    §Profile gating states it, and `mcp-tool-authoring`/`mcp-profile-gating` should be updated to state it
    explicitly the next time either is edited (not done as part of this entry — recording the decision
    here first, per this file's own rule that a decision only recorded in a skill is not recorded).

### DEC-028 · The untrusted-content envelope extends to resources, via `ReadResourceResult`

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Contracts/GitLabContent.cs`
- **Decision.** `GitLabContent` gains `WrapResource<T>`/`WrapResourceText`, returning `ReadResourceResult`
  with the identical preamble+nonce+delimiter envelope `Wrap<T>`/`WrapText` already build for
  `CallToolResult` — carried in a `TextResourceContents` instead of a `TextContentBlock`. Any resource
  whose content originates from GitLab (an issue, a merge request, an epic, later a wiki page or file)
  must return `ReadResourceResult` and go through one of these two methods, exactly as DEC-007 already
  requires of tools. Server-authored, non-GitLab resource content (profile/server metadata) must NOT go
  through `GitLabContent` at all — wrapping it would falsely claim it as untrusted GitLab input.
- **Context.** The `mcp-prompts-and-resources` skill (written before any resource existed in this repo)
  flagged this explicitly as open: "Until that skill extends the envelope to `ReadResourceResult`, keep
  resources to server-authored or structural data … That is an open decision, and it belongs to
  `mcp-untrusted-content`, not here." `WithResources<T>()` has no `JsonSerializerOptions` overload
  (confirmed against the 2.2.0 XML), so a resource's return type must stay inside the SDK's own context —
  but `ReadResourceResult` itself already is such a type, and nothing stops the method body from
  hand-building its `Contents` from an already-source-generated payload string the same way a tool does.
  Extending the envelope this way adds no new AOT risk: no new `[JsonSerializable]` entry is needed
  beyond what the reused record type (e.g. `IssueSummary`) already carries from its tool.
- **Consequences.**
  - `GitLabContent.Build` was factored to share its preamble+nonce+delimiter logic (`Envelope`) between
    the tool and resource paths — one place still writes the delimiter format, not two copies that could
    drift.
  - `GitLabErrorMapping` (DEC-021) gained a sibling `AddReadResourceFilter` registration (same
    `WithGitLabErrorMapping()` call) — `ReadResourceResult` has no `IsError` field the way `CallToolResult`
    does, so the resource-side handler re-throws as `McpException` instead of returning a soft failure
    result; `McpException.Message` is relayed to the client the same way a tool's mapped message is,
    instead of collapsing to the SDK's generic redacted `-32603 "An error occurred."`. Verified live: an
    unauthenticated `gitlab://issue/...` read now returns `"gitlab_unauthenticated: …"` instead of the
    generic string.
  - `mcp-untrusted-content` and `mcp-prompts-and-resources` should be updated to state this the next time
    either is edited (not done as part of this entry, per this file's own rule).

### DEC-029 · First resource surface: two server-authored resources, three GitLab-content templates, and the `ProfileGate` templated-resource fix

- **Status** DECIDED · **Date** 2026-09-09 · **Owner files** `src/GitlabMCP.Tools/Resources/*.cs`,
  `src/GitlabMCP.Profiles/ProfileCatalog.Resources.cs`, `src/GitlabMCP.Profiles/ProfileGate.cs`
- **Decision.** Two resource classes, both under `GitlabMCP.Tools.Resources`:
  - `ServerResources` — direct (parameterless) resources `gitlab-mcp://server/info` and
    `gitlab-mcp://server/profiles`. Server-authored only (active profile, targeted GitLab base address,
    server time; the static persona table from CLAUDE.md) — never `GitLabContent`-wrapped (DEC-028).
    Granted `Grant.Full`, mirroring the `gitlab_ping` canary: visible in every profile.
  - `GitLabEntityResources` — templated resources `gitlab://issue/{projectId}/{iid}`,
    `gitlab://merge_request/{projectId}/{iid}`, `gitlab://epic/{groupId}/{iid}`. Each reuses the exact
    client call and mapper its equivalent `gitlab_get_*` tool already uses, wrapped via
    `GitLabContent.WrapResource` (DEC-028), and is granted identically to that tool
    (`gitlab_get_issue`/`gitlab_get_merge_request`/`gitlab_get_epic`'s own `Grant` — Planning, Developer,
    Planning respectively) — a client sees the resource iff it could already call the equivalent tool.
    Their purpose is letting an MCP client attach one specific GitLab entity as context directly (e.g. a
    file-reference-style `@gitlab://issue/42/7`), which is a different usage shape than a tool the model
    decides to invoke mid-reasoning — not a redundant duplicate of the `gitlab_get_*` tools.
  - `ProfileGate.Apply`'s resource-gating arm, and `AssertCatalogIsComplete`'s registered-resource sweep,
    are fixed to read `resource.ProtocolResource?.Name ?? resource.ProtocolResourceTemplate?.Name` instead
    of `ProtocolResource?.Name` alone. Before this fix (in place since DEC-023, unexercised until this
    entry's first templated resource), a templated resource's `ProtocolResource` is always `null` — the
    old check's `is not { } protocolResource` branch was unconditionally true, so **every templated
    resource would have been silently removed from every profile, including `FullPermission`**, no matter
    its catalog grant. The code comment above that line already predicted exactly this and said to fix it
    "in the same commit as the first templated resource" — this entry is that commit.
  - `docs/tool-catalog-backlog.json`'s 542 REST tool candidates are unaffected — resources are additive,
    not a substitute for tool coverage.
- **Context.** `mcp-profile-gating`'s own `AssertCatalogIsComplete` already carried `ResourceGrants`
  plumbing from DEC-023 with an explicitly empty `ResourceRows()`, anticipating this. `ProfileCatalog`'s
  `BuildToolGrants` merge point does not need to know about resources at all — resources have their own
  `PrimitiveGrant` catalog, independent of `ToolGrant`'s `ReadOnly` axis (nothing about "read-only" applies
  to a resource read, which is inherently read-only by protocol shape).
- **Consequences.**
  - Verified live against a running server: `resources/list` returns exactly the 2 direct resources in
    every profile; `resources/templates/list` returns 0/2/3/0 templates for
    Maintainer/Developer/FullPermission/DevOps respectively (DevOps holds none of the three
    `gitlab_get_issue`/`_merge_request`/`_epic` tools, so correctly sees no equivalent resource either) —
    this is the fix above being exercised correctly, not merely compiling.
  - `tests/snapshots/<profile>.resources.txt` and `<profile>.resource_templates.txt` are new golden files
    alongside the existing `.tools.txt`/`.prompts.txt` (DEC-014's pattern extended, not replaced).
  - `GitlabMCP.Tools.csproj`'s pre-existing `ProjectReference` to `GitlabMCP.Profiles` remains unused by
    any file under `Resources/` — `ServerInfoResource` deliberately reports raw registered-profile/base-
    address facts rather than a live granted-tool count, specifically to avoid a `Tools` file reading
    `ProfileCatalog` directly, keeping the DEC-018 dependency direction a Tools/Resources file actually
    follows, not just states.

### DEC-030 · Task-oriented prompt library, one level more specific than the per-profile guide

- **Status** DECIDED · **Date** 2026-09-09 · **Owner file** `src/GitlabMCP.Tools/TaskPrompts.cs`
- **Decision.** Six new `[McpServerPrompt]`s in a new `TaskPrompts` class, alongside the existing
  `ProfilePrompts` per-profile overviews: `gitlab_triage_issue_backlog`, `gitlab_review_merge_request`,
  `gitlab_plan_iteration`, `gitlab_investigate_pipeline_failure`, `gitlab_prepare_release`,
  `gitlab_audit_access_review`. Each names a concrete sequence of already-registered tools for one
  recurring job, rather than the persona-wide overview `ProfilePrompts` already gives. Every argument is
  `string`/`int` (never `bool`, per `mcp-prompts-and-resources` Step 2's measured interop trap) and every
  body is a static string literal naming only tool names verified to exist under those exact strings.
  Granted to match the persona(s) that already hold the tools each prompt walks through (e.g.
  `gitlab_prepare_release` → `Grant.Delivery`, matching that it calls both Developer- and DevOps-gated
  tools).
- **Context.** This is the same content shape `ProfilePrompts` already established (DEC-004/CLAUDE.md
  "each profile should ship its own … `[McpServerPrompt]` set") at finer grain — one task, not one whole
  persona. Nothing here required new decisions on return type or registration; `WithPrompts<TaskPrompts>`
  reuses the same `GitLabJson.Options` argument the existing prompt class already needed.
  `ProfileCatalog.Common.cs`'s `PromptRows()` gained six rows in the same commit, per
  `AssertCatalogIsComplete`'s standing requirement.
- **Consequences.** Golden snapshots: `Maintainer.prompts.txt` 1→3, `Developer.prompts.txt` 1→4,
  `DevOps.prompts.txt` 1→4, `FullPermission.prompts.txt` 3→9 — verified live, not merely computed.

### DEC-031 · Resource surface extended to four more GitLab entities

- **Status** DECIDED · **Date** 2026-09-09 · **Owner files** `src/GitlabMCP.Tools/Resources/GitLabEntityResources.cs`,
  `src/GitlabMCP.Profiles/ProfileCatalog.Resources.cs`
- **Decision.** `GitLabEntityResources` gains four more templated resources, each reusing the exact client
  call and mapper of its equivalent tool and granted identically to it (DEC-029's own rule, applied
  again): `gitlab_file` (`gitlab://project/{projectId}/file/{filePath}/{refName}`, ~
  `gitlab_get_file_content`, `Grant.Planning`), `gitlab_wiki_page` (`gitlab://wiki/{projectId}/{slug}`, ~
  `gitlab_get_wiki_page`'s project-scoped case only — the group-wiki case stays tool-only, since a
  resource template wants one simple shape, not the tool's `project` XOR `group` branch — `Grant.Planning`),
  `gitlab_pipeline` (`gitlab://pipeline/{projectId}/{pipelineId}`, ~ `gitlab_get_pipeline`,
  `Grant.Delivery`), `gitlab_release` (`gitlab://release/{projectId}/{tagName}`, ~ `gitlab_get_release`,
  `Grant.Everyone`).
- **Context.** Straightforward application of DEC-029's established pattern — no new SDK behavior, no new
  `GitLabContent`/`ProfileGate` mechanism, just more entities. `gitlab_wiki_page`'s `version`/`renderHtml`
  tool parameters are fixed at `null`/`false` in the resource — a resource is for "the current, plain-text
  version of this thing", not the tool's full parameter surface; a caller needing a historical revision or
  rendered HTML still has `gitlab_get_wiki_page`.
- **Consequences.** `resources/templates/list` grows from 3 to 7 rows; per-profile counts move
  Maintainer 2→5 (`gitlab_issue`/`gitlab_epic`/`gitlab_file`/`gitlab_wiki_page`/`gitlab_release`),
  Developer 3→7 (all seven — `Planning`, `Developer` and `Delivery` all include `Developer`),
  DevOps 0→2 (`gitlab_pipeline`/`gitlab_release`, both the only two rows carrying a `DevOps` bit),
  FullPermission 3→7 — verified live against a running server for all four profiles, not just computed,
  and re-captured in `tests/snapshots/<profile>.resource_templates.txt`.

### DEC-032 · GitHub Actions CI/CD: build+test+AOT-gate on every push, versioned Docker Hub publish on a tag

- **Status** DECIDED · **Date** 2026-09-09 · **Owner files** `.github/workflows/ci.yml`,
  `.github/workflows/release.yml`, `global.json`, `Dockerfile`, `compose.yaml`, `tests/GitlabMCP.Tests/`
- **Decision.** Two workflows, not a reusable `workflow_call` split, and not a single combined one. `ci.yml`
  runs on every push/PR to `main`: `dotnet build`+`dotnet test` in Release, then a `docker build`
  (`linux/amd64`, `push: false`) — the Dockerfile's build stage runs the real `dotnet publish -r
  linux-musl-x64` inside that build, so this doubles as the Native AOT gate on every PR, not just at
  release time. It needs zero secrets, so it runs identically on a fork's pull request. `release.yml`
  triggers on a `vX.Y.Z` tag push, re-runs the same build+test gate, then builds and pushes ONE
  `linux/amd64` image to Docker Hub under four tags — `<user>/gitlabmcp:X.Y.Z-amd64`, `:X.Y.Z`,
  `:latest-amd64`, `:latest` — with `provenance: false` (a flat image manifest, not an OCI index; buildx
  defaults to attaching a provenance attestation even for a single-platform build, verified locally on
  this machine's plain `docker build`), then publishes a GitHub Release via `gh release create
  --generate-notes`. A `global.json` pinning SDK `10.0.401`/`rollForward: latestFeature` was added so a
  local build, the Docker build stage, and `actions/setup-dotnet@v4` (given no `dotnet-version` input) all
  resolve the identical toolchain.
- **Context.** Duplicating the build+test job across the two files was a deliberate call against a
  `workflow_call` reusable workflow: this repo is small enough that the indirection would cost more
  readability than the ~15 duplicated lines it would save, and the two workflows have different trigger
  security postures (ci.yml must stay secret-free for fork PRs; release.yml is secret-bearing and
  tag-gated) that are clearer written out than parameterized. Docker Hub was the target, not GHCR, per an
  explicit ask for a Docker PAT + username pair as GitHub secrets. Single `linux/amd64` only, never a
  multi-arch manifest — arm64 stays out of scope repo-wide (CLAUDE.md), so a `version:arch` tag is
  unambiguous rather than a placeholder for a future second platform.
- **The blocking bug this surfaced.** `GitlabMCP.slnx` referenced `tests/GitlabMCP.Tests/GitlabMCP.Tests.csproj`,
  and CLAUDE.md/DECISIONS.md/two skills all asserted that project existed — but the project's files were
  never actually present on disk (only `Directory.Packages.props`' pinned xunit/Test.Sdk/coverlet versions
  survived from whenever it once did), so `dotnet build GitlabMCP.slnx` failed outright with `MSB3202`
  before any of today's work started. A `dotnet build`/`dotnet restore` run during this session's own
  investigation silently rewrote `GitlabMCP.slnx` to drop the dangling reference entirely (no error, no
  log line) — worth knowing: this SDK version self-heals a solution file pointing at a missing project
  rather than continuing to fail loudly. Fixed by writing the real project (`GitlabMCP.Tests.csproj`, 3
  test files, 37 tests: `GitLabMcpOptionsExtensions.ResolveProfile`/`ResolveVersion`, `Grant.IsVisibleIn`,
  and `GitLabGraphQlEndpoint.Resolve` against gitlab.com/self-hosted/path-prefixed/non-standard-port bases)
  and re-adding the `<Folder Name="/tests/">` entry to `GitlabMCP.slnx`. Two more dotnet-new template
  leftovers were found and removed in the same pass — `src/GitlabMCP/Tools/RandomNumberTools.cs` (already
  deleted from disk but still staged as added in git, i.e. `git status` showed `AD`) and a generic
  `src/GitlabMCP/README.md` describing the demo random-number tool — neither was ever part of this
  project's real surface, and `mcp-server-smoke-test`/`docker-aot-image` referenced both as if current;
  both skills were corrected in the same commit.
- **The version knob.** `GitLabMcp:Version` (env `GitLabMcp__Version`) is new on `GitLabMcpOptions`,
  resolved by `ResolveVersion()` (absent → `"dev"`) exactly like `ResolveProfile()`. Unlike every other
  config key here, an operator never sets it — the Dockerfile's `ARG APP_VERSION` (default `0.0.0-dev`)
  bakes it in at build time via `-p:Version=$APP_VERSION -p:InformationalVersion=$APP_VERSION` on the
  `dotnet publish` line, then re-declares the ARG in the final stage to export it as that `ENV`.
  `ServerInfoResource` gained a `Version` field so `server_info` reports it at runtime. Verified live: a
  local `docker build --build-arg APP_VERSION=9.9.9-test` plus `docker run` produced a startup log line
  `GitlabMCP 9.9.9-test starting: profile=Developer gitlab=https://gitlab.example.internal/api/v4/` and a
  `resources/read` on `gitlab-mcp://server/info` echoing `"version":"9.9.9-test"` — confirming
  `-p:Version`'s SemVer2 prerelease suffix survives into `InformationalVersion` even though the MCP SDK's
  own `initialize.serverInfo.version` (assembly version, numeric-only) reported `9.9.9.0`.
- **Self-hosted GitLab was already implemented, not new.** `GitLab:BaseAddress` (default
  `https://gitlab.com/api/v4/`, validated absolute + trailing-`/` by `ValidateGitLabClientOptions`) and
  `GitLabGraphQlEndpoint.Resolve`'s segment-walk (DEC-020/021) already supported a bare self-hosted domain,
  a path-prefixed self-hosted install, and a non-standard port — this decision's contribution is making
  that fact discoverable and verified: pinned down by `GitLabGraphQlEndpointTests`, documented in
  `README.md`'s config-key table, and given a copy-paste `compose.yaml` example.
- **Operability.** A startup log line (`Program.cs`, after `ProfileGate.AssertCatalogIsComplete`) prints
  version/profile/GitLab-target — never the token — so `docker logs` alone confirms a container picked up
  the right configuration without a single MCP call.
- **Required GitHub repository secrets** (documented in `README.md`, not stored anywhere in this repo):
  `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a Docker Hub Personal Access Token, Read & Write scope).
  `ci.yml` needs neither; `release.yml`'s GitHub Release step uses the ambient `GITHUB_TOKEN`.
- **Consequences.** `dotnet build GitlabMCP.slnx` and `dotnet test GitlabMCP.slnx` are both green again
  (0 warnings/errors, 37/37 tests) after being silently broken. Every push/PR now gets a real Native AOT
  regression gate, not just at Docker-release time. A tagged release is fully automated from `git tag` to
  a pulled, versioned image plus a GitHub Release. `docs/architecture.md`, `CLAUDE.md`, and the
  `docker-aot-image`/`mcp-server-smoke-test` skills were updated to stop describing the pre-fix,
  pre-CI/CD, single-project-era state as current.

### DEC-033 · `.gitignore`'s `**/[Pp]ackages/*` was silently dropping this project's own `Packages/` source

- **Status** DECIDED · **Date** 2026-09-09 · **Owner files** `.gitignore`
- **Decision.** Deleted the standard `VisualStudio.gitignore` boilerplate block `**/[Pp]ackages/*` (plus
  its `!**/[Pp]ackages/build/` exception) rather than narrowing it. This repo uses Central Package
  Management (`Directory.Packages.props`) + `PackageReference` exclusively — there is no
  `packages.config`-era restore folder for that rule to ever legitimately match.
- **Context.** The first real push (this session, DEC-032) was followed by CI's very first run failing
  with `CS0234`/`CS0246`/`SYSLIB1030` on `PackagesJsonContext.cs` — the `GitlabMCP.Contracts.Packages`
  namespace didn't exist on the runner. Root cause: git's ignore matching is case-insensitive on Windows
  (`core.ignorecase=true`, the default), so `**/[Pp]ackages/*` matched this project's own
  `src/GitlabMCP.Contracts/Packages/` and `src/GitlabMCP.Mapping/Packages/` directories — 15 files, the
  entire GitLab Packages/Container Registry/Debian/Terraform-module domain — and they were **never
  tracked by git at all**, from the moment those directories were first created. Invisible for the whole
  session because every local `dotnet build`/`docker build` reads the real filesystem directly, never
  through git; a fresh clone (exactly what GitHub Actions' first run did) was the first thing to ever
  actually exercise what git had recorded, and it broke immediately.
- **Consequences.** Recovered via `git add -f`; verified with a genuinely fresh `git clone` built inside
  a Linux container (matching CI's environment exactly, not just re-testing the already-populated local
  working tree) before pushing again. The real, actual first GitHub Actions run after this fix is green:
  build+test and the Docker/AOT gate both pass. **Lesson for any future large `git add -A`/session-start
  staging operation in this repo: verify with a fresh clone, not just `git status`, since a case-fold
  gitignore collision produces zero local symptoms.**

### DEC-034 · `GitLab:BaseAddress` accepts a bare URL — `api/v4/` is appended automatically

- **Status** DECIDED · **Date** 2026-09-10 · **Owner files** `src/GitlabMCP.Abstractions/GitLabBaseAddressNormalizer.cs`,
  `src/GitlabMCP/Program.cs`
- **Decision.** A new `GitLabBaseAddressNormalizer.Normalize(Uri)` runs inside the existing
  `PostConfigure<GitLabClientOptions>` in `Program.cs` — before `ValidateGitLabClientOptions` or
  `GitLabGraphQlEndpoint.Resolve` ever see the value. Rule: if the URL's path already contains an `api`
  segment anywhere, leave it untouched (assume the operator meant exactly what they typed — including a
  deliberately-but-wrongly-versioned `.../api/v5/`, which should fail loudly, not be silently rewritten
  into a nonsense double path); otherwise append `api/v4/` to whatever prefix was given — nothing for a
  bare domain, the existing prefix for a path-prefixed self-hosted install — and always normalize to
  exactly one trailing slash.
- **Context.** DEC-032/CLAUDE.md had already documented that `GitLab:BaseAddress` supports self-hosted
  instances, but required the operator to know and type the exact `api/v4/` REST suffix themselves — a
  real, reported point of friction (the request was, verbatim, "just set up the GitLab URL, since it
  could be a self-hosted GitLab's URL," which the prior design technically satisfied but did not make
  simple). Normalizing once, centrally, before both consumers of the value (REST validation and GraphQL
  endpoint derivation) — rather than teaching either of them to accept a bare URL individually — keeps
  the "one place, not per-call" pattern this codebase already uses for error mapping and profile gating.
- **Consequences.** `GitLab__BaseAddress=https://gitlab.mycompany.internal` (no path at all) now boots
  and reports `gitLabBaseAddress: "https://gitlab.mycompany.internal/api/v4/"` from the `server_info`
  resource — verified live in a rebuilt Docker container, alongside a path-prefixed bare self-hosted URL
  (`https://example.com/gitlab` → `.../gitlab/api/v4/`) and confirming a deliberately wrong version path
  (`https://gitlab.com/api/v5/`) still fails fast with the same clear `OptionsValidationException` as
  before, unmangled. 15 new unit tests (`GitLabBaseAddressNormalizerTests`) cover the bare-domain,
  path-prefixed, non-standard-port, already-correct (idempotency), and leave-alone cases — total suite
  37→52. README.md, `compose.yaml` and the `docker-aot-image` skill's config-key index were updated to
  describe "just set your instance's URL" rather than "must end in `api/v4/`".

### DEC-035 · GitHub Actions Docker build: no `cache-from`/`cache-to: type=gha`

- **Status** DECIDED · **Date** 2026-09-10 · **Owner files** `.github/workflows/ci.yml`, `.github/workflows/release.yml`
- **Decision.** Neither workflow's `docker/build-push-action` step sets `cache-from`/`cache-to`. Restore
  and publish always run together, live, in one uninterrupted BuildKit session per CI run — never one
  imported from cache and the other executed fresh.
- **Context.** The commit that landed DEC-034 triggered CI's Docker job to fail with the exact
  `NETSDK1064` ("Package Microsoft.NET.ILLink.Tasks ... was not found") the `docker-aot-image` skill's own
  pitfall table already named — one layer up from where that table described it. `type=gha` caches
  *layers*; the Dockerfile's restore and publish steps share a `RUN --mount=type=cache` NuGet mount, which
  is explicitly excluded from a layer's exported filesystem diff. GitHub's runners are ephemeral — a fresh
  BuildKit builder every job — so when buildx cache-hit the restore layer (skipping its execution, and
  with it the mount-fill restore would have done) while a later `COPY src/` change forced publish to run
  live, publish's mount was empty and `--no-restore` had nothing to work with. This never reproduced
  locally: a persistent local Docker daemon keeps the same cache-mount store across every build, so restore
  populating it once was enough for the whole session, masking the bug completely until the very first CI
  run to hit a partial cache (the second CI run overall — the first had no prior gha cache to import from
  at all, so it ran everything live and happened to pass).
- **Consequences.** Confirmed fixed: re-pushing without `cache-from`/`cache-to` produced a fully green CI
  run. Slower Docker builds on every CI run (no restore-layer reuse across runs) in exchange for
  correctness — the AOT `Generating native code` step dominates wall-clock time either way (~200s of
  ~230s total, measured), so the caching this removes was buying comparatively little. Do not reintroduce
  `type=gha` (or any other cache backend) for this build step without first either (a) fusing restore and
  publish into one `RUN` so they can never be cache-split, or (b) verifying the chosen cache backend
  actually persists BuildKit mount contents across ephemeral runners, not just layers — re-read
  `docker-aot-image`'s "Common Pitfalls" table first either way.

## Re-verify the stamped facts

Run from the repo root in Git Bash. Each was run on 2026-09-09 and the expected output is beside it.

```bash
# DEC-001, DEC-002 — RID set and the SDK version of record
grep -oE '(RuntimeIdentifiers>[^<]*|Include="[^"]*" Version="[^"]*")' GitlabMCP/GitlabMCP.csproj
#   RuntimeIdentifiers>win-x64;linux-x64;linux-musl-x64
#   RuntimeIdentifiers>                      <- the closing tag; expected, ignore it
#   Include="ModelContextProtocol.AspNetCore" Version="2.2.0"

# DEC-002 — Core is byte-identical across the bump; the two md5s must match
md5sum ~/.nuget/packages/modelcontextprotocol.core/2.{1,2}.0/lib/net10.0/ModelContextProtocol.Core.xml

# DEC-002 — the whole public delta: 6 lines added under "only in 2.2.0", nothing under "only in 2.1.0"
for v in 2.1.0 2.2.0; do
  grep -oE 'name="[^"]+"' ~/.nuget/packages/modelcontextprotocol.aspnetcore/$v/lib/net10.0/ModelContextProtocol.AspNetCore.xml |
    sort -u > /tmp/asp-$v.txt
done
echo "only in 2.2.0:"; comm -13 /tmp/asp-2.1.0.txt /tmp/asp-2.2.0.txt
echo "only in 2.1.0:"; comm -23 /tmp/asp-2.1.0.txt /tmp/asp-2.2.0.txt

# DEC-003 — the transport line, and that SessionMode is not also assigned
grep -n 'Stateless\|SessionMode' GitlabMCP/Program.cs
#   11:        options.Stateless = true;
```

---

## Agent appends

Entries appended by agents in `gitlab-mcp-tool-builder` §Step 8's shorter shape land here. Promote each
to a numbered `DEC-0NN` entry above when reviewing the change that produced it, and leave the original
line in place.

*(none yet)*
