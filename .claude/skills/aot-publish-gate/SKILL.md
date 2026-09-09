---
name: aot-publish-gate
description: >
  Run and interpret GitlabMCP's Native AOT publish gate — the check that `dotnet build`
  does not perform. This skill is the PROCEDURE; the agent `aot-gatekeeper` is the one
  that RUNS it end to end and reports per-RID. USE FOR: `dotnet publish -c Release -r
  win-x64`, MSB3073 / exit code 123 / "vswhere.exe is not recognized" /
  `Microsoft.NETCore.Native.targets(396,5)`, "is the tree still AOT-clean", IL2026 /
  IL3050 pass-fail check, which RID to publish and when, "Cross-OS native compilation is
  not supported", a publish that exits 1 with no error text, a false PASS, why
  GitlabMCP.exe changed size. DO NOT USE FOR: diagnosing and fixing an IL warning once one
  appears (use dotnet-upgrade:dotnet-aot-compat), capturing or reading an MSBuild binlog
  (use dotnet-msbuild:binlog-generation and dotnet-msbuild:binlog-failure-analysis),
  choosing Docker base images or writing the Dockerfile (use `docker-aot-image`), writing
  tool methods and their `JsonSerializerContext` entries (use `mcp-tool-authoring`).
---

# AOT Publish Gate

`GitlabMCP.csproj` sets `PublishAot=true` unconditionally. That makes an ordinary `dotnet build` run
the Roslyn trim/AOT/single-file analyzers, but it does **not** run ILCompiler and it does **not** link
a native binary. Whole-program ILC warnings and the entire native toolchain are only exercised by
`dotnet publish -r <rid>`. This skill is that publish loop: how to run it so the result means
something, how to turn the log into a pass/fail, and what to do with each way it fails.

**Current baseline — keep it.** Measured 2026-09-09 against `ModelContextProtocol.AspNetCore` **2.2.0**
(the single `PackageReference` in the csproj). All three RIDs were gated for real in that session; none of
these numbers is carried forward.

| Gate | Verdict | Artifact of record |
|---|---|---|
| `dotnet build GitlabMCP.slnx` | 0 warnings, 0 errors | — |
| clean `win-x64` publish | exit 0, **zero** IL2xxx/IL3xxx, one `Generating native code` | `GitlabMCP.exe` **24,549,888 B** (23.4 MiB), measured three times, identical each time |
| `docker build .` (musl) | exit 0, zero IL codes, one `Generating native code` | `/app/GitlabMCP` **23,106,152 B**; exported rootfs **34,798,592 B** |
| `docker build --build-arg RID=linux-x64 …` (glibc, Step 4) | exit 0, zero IL codes, one `Generating native code` | `/app/GitlabMCP` **23,106,400 B**; exported rootfs **37,331,456 B** |

**24,549,888 B is the `win-x64` number of record.** `aot-gatekeeper` quotes the same figure in its Output
Format template and explicitly retires the older **24,549,376 B**; the two files agree. If you meet 24,549,376 B
anywhere, it is stale and a delta computed against it prints a spurious `+512 B` on an otherwise clean run.

Image size — `docker images` says 59.3 MB musl, 53.8 MB glibc — is **not** a gate figure; `docker-aot-image`
§10 calls it the wrong measure. Compare the binary and the exported rootfs.

## When to Use

- Before committing anything that adds a package reference, a DTO crossing the MCP boundary, or a
  new `WithTools<T>()` registration.
- After `dotnet add package GitLab.Client`, or any package version bump.
- When a publish fails and you need to tell a toolchain problem from a code problem.
- When someone asks "does this still AOT?" — a green `dotnet build` is not an answer.

## When Not to Use

- To *fix* an IL warning. This skill only decides pass/fail and hands you off.
- For Debug-loop work. `dotnet build GitlabMCP.slnx` is the fast inner loop and it does catch
  Roslyn-visible IL warnings; run the gate at commit boundaries, not per edit.
- For anything about the container image beyond "did the publish inside it stay clean".
- As the *runner*. If the ask is "run the gate and report", that is the **`aot-gatekeeper`** agent in
  `.claude/agents/` — it loads this skill and executes these steps, and it owns the per-RID report
  format, the timeout discipline and the resume-certification rule. This file is the procedure only.

## Critical Rules

| Rule | Why |
|---|---|
| **Delete `bin/Release` and `obj/Release` before a gate run.** | A warm tree skips ILC *and* the link step and exits 0 while still printing the vswhere error. Verified: a stale tree gives a clean **false PASS**. |
| **AND the exit code with the IL scan.** Neither alone is a gate. | A hard MSB3073 failure log contains **zero** IL codes (naive IL scan says PASS); an IL2026/IL3050 build exits **0** (naive exit-code check says PASS). Both cases reproduced. |
| **Never `#pragma warning disable` or `[UnconditionalSuppressMessage]` an IL warning.** | The linker still trims the member; the failure moves from build time to runtime. This is `dotnet-upgrade:dotnet-aot-compat`'s rule — it applies here without exception. |
| **Match on `(warning\|error) IL\d{4}`, never on prose or the summary line.** | MSBuild does not localise the diagnostic keyword — `warning IL2026` stays English even in a localised log — but it *does* localise the summary line (this machine's MSBuild UI is fr-FR and prints `2 Avertissement(s)`, not `2 Warning(s)`). |
| **Do not add `<IsAotCompatible>` or `<IsTrimmable>` to the csproj.** | `PublishAot=true` already turns on all three analyzers. Verified via `dotnet msbuild -getProperty`: `EnableAotAnalyzer`, `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer` are all `true` while `IsAotCompatible` is empty. `dotnet-aot-compat` Step 1 tells you to add it; that step is a no-op here. |
| **Never add an arm64 RID.** | Repo-wide rule, stated in `CLAUDE.md`. Not in the RID list, not in a publish command, not in a Dockerfile, not as an aside. |

## The RID matrix

`<RuntimeIdentifiers>win-x64;linux-x64;linux-musl-x64</RuntimeIdentifiers>` — three RIDs, x64 only.
**Each RID is a separate publish. There is no "publish all".** Adding a RID to that list is a
commitment to publishing and gating it.

| RID | Matters for | Where this repo publishes it | Final-stage libc |
|---|---|---|---|
| `win-x64` | Local dev; the fastest full gate on this machine | On Windows, needs `vswhere.exe` on PATH | n/a |
| `linux-musl-x64` | **The shipped container** (`Dockerfile` default) | On Linux x64; here, via `Dockerfile` — `mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot` | musl — Alpine base |
| `linux-x64` | glibc container variant | On Linux x64; here, via `Dockerfile` + `--build-arg SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-aot` | glibc — chiseled base |

(Both SDK tags verified present on MCR. Nothing makes them the *only* place those RIDs can be built — any
Linux x64 host with the Native AOT prerequisites will do; they are simply what this repo builds in.)

**You cannot cross-publish the Linux RIDs from Windows.** Verified:

```text
dotnet publish ... -r linux-musl-x64
  → Microsoft.NETCore.Native.Publish.targets(60,5): error : Cross-OS native compilation is not supported.
```

The managed compile still runs first (it produces `bin/Release/net10.0/linux-musl-x64/GitlabMCP.dll`
before failing), so `dotnet build -r linux-musl-x64` on Windows is a valid *analyzer* check for that
RID — but it is not the gate. The Linux gate runs through Docker: see Step 4.

## Step 1 — Put `vswhere.exe` on PATH (Windows only, and it is mandatory)

Without it, a clean `win-x64` publish fails like this — **exit 1**:

```text
  Generating native code
C:\Users\Arius\.nuget\packages\microsoft.dotnet.ilcompiler\10.0.12\build\Microsoft.NETCore.Native.targets(396,5):
error MSB3073: The command ""'vswhere.exe' is not recognized as an internal or external command;
...;C:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\link.exe"
@"obj\Release\net10.0\win-x64\native\link.rsp"" exited with code 123.
```

(On this machine the prose arrives in French — `'vswhere.exe' n'est pas reconnu en tant que commande
interne ou externe` — but `error MSB3073`, `code 123` and the targets path are stable.)

**`MSB3073` alone does not mean vswhere. The targets line number is the discriminator.** Both native steps
are `Exec` tasks in the same file, so both report `MSB3073`: line **396** is the linker (`$(CppLinker)` with
`link.rsp`) and fails with **code 123** on this problem; line **330** is the `ilc` invocation itself
(`IlcCompile`, the target that prints `Generating native code`) and fails with **code 1** on a different
one. `(330,5)` is not a PATH problem — see *Known failure modes*.

**Root cause, traced in the ILCompiler targets — this is not a code problem and no csproj change fixes it.**
`Microsoft.NETCore.Native.Windows.targets(126)` Execs `findvcvarsall.bat` with `ConsoleToMSBuild="true"`.
That batch file calls `vcvarsall.bat`, which invokes bare `vswhere.exe`; when it is not on PATH the
"not recognized" text goes to **stderr** and gets captured into the task's console output. Lines 137-138
then split that output into `_CppToolsDirectory` and `CppLinker`, so `$(CppLinker)` becomes
`"<error prose>;C:\...\link.exe"`. `Microsoft.NETCore.Native.targets(396,5)` Execs that string, cmd
returns **123**, MSBuild reports **MSB3073**. The exit code and any "file name syntax" line are
downstream noise — the only thing to fix is PATH.

**This reproduces identically in PowerShell and in Git Bash. Do not waste a cycle switching shells.**

PowerShell 7 (primary on this machine):

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
Get-Command vswhere.exe | Select-Object -ExpandProperty Source   # sanity check
```

Git Bash:

```bash
export PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH"
command -v vswhere.exe
```

The prepend is per-shell and does not persist. Do it at the top of every gate session.

## Step 2 — Publish from a clean tree

```powershell
Remove-Item -Recurse -Force 'C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP\bin\Release' -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force 'C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP\obj\Release' -ErrorAction SilentlyContinue
```

```bash
rm -rf /c/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP/bin/Release \
       /c/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP/obj/Release
```

Skipping this is the single most common way to get a meaningless PASS. Verified sequence: publish
fails on vswhere → fix PATH → publish succeeds → *remove the PATH fix* → publish **exits 0** and still
prints the vswhere error, because `obj/.../native/GitlabMCP.exe` was already up to date and the link
step never ran.

A genuine full run prints `Generating native code`. If that line is missing, ILC did not run — the build
was incremental, or it only re-linked an object ILC had written earlier — and the verdict is worthless
however healthy the resulting exe looks.

## Step 3 — Turn the log into a verdict

Both blocks below were run against four real logs (clean pass, MSB3073 failure, an IL2026+IL3050
build that exited 0, and the stale-tree false pass) and agree on all four.

Git Bash:

```bash
PROJ="C:/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP/GitlabMCP.csproj"
LOG=/c/Users/Arius/AppData/Local/Temp/publish-win-x64.log

dotnet publish "$PROJ" -c Release -r win-x64 > "$LOG" 2>&1; RC=$?

if   [ $RC -ne 0 ]; then
  echo "GATE: FAIL - publish failed (exit $RC)"
  grep -oE 'error (MSB[0-9]+|CS[0-9]+|IL[0-9]{4})' "$LOG" | sort -u
elif grep -qE '(warning|error) IL[0-9]{4}' "$LOG"; then
  echo "GATE: FAIL - $(grep -oE '(warning|error) IL[0-9]{4}' "$LOG" | sort -u | tr '\n' ' ')"
elif ! grep -q 'Generating native code' "$LOG"; then
  echo "GATE: INVALID - ILC did not run; outputs were stale, delete bin/Release + obj/Release"
else
  echo "GATE: PASS - AOT publish clean, 0 IL warnings"
fi
```

PowerShell 7 — note the `@(...)` wrappers. Without them, `(Select-String ...).Matches` throws
`InvalidOperation: Cannot index into a null array` on a zero-match log, i.e. exactly on a passing run:

```powershell
$proj = 'C:\Users\Arius\RiderProjects\GitlabMCP\GitlabMCP\GitlabMCP.csproj'
$log  = "$env:TEMP\publish-win-x64.log"

dotnet publish $proj -c Release -r win-x64 2>&1 | Tee-Object -FilePath $log
$rc = $LASTEXITCODE

$codes = @(@(Select-String -Path $log -Pattern '\b(?:warning|error) (IL\d{4})\b' -AllMatches) |
           ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

if     ($rc -ne 0)     { "GATE: FAIL - publish failed (exit $rc)" }
elseif ($codes.Count)  { "GATE: FAIL - IL warnings: " + ($codes -join ', ') }
elseif (-not (Select-String -Path $log -SimpleMatch 'Generating native code' -Quiet)) {
                         "GATE: INVALID - ILC did not run; tree was stale" }
else                   { "GATE: PASS - publish clean, 0 IL warnings" }
```

On PASS, confirm the artifact shape — one executable, no runtime, no loose assemblies:

```text
bin/Release/net10.0/win-x64/publish/
  GitlabMCP.exe                            24,549,888 B   <- the deliverable
  GitlabMCP.pdb                           112,726,016 B   <- symbols, not shipped
  GitlabMCP.staticwebassets.endpoints.json        53 B
```

## Step 4 — Gate the container RIDs

Both Linux RIDs are gated by building the image; the publish runs inside the build stage. From the repo root.

`linux-musl-x64` — the `Dockerfile` default, and the one that gates every commit:

```bash
LOG=/c/Users/Arius/AppData/Local/Temp/docker-gate.log
docker build --no-cache --progress=plain -t gitlabmcp:gate . > "$LOG" 2>&1; echo "EXIT=$?"
grep -oE '(warning|error) IL[0-9]{4}' "$LOG" | sort -u
```

```powershell
$log = "$env:TEMP\docker-gate.log"
docker build --no-cache --progress=plain -t gitlabmcp:gate . 2>&1 | Tee-Object -FilePath $log
"EXIT=$LASTEXITCODE"
@(@(Select-String -Path $log -Pattern '\b(?:warning|error) (IL\d{4})\b' -AllMatches) |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
```

Same verdict logic as Step 3 — exit code AND IL scan AND `Generating native code` present in the log.

`linux-x64` — the glibc variant. The `Dockerfile` parameterises RID and both images, so it is the same
build with three `--build-arg`s (all three values are the `Dockerfile`'s own documented set):

```bash
LOG=/c/Users/Arius/AppData/Local/Temp/docker-gate-glibc.log
docker build --no-cache --progress=plain \
  --build-arg RID=linux-x64 \
  --build-arg SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-aot \
  --build-arg RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled \
  -t gitlabmcp:gate-glibc . > "$LOG" 2>&1; echo "EXIT=$?"
grep -oE '(warning|error) IL[0-9]{4}' "$LOG" | sort -u
```

**Verified 2026-09-09, and run twice independently with `--no-cache`:** exit 0, exactly one
`Generating native code`, **zero** IL codes, publish step 45.1 s, 54.1 s of step time end to end. Both runs produced
byte-identical artifacts — `/app/GitlabMCP` **23,106,400 B**, exported rootfs **37,331,456 B**.

`docker build` never prints the binary size, and the two final stages need different commands — the
chiseled/glibc stage has **no shell at all** (`/bin/sh: no such file or directory`), so it goes through the
filesystem:

```bash
# musl / Alpine final stage — busybox `sh` and `stat` are present
MSYS_NO_PATHCONV=1 docker run --rm --entrypoint /bin/sh gitlabmcp:gate -c 'stat -c %s /app/GitlabMCP'

# chiseled / glibc final stage — no shell
cid=$(docker create gitlabmcp:gate-glibc)
MSYS_NO_PATHCONV=1 docker cp "$cid:/app/GitlabMCP" ./tmp-bin && docker rm "$cid" && stat -c %s ./tmp-bin && rm ./tmp-bin
```

`MSYS_NO_PATHCONV=1` is a Git Bash requirement — it otherwise rewrites a docker argument starting with `/`
into a Windows path. PowerShell needs no prefix.

**The container gate has been executed end to end** for **both** Linux RIDs, `--no-cache`, against the
current `Dockerfile` — `aot-gatekeeper` records the same, so the two files agree. The baseline table at the
top of this file is the record that agent asks the first container gate of a session to establish. Its
standing instruction — re-measure before comparing a delta — still applies.

**Gate `linux-x64` on demand, not on every commit** — when the glibc image is being shipped or refreshed,
when a package reference changes, or when the musl gate fails and you need to know whether the cause is
libc-specific. It shares the csproj, the analyzers and the ILC version with musl, so it almost never
diverges; but it *is* in `<RuntimeIdentifiers>`, so it is never allowed to go unpublished before a release.

**`--no-cache` (or at least an assertion that `Generating native code` appears in the log) is not
optional.** Verified: a warm rebuild prints `#14 CACHED` for the publish step, emits no publish output
at all, and an IL scan over that log finds nothing — the same false-PASS class as the stale `obj/`.

Base images, layer ordering, `.dockerignore` and the libc↔RID coupling belong to `docker-aot-image`;
this skill only cares that the publish inside the image stayed clean.

## The rules the gate is enforcing

Each of these exists to prevent a specific failure. They are project policy, not style. The first three
are `mcp-tool-authoring`'s rules and the recipes live there — what this table adds is the **last column**:
which of them a green gate actually proves anything about.

| Rule | Failure it prevents | Does the gate catch it? |
|---|---|---|
| Every parameter type, every return type **and every `GitLabContent.Wrap<T>` payload type** crossing the MCP boundary has a `[JsonSerializable]` entry in a source-generated context. | `JsonSerializerIsReflectionEnabledByDefault` is already **false** here (verified). Two different timings. A missing entry for a **parameter type or a record return type** fails *schema generation*, so it throws `NotSupportedException` while `WithTools<T>` builds the tool — the server never starts. A missing entry for a **`GitLabContent.Wrap<T>` payload type** generates no schema at all, so the server starts, `tools/list` is complete, and the throw lands on that tool's **first `tools/call`**, out of `McpJsonUtilities.GetTypeInfo<T>` and redacted on the wire to `An error occurred invoking 'gitlab_x'.` (measured; the type name appears only in the server log). | **No.** Reflection-based `JsonSerializer` calls surface as IL2026+IL3050, but a missing context entry produces no warning at all. Starting the server catches only the first kind; the second needs a live `tools/call` on **every** tool. |
| Chain the context onto a **copy** of `McpJsonUtilities.DefaultOptions`; never assign `TypeInfoResolver` wholesale. | Replacing it compiles clean, publishes clean, and silently corrupts the wire. No exception, no warning, wrong data. | **No.** Only a live `tools/call` catches it. |
| `WithTools<T>()` / `WithPrompts<T>()` / `WithResources<T>()` only. **Never** the `*FromAssembly` or `IEnumerable<Type>` overloads. | Those overloads are `[RequiresUnreferencedCode]`; the generic ones carry `[DynamicallyAccessedMembers]` on `TToolType` instead, which is what makes them trim-safe. | **Yes — IL2026 at the call site**, already on an ordinary `dotnet build`. The gate is not even needed. |
| `InvariantGlobalization=true` ⇒ no culture-sensitive parse or format anywhere. Use `CultureInfo.InvariantCulture` explicitly and `StringComparison.Ordinal`. | Culture data is not in the binary. Culture-sensitive `ToString`/`Parse` silently behave as invariant; asking for a specific culture throws. The Alpine and chiseled runtime images ship no ICU at all. | **No** — it is a runtime behaviour change, not a warning. |
| No `MakeGenericType`, `Activator.CreateInstance(Type)`, `Type.GetMethod` + `Invoke`, or `Assembly.GetTypes()` on any path reachable from a tool. | Generic instantiations and members ILC could not see statically do not exist at runtime. | **Yes — IL2xxx / IL3050.** |

`GitLab.Client` is documented as AOT/trim-clean (source-generated STJ, source-generated config binding,
no reflection DI). Whether *its* DTOs need `[JsonSerializable]` entries in **your** context when a tool
returns one is an **open question** — the package is not referenced yet, so nobody has measured it.
Settle it with a real publish the day the reference lands; do not assume either answer.

## When an IL warning appears

Load **`dotnet-upgrade:dotnet-aot-compat`** and follow its loop: build → pick one warning → open only
that file and line → fix → rebuild. **Do not** explore the codebase up-front. **Do** batch 5-10 fixes per
rebuild, cap at 3 iterations per warning, and stop at zero — those three are its instructions, follow them.

Two corrections to that skill for this repo:

- Its Step 1 (add `<IsAotCompatible>`) is a no-op here — see *Critical Rules*. Skip it.
- Its grep (`grep 'IL[0-9]\{4\}'`) is weaker than Step 3 above — no `warning|error` anchor, no exit-code
  AND, no ILC-ran assertion. Use Step 3's.

There are **no** IL warnings in this tree today. When they do appear they will almost certainly be
IL2026/IL3050 from `JsonSerializer`, because that is the only reflection surface an MCP tool server
normally has — so its Strategy C (migrate to a source-generated `JsonSerializerContext`) is the right
first move. Wire the context the MCP way (chained onto `McpJsonUtilities.DefaultOptions`, passed to
`WithTools<T>()`), not the call-site way (`JsonSerializer.Serialize(obj, Ctx.Default.Type)`) that the
skill demonstrates.

**Do not** load `dotnet11:system-text-json-net11`. It covers three APIs that exist only on net11.0 and
says so in its own frontmatter; this project is net10.0.

## When MSBuild fails instead of the compiler

Symptom: a nonzero exit whose log has **no** `error CS`, no `error IL`, and either no error line at all
or one pointing into a `.targets` file. `-bl:` works on `dotnet publish`:

```bash
dotnet publish "$PROJ" -c Release -r win-x64 -bl:/c/Users/Arius/AppData/Local/Temp/publish.binlog
```

```powershell
dotnet publish $proj -c Release -r win-x64 -bl:"$env:TEMP\publish.binlog"
```

An explicit path needs no escaping in PowerShell — verified on 7.6.5, the quoted path passes through
untouched. Only MSBuild's `{}` auto-name placeholder needs `-bl:{{}}` there, and nothing here uses it.
Capture per `dotnet-msbuild:binlog-generation`, then analyse
with `dotnet-msbuild:binlog-failure-analysis` — the plugin's `binlog` MCP tools are installed
(`binlog_overview`, `binlog_diagnose`, `binlog_errors`, …). **Never `cat` or `strings` a `.binlog`.**

Detail that matters for this project: `binlog_errors` with `include_task_output=true` is what turns
`MSB3073: "cmd" exited with code 123` into the sentence that explains it. The vswhere failure in
Step 1 is exactly that shape.

## Known failure modes

| Symptom | Cause | Action |
|---|---|---|
| `MSB3073` / exit 123 / `Native.targets(396,5)` / `'vswhere.exe' is not recognized` | `vswhere.exe` not on PATH | Step 1. Not a code problem. |
| `MSB3073` / **exit code 1** / `Native.targets(330,5)`, with `System.IO.IOException … native\GitlabMCP.sourcelink … being used by another process` at `ILCompiler.SourceLinkWriter` in the log | Transient lock on an ILC intermediate — AV scanner, or the previous `ilc` process still holding the file. Line 330 is the `ilc` Exec, **not** the linker: this is neither a PATH problem nor a code problem. | Re-run — but read the last row first, because the recovery run does not re-run ILC. |
| `error : Cross-OS native compilation is not supported.` at `Native.Publish.targets(60,5)` | Publishing a `linux-*` RID from Windows | Step 4 — build the Linux RIDs in Docker. |
| Exit 0 but the vswhere error is still printed | Warm `obj/` — link step skipped | Delete `bin/Release` + `obj/Release`, re-run. The previous verdict was fake. |
| Exit 0, `#14 CACHED`, no publish output in the docker log | BuildKit layer cache | `docker build --no-cache`. |
| Build succeeds, `0 Erreur(s)`, `2 Avertissement(s)` with IL2026/IL3050 | Reflection-based JSON reached by the analyzers | Gate FAIL despite exit 0 → `dotnet-upgrade:dotnet-aot-compat`. |
| **Nonzero exit, log ends at `Generating native code`, no error line anywhere** — exit **1** three times, exit **127** once, same 4-line log either way | **Cause not established**, but the trigger conditions are the same as the `(330,5)` row — first full ILC run after deleting `obj/Release`, machine busy with a concurrent Docker build — so a lost or locked ILC intermediate is the leading hypothesis. Narrowed once: `obj/Release/net10.0/win-x64/native/GitlabMCP.obj` (~149 MB) **was already on disk**, so ILC itself had finished and the *link* step is what died — which is why resuming works. Observed four times across sessions and both shells; a later clean run succeeded unchanged every time. Intermittent, not deterministic. | Re-run **without** deleting: that recovers a usable binary, but it only re-links the native object ILC had already written — the log contains **no** `Generating native code`, so Step 3 correctly scores it `GATE: INVALID` and **it must not be recorded as a pass**. Once it succeeds, delete `bin/Release` + `obj/Release` and run the full gate again for the verdict. If it recurs, capture `-bl:` for `dotnet-msbuild:binlog-failure-analysis`; do not call it a code regression until a binlog says so. |

## Why the exe got bigger

Track the number, do not chase it. `GitlabMCP.exe` is **24,549,888 B** today, the musl binary
**23,106,152 B** and the glibc binary **23,106,400 B**; a few hundred KB of movement from adding tool
classes is normal, several MB is a new dependency dragging a subsystem in with it. First check what changed
in `<PackageReference>`, then whether a new root kept a whole assembly alive.

**How reproducible the number actually is — this is what the stale-baseline disagreement was about.** An
earlier session recorded **24,549,376 B**; 512 bytes, one file-alignment block, is what *cross-session* noise
looks like here. *Within* a session the output is byte-reproducible: three `win-x64` publishes (two clean,
one resumed) produced exactly 24,549,888 B each, and two independent `--no-cache` glibc builds produced
byte-identical artifacts. So treat
sub-KB drift as noise — but do not compute a delta against 24,549,376 B at all. It is retired; 24,549,888 B
is the baseline, and `aot-gatekeeper`'s report template naming the old one is what manufactures a `+512 B`
delta out of a clean run.

Two tools, two different questions:

- `binlog_compare` (over two binlogs from `dotnet-msbuild:binlog-generation`) answers **what changed** —
  it diffs MSBuild properties and per-project package versions. It does **not** measure the executable
  and cannot attribute bytes to an assembly.
- For attribution, make ILC report its own sizes: publish with `-p:IlcGenerateMstatFile=true` and/or
  `-p:IlcGenerateMapFile=true`. Both are real properties — `Microsoft.NETCore.Native.targets` 10.0.12
  lines 248-249 turn them into ILC's `--map` and `--mstat` args. Verified on a clean `win-x64` publish:
  they drop `GitlabMCP.mstat` (19.8 MB) and `GitlabMCP.map.xml` (76 MB) into
  `obj/Release/net10.0/win-x64/native/`, and change neither the exit code nor the exe — 24,549,888 B
  with and without.

Binary size is explicitly **out of scope** for `dotnet-upgrade:dotnet-aot-compat`, and neither
`dotnet-diag:analyzing-dotnet-performance` (static anti-pattern scan) nor `dotnet-diag:microbenchmarking`
(BenchmarkDotNet) measures it — reach for those only if the question turns out to be *throughput or
allocation*, not size.

## Checklist

- [ ] `vswhere.exe` prepended to PATH in this shell (Windows).
- [ ] `bin/Release` and `obj/Release` deleted before the run.
- [ ] `dotnet publish -c Release -r win-x64` exited **0**.
- [ ] The log contains `Generating native code` — the run was real. A run that only re-linked is
      `GATE: INVALID`, never a pass, however plausible the exe it produced.
- [ ] `(warning|error) IL\d{4}` matches **zero** lines.
- [ ] `bin/Release/net10.0/win-x64/publish/` holds exactly one executable and no runtime assemblies.
- [ ] Any size delta was taken against **24,549,888 B**, not the retired 24,549,376 B.
- [ ] `docker build --no-cache .` (musl) exited 0 with zero IL codes — whenever the container is in scope.
- [ ] `docker build --no-cache --build-arg RID=linux-x64 …` (Step 4) exited 0 with zero IL codes — before
      a release, or whenever the glibc image ships or a package reference changes.
- [ ] No RID added to `<RuntimeIdentifiers>` without a publish that proves it; **no arm64**.
- [ ] No `#pragma warning disable IL*` and no `[UnconditionalSuppressMessage]` anywhere in the diff.
