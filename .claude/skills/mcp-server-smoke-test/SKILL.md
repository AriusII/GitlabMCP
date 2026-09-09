---
name: mcp-server-smoke-test
description: >
  Prove the GitlabMCP server answers MCP JSON-RPC over HTTP, not just that it builds. USE FOR: driving
  initialize / tools/list / tools/call by hand (curl, Invoke-RestMethod), "is the server up", "tools/list is
  empty", "my tool is missing", asserting a profile's tool set, GitlabMCP.http, .mcp.json or a VS Code
  server entry, ports 9080/9443/5000, Accept and MCP-Protocol-Version headers, HttpServerSessionMode,
  text/event-stream, 406/415/405, first contact with real GitLab and its PAT. DO NOT USE FOR: running tests
  or writing test code (use dotnet-test:run-tests, dotnet-test:code-testing-agent), deciding which tools a
  profile grants (use mcp-profile-gating), writing tool methods or their descriptions (use
  mcp-tool-authoring), the AOT publish (use `aot-publish-gate`; an IL warning,
  dotnet-upgrade:dotnet-aot-compat), container build, base image, in-container ports (use
  `docker-aot-image`), token scope or leakage and prompt-injection handling of GitLab-authored text (use
  `mcp-untrusted-content`).
---

# MCP Server Smoke Test

Protocol-level verification of a running GitlabMCP server. A green `dotnet build` proves nothing about the
wire: tools are registered at DI time, schemas are generated at startup, and a broken tool still returns
**HTTP 200**. The only proof is a JSON-RPC round trip.

## When to Use This Skill

- After changing `Program.cs`, a `Tools/` class, the JSON context, or profile registration.
- When a client (Claude Code, VS Code, or any MCP host) connects but shows no tools, or the wrong ones.
- Before and after wiring a real GitLab token, to separate "server broken" from "GitLab said no".
- As the acceptance gate for a profile: the advertised tool set must match its snapshot exactly.

## When Not to Use

- The build or AOT publish is failing — nothing is listening; fix that first with `aot-publish-gate`.
- You want automated tests. `tests/GitlabMCP.Tests/` exists (pure unit tests: `ResolveProfile`,
  `Grant.IsVisibleIn`, `GitLabGraphQlEndpoint.Resolve`) — see *Where Automated Tests Go* for what it
  does and, more importantly, does not cover. This skill's curl/`.http` sequence is still the only thing
  that exercises the actual wire protocol.

## The Transport, As Configured

`GitlabMCP/Program.cs` calls `.WithHttpTransport(o => o.Stateless = true)` and `app.MapMcp()` with no route
pattern. Everything below was observed against this tree, not inferred.

| Fact | Value |
|---|---|
| Endpoint | `POST /` **only**. `/mcp` → 404, `/sse` → 404 (legacy SSE is off). |
| Required request headers | `Content-Type: application/json`, or `415`. `Accept` is required too and must match **both** `application/json` and `text/event-stream` — a wildcard counts (`*/*` and `text/*, application/*` both pass), but only one of the two, or **no `Accept` header at all**, is `406`. |
| Response shape | Two layers that look nothing alike. **JSON-RPC-layer** responses — results *and* `-326xx` errors such as `-32601` / `-32602` — are HTTP `200` + `text/event-stream`, framed `event: message` / `data: {json}`. **Transport-layer** rejections — every non-`200`, non-`202` status (`400`, `406`, `415`) — are plain `application/json; charset=utf-8` carrying the signal in the HTTP status, and **never** appear as an SSE frame. Anything that only greps `^data: ` prints nothing at all for those, which is indistinguishable from a healthy `202`. The **code alone does not tell you the layer** — `-32602` shows up in both, as an unknown-tool result at `200` and as a `400` when `2026-07-28` validation rejects the request. Branch on the status. |
| Notifications (no `id`) | `202 Accepted`, `Content-Length: 0`. |
| `GET /` and `DELETE /` | `405 Method Not Allowed`, `Allow: POST` — a consequence of the stateless session mode, not of `MapMcp()`. |
| Session | No `Mcp-Session-Id` is issued. Each request builds a fresh server context, so **`initialize` is not a prerequisite** for `tools/list` — you can call it cold (verified: `200`). Under either *stateful* mode a cold `tools/list` is `400` instead. |
| Session mode | `HttpServerSessionMode.Stateless`. `Program.cs`'s `options.Stateless = true` is a convenience proxy for exactly that — see the mode table below before you touch it. |
| `MCP-Protocol-Version` | Optional, but **validated when present**. `2025-11-25`, `2025-06-18`, `2025-03-26` and `2024-11-05` all return `200`; an unrecognised value gets `400` / `-32022`. Send `2025-11-25` — stateless mode has no handshake to carry the version, and `GitlabMCP.http` already does. `2026-07-28` is a different wire contract, not just a newer number: on top of the header it demands an `Mcp-Method` header (`-32020`) **and** two `params._meta` keys — `io.modelcontextprotocol/protocolVersion` and `io.modelcontextprotocol/clientCapabilities` as a JSON object — each missing one answering `400` / `-32602`, validated in that order. Supply all three and this server does answer normally (its `tools/list` result then carries extra `ttlMs` / `cacheScope` / `resultType` fields), but every hand-rolled request in this skill omits them, so do not reach for it. |
| Ports | `launchSettings.json` profile `http` → `http://localhost:9080`; profile `https` → `https://localhost:9443` **and** `http://localhost:9080`. |
| What ships | `launchSettings.json` is **not published**. On the host, `dotnet run --project GitlabMCP --no-launch-profile` binds `http://localhost:5000`. The container does **not**: `Dockerfile` sets `ASPNETCORE_HTTP_PORTS=9080`, so the image listens on **9080**. Read that env var before assuming a port. |

### `HttpServerSessionMode` — the three modes

`ModelContextProtocol.AspNetCore` 2.2.0 added `HttpServerTransportOptions.SessionMode`, and
`HttpServerTransportOptions.Stateless` is now documented as a **convenience proxy** over it: reading returns
`true` only when `SessionMode` is `Stateless`, assigning `true` selects `Stateless` and `false` selects
`Stateful`, and because both write the same field **the last assignment wins**. It is *not* `[Obsolete]` —
`Program.cs`'s `options.Stateless = true` is current and means exactly
`SessionMode = HttpServerSessionMode.Stateless`. What the boolean cannot express is the third mode, which
also reads `false`, so anything that *inspects* the mode must read `SessionMode`.

| `SessionMode` | Session / `initialize` | `GET` + `DELETE /` | sampling · elicitation · roots |
|---|---|---|---|
| `Stateless` (`= 0`, the default, **what this server runs**) | no `Mcp-Session-Id`; `initialize` optional, `tools/list` answers cold | `405` | **disabled** — the server can make no request to the client; the SDK points at MRTR instead |
| `Stateful` (`= 1`) | `Mcp-Session-Id` issued; `initialize` required — a cold `tools/list` is `400` | `200` with the session header, `400` without | available |
| `StatefulForInitializeClients` (`= 2`) | both at once: a session for `initialize` clients, per-request for `2026-07-28` clients | legacy half `200`/`400`; a `2026-07-28` `GET` is `405` | available **only** to the session half |

Measured against 2.2.0 by running the same binary in each mode. Two wire tells: both stateful modes add
`"listChanged":true` to `initialize`'s `capabilities.tools`, and `Stateful` alone refuses a well-formed
`2026-07-28` request with `-32022` (`StatefulForInitializeClients` serves it statelessly).

For a smoke test the consequence is narrow but absolute: **this server can never sample, elicit or ask for
roots**, so a client waiting on one of those is misconfigured, not slow. Changing the mode is
`mcp-profile-gating`'s decision, not this skill's — its per-request `ConfigureSessionOptions` gate is only
sound under `Stateless`.

**Use the `http` profile locally.** The template's own generic README (since removed as dead template cruft
— it never described this project) recorded under *Known issues*, template-authored and not re-verified
here, that VS Code cannot connect to `https://localhost:9443` even with a trusted dev certificate
([microsoft/vscode#248170](https://github.com/microsoft/vscode/issues/248170)), while
`http://localhost:9080` succeeds. Whatever its current status, nothing is lost by following it: `curl` on
this machine completes a `ping` against 9443 with no `-k`, so the dev cert itself is fine, and the `https`
profile also binds 9080. You never need 9443 for a smoke test.

## Critical Rules

| Rule | Why |
|---|---|
| **`Accept` must list both media types.** | Only `application/json` → `406` `{"error":{"code":-32000,"message":"Not Acceptable: Client must accept both application/json and text/event-stream"}}`. This is the #1 hand-rolled-request failure. |
| **Parse the body as SSE, not JSON.** | `Invoke-RestMethod` returns a `System.String` for a `200`/`text/event-stream` reply; it does **not** give you an object. Strip the `data: ` prefix first. (On a transport-layer `400`/`406` it *does* return a `PSCustomObject`, because that reply is `application/json` — so the return type flips with the status. Branch on the status, not on the type.) |
| **HTTP 200 does not mean the tool worked.** | A throwing tool returns `200` with `"isError":true` inside `result`. `curl --fail` and `%{http_code}` will not catch it. Assert on the body. |
| **A failed tool call tells you nothing on the wire.** | The SDK redacts non-`McpException` messages to `"An error occurred invoking 'x'."`. The real exception and stack are **only** in the server's console/log. Always have the log open. |
| **Never assert an empty expectation.** | A snapshot check whose expected file is empty passes when the server is down. Assert, in order: the request completed; the status was `200` (not a transport-layer rejection); an SSE frame is present; the envelope is not an `error`; the tool list is non-empty. Skip any one of those and "no output" starts reading as "pass". |
| **Do not smoke-test production GitLab data with a write-capable token.** | Use a scratch project and a `read_api` token. Scope selection, leakage and scrubbing rules live in `mcp-untrusted-content`; Step 5 covers only how to read the result. |

## Workflow

### Step 1: Start the server and wait until it answers

Do not `sleep 5` and hope. Poll `ping`, the cheapest MCP-level probe.

```bash
cd /c/Users/Arius/RiderProjects/GitlabMCP
dotnet run --project GitlabMCP --launch-profile http > /tmp/gitlabmcp.log 2>&1 &
for i in $(seq 1 40); do
  curl -s -o /dev/null -m 1 -X POST http://localhost:9080/ \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json, text/event-stream' \
    -d '{"jsonrpc":"2.0","id":1,"method":"ping"}' && { echo "up after ${i}s"; break; }
  sleep 1
done
```

```powershell
Set-Location C:\Users\Arius\RiderProjects\GitlabMCP
$log = "$env:TEMP\gitlabmcp.log"
$srv = Start-Process dotnet -ArgumentList 'run','--project','GitlabMCP','--launch-profile','http' `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err" -NoNewWindow -PassThru
foreach ($i in 1..40) {
    try {
        Invoke-RestMethod -Uri 'http://localhost:9080/' -Method Post -TimeoutSec 1 `
            -Headers @{ Accept = 'application/json, text/event-stream' } `
            -ContentType 'application/json' `
            -Body '{"jsonrpc":"2.0","id":1,"method":"ping"}' | Out-Null
        "up after ${i}s"; break
    } catch { Start-Sleep 1 }
}
```

A healthy `ping` returns `data: {"result":{},"id":1,"jsonrpc":"2.0"}`.

Keep the log where you can read it — by the Critical Rule above, a failed `tools/call` says nothing on the
wire and everything in that file. `tail -f /tmp/gitlabmcp.log`, or `Get-Content $log -Wait`. Stop the
server with `Stop-Process -Id $srv.Id -Force` (verified: this frees 9080). Fall back to
`netstat -ano | findstr :9080` + `taskkill /PID <pid> /F` only for a server someone else started.

### Step 2: Drive the sequence by hand

Define the helper once, then all four calls are one-liners.

```bash
# Git Bash. Override the endpoint with MCP_URL for the container or a non-default port.
# Captures the status code instead of piping curl into sed: a transport-layer rejection is
# plain JSON, so a bare `curl | sed -n 's/^data: //p'` would print NOTHING and still exit 0.
mcp() {
  local method="$1" params="${2:-}" id='"id":1,' payload resp code body
  # A notification carries NO id. Send one and the server answers
  # -32601 "Method 'notifications/initialized' is not available." instead of 202.
  case "$method" in notifications/*) id='' ;; esac
  if [ -n "$params" ]; then payload="{\"jsonrpc\":\"2.0\",${id}\"method\":\"$method\",\"params\":$params}"
  else                    payload="{\"jsonrpc\":\"2.0\",${id}\"method\":\"$method\"}"; fi
  resp=$(curl -sS -w $'\n%{http_code}' -X POST "${MCP_URL:-http://localhost:9080/}" \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json, text/event-stream' \
    -H 'MCP-Protocol-Version: 2025-11-25' \
    -d "$payload") || { echo "curl failed (exit $?)" >&2; return 1; }
  code=${resp##*$'\n'}; body=${resp%$'\n'*}
  case "$code" in
    202) return 0 ;;                                                  # notification, no body
    200) printf '%s\n' "$body" | sed -n 's/^data: //p' ;;             # SSE frame
    *)   printf 'HTTP %s (not an SSE response): %s\n' "$code" "$body" >&2; return 1 ;;
  esac
}

mcp initialize '{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"curl","version":"1.0"}}'
mcp notifications/initialized      # 202, empty body — prints nothing. That is correct.
mcp tools/list
mcp tools/call '{"name":"get_random_number","arguments":{"min":1,"max":6}}'
```

```powershell
function Invoke-Mcp {
    param([Parameter(Mandatory)][string]$Method, $Params, [string]$Url = 'http://localhost:9080/')
    $payload = @{ jsonrpc = '2.0'; method = $Method }
    # A notification carries NO id. Send one and the server answers -32601 instead of 202.
    if ($Method -notlike 'notifications/*') { $payload.id = 1 }
    if ($PSBoundParameters.ContainsKey('Params')) { $payload.params = $Params }
    # -SkipHttpErrorCheck + -StatusCodeVariable (PowerShell 7) surface the transport-layer
    # rejections instead of throwing; they are plain JSON, not SSE, so branch on the status.
    $raw = Invoke-RestMethod -Uri $Url -Method Post -ContentType 'application/json' `
        -Headers @{ Accept = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-11-25' } `
        -Body ($payload | ConvertTo-Json -Depth 10 -Compress) `
        -SkipHttpErrorCheck -StatusCodeVariable code
    if ($code -eq 202) { return }                        # notification, no body
    if ($code -ne 200) {
        Write-Error "HTTP $code (not an SSE response): $($raw | ConvertTo-Json -Depth 10 -Compress)"
        return
    }
    # text/event-stream: Invoke-RestMethod hands back a String. Unwrap the data: frames first.
    ([string]$raw -split "`n" | Where-Object { $_ -like 'data: *' } | ForEach-Object { $_.Substring(6) }) -join '' |
        ConvertFrom-Json
}

(Invoke-Mcp 'initialize' @{ protocolVersion='2025-11-25'; capabilities=@{}; clientInfo=@{ name='pwsh'; version='1.0' } }).result
Invoke-Mcp 'notifications/initialized'      # 202, empty body - returns nothing. That is correct.
(Invoke-Mcp 'tools/list').result.tools.name
(Invoke-Mcp 'tools/call' @{ name='get_random_number'; arguments=@{ min=1; max=6 } }).result.content[0].text
```

Reference responses from the current (template) tree:

```text
initialize  {"result":{"protocolVersion":"2025-11-25","capabilities":{"logging":{},"tools":{}},
                       "serverInfo":{"name":"GitlabMCP","version":"1.0.0.0"}},"id":1,"jsonrpc":"2.0"}
tools/list  {"result":{"tools":[{"name":"get_random_number","description":"...","inputSchema":{...}}]},"id":1,...}
tools/call  {"result":{"content":[{"type":"text","text":"3"}]},"id":1,"jsonrpc":"2.0"}
```

`initialize`'s `capabilities` is the fastest structural check there is: `"tools":{}` present means tools are
wired. If you later register prompts or resources and they do **not** appear there, registration never ran.

**Then `tools/call` every advertised tool once — this is not optional.** A clean `tools/list` is no longer
evidence that the serialization context is complete. A missing `[JsonSerializable]` entry for a type that is
only ever reached through `GitLabContent.Wrap<T>` never reaches schema generation, so the server starts, lists
all its tools, and throws only when that one tool is first called (troubleshooting check 6b). Measured on
2.2.0: the wire shows nothing but `"An error occurred invoking 'x'."`, and only the server log carries
`NotSupportedException: JsonTypeInfo metadata for type 'X'`. Loop the tool names out of `tools/list` and call
each one; a tool nobody called is a tool nobody has tested.

That failure needs reflection-based serialization to be **off**, which is why it is a real risk here and not a
theoretical one: `GitlabMCP.csproj` sets `PublishAot=true`, and that alone puts
`"System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": false` into `GitlabMCP.runtimeconfig.json` —
so plain `dotnet run` behaves exactly like the published native binary. Verified both ways: with the switch
off the call succeeds and silently hides the missing entry; with it on you get the redacted error above.

### Step 3: Extend `GitlabMCP.http` as the living smoke-test file

`src/GitlabMCP/GitlabMCP.http` carries `@HostAddress`, both required headers, `ping`/`initialize`,
`tools`/`prompts`/`resources` `list`, a `resources/read` on `server_info`, and one `tools/call`
(`gitlab_ping`) — a live, tracked file, not a template leftover. Grow it rather than keeping recipes in
shell history: Visual Studio 2022 and Rider both run these in-IDE. Separate requests with `###`, name
them, and keep the handshake order.

```http
@HostAddress = http://localhost:9080

### ping - liveness
POST {{HostAddress}}/
Accept: application/json, text/event-stream
Content-Type: application/json
MCP-Protocol-Version: 2025-11-25

{ "jsonrpc": "2.0", "id": 1, "method": "ping" }

### tools/list - the profile's advertised surface
POST {{HostAddress}}/
Accept: application/json, text/event-stream
Content-Type: application/json
MCP-Protocol-Version: 2025-11-25

{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }

### tools/call - one request per tool, with realistic arguments
POST {{HostAddress}}/
Accept: application/json, text/event-stream
Content-Type: application/json
MCP-Protocol-Version: 2025-11-25

{
  "jsonrpc": "2.0", "id": 3, "method": "tools/call",
  "params": { "name": "get_project", "arguments": { "project": "gitlab-org/gitlab" } }
}
```

House rules for that file: **one request per tool**, arguments a reviewer can recognise as real, and a
`###` comment naming the profile the request belongs to. **Never put a token literal in it.**

The token goes in an environment file named `http-client.env.json`, resolved from the `.http` file's own
directory upward. Two ways to keep it out of git, both documented by Microsoft:

- `http-client.env.json.user`, a sibling whose values override the shared file. `.gitignore` line 9 is
  `*.user`, so `git check-ignore` already excludes it — verified. This is the documented place for
  user-specific values; the plain `http-client.env.json` is *meant* to be committed.
- Better here, because `GitlabMCP.csproj` already carries a `UserSecretsId`: keep no secret in any file and
  point the variable at ASP.NET Core user secrets.

```json
{ "dev": { "HostAddress": "http://localhost:9080",
           "Token": { "provider": "AspnetUserSecrets", "secretName": "GitLab:AccessToken" } } }
```

Referenced from the `.http` file as any other variable — `PRIVATE-TOKEN: {{Token}}`. Note that a variable
defined in the `.http` file **overrides** the environment file, so an `@Token = ...` line silently wins:
do not leave one there.

### Step 4: Snapshot the profile's advertised tool set

This is the assertion that matters most. A profile is defined by *what `tools/list` returns*, so pin it:
one plain-text file per profile holding the sorted tool names, diffed on every run. Adding a tool without
updating the snapshot is the failure you want, not a nuisance. Gating semantics — how a tool ends up in or
out of the collection — belong to `mcp-profile-gating`; this step only asserts the result.

```bash
# tools-snapshot.sh <expected-file>   - exits non-zero on any drift.
set -euo pipefail
URL="${MCP_URL:-http://localhost:9080/}"
EXPECTED="$1"
resp=$(curl -sS -w $'\n%{http_code}' -X POST "$URL" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -H 'MCP-Protocol-Version: 2025-11-25' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}') \
  || { echo "FAIL - no response from $URL (curl exit $?)"; exit 1; }
code=${resp##*$'\n'}; raw=${resp%$'\n'*}
[ "$code" = 200 ] || { echo "FAIL - HTTP $code (transport rejection, not an SSE response)"; echo "$raw"; exit 1; }
data=$(printf '%s\n' "$raw" | sed -n 's/^data: //p')
[ -n "$data" ] || { echo "FAIL - no SSE frame"; echo "$raw"; exit 1; }
# Anchored to the JSON-RPC envelope. A bare grep for '"error"' also matches a tool whose
# inputSchema has a property named `error` - plausible for a CI/job/log tool.
case "$data" in '{"error":'*) echo "FAIL - JSON-RPC error"; echo "$data"; exit 1 ;; esac
actual=$(printf '%s\n' "$data" | grep -o '"name":"[^"]*"' | sed 's/^"name":"//; s/"$//' | sort)
[ -n "$actual" ] || { echo "FAIL - zero tools advertised"; exit 1; }
diff -u "$EXPECTED" <(printf '%s\n' "$actual") && echo "PASS" || { echo "FAIL - tools/list drifted"; exit 1; }
```

Every exit path prints a line starting `FAIL - `, including the two a naive script swallows: a closed port
(`curl exit 7`) and a transport-layer rejection (`HTTP 406 …`, plain JSON, no `data:` frame). A log scraper
grepping for `FAIL` must never come up empty on a broken run. Verified: pass, drift, empty expectation,
server down, and `406` all behave.

`jq` is **not** installed on this machine — the `grep -o '"name":"..."'` extraction above is the deliberate
fallback, and it is safe for `tools/list`, whose only `"name"` keys are tool names (parameters are schema
object keys, not `name` fields). Where `jq` is available, prefer `jq -r '.result.tools[].name' | sort`.

```powershell
# Test-ToolSnapshot.ps1 -Expected <file> [-Url <url>]
param([Parameter(Mandatory)][string]$Expected, [string]$Url = 'http://localhost:9080/')
$ErrorActionPreference = 'Stop'
# Without the try, a closed port throws a raw (localized) HttpRequestException stack trace and a log
# scraper looking for FAIL sees nothing. -SkipHttpErrorCheck keeps a 4xx from throwing so we can label it.
try {
    $raw = Invoke-RestMethod -Uri $Url -Method Post -ContentType 'application/json' `
        -Headers @{ Accept = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-11-25' } `
        -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' `
        -SkipHttpErrorCheck -StatusCodeVariable code
} catch { "FAIL - no response from $Url : $($_.Exception.Message)"; exit 1 }
if ($code -ne 200) { "FAIL - HTTP $code (transport rejection, not an SSE response)"; $raw | ConvertTo-Json -Depth 10 -Compress; exit 1 }
if ([string]$raw -notmatch '(?m)^data: ') { "FAIL - no SSE frame"; $raw; exit 1 }
$json = (([string]$raw -split "`n" | Where-Object { $_ -like 'data: *' } | ForEach-Object { $_.Substring(6) }) -join '') | ConvertFrom-Json
if ($json.error) { "FAIL - JSON-RPC error $($json.error.code): $($json.error.message)"; exit 1 }
$actual = @($json.result.tools.name | Sort-Object)
if ($actual.Count -eq 0) { "FAIL - zero tools advertised"; exit 1 }
$want    = @(Get-Content $Expected | Where-Object { $_ -ne '' } | Sort-Object)
$missing = @($want   | Where-Object { $_ -notin $actual })
$extra   = @($actual | Where-Object { $_ -notin $want   })
if ($missing.Count -or $extra.Count) {
    "FAIL - tools/list drifted from $Expected"
    $missing | ForEach-Object { "  - missing:    $_" }
    $extra   | ForEach-Object { "  + unexpected: $_" }
    exit 1
}
"PASS - $($actual.Count) tools match $Expected"
exit 0   # without this, $LASTEXITCODE is empty on success and CI cannot branch on it
```

**Where the snapshots live is `mcp-profile-gating`'s decision, not this skill's** — it owns the golden lists.
Its L1 asserts `ProfileCatalog.ToolsFor(profile)` against a committed list of names, and its L2 asserts that
the same set comes back from `tools/list` over the transport. The script above *is* L2's transport half, so it
must read that same file: **one golden list per profile, two readers.** A rival copy under this skill would
drift, and the drift would show up as a passing test and a broken server.

Until the test project lands there is no committed location, so take the path as an argument (`$1` /
`-Expected`) and do not hard-code one here. When `mcp-profile-gating` fixes the location, these scripts point
at it unchanged.

Beyond names, three properties are worth asserting once real profiles exist and are cheap to bolt onto the
same script: every tool's `description` is non-empty; every read tool carries
`annotations.readOnlyHint: true` (the SDK omits an unset hint entirely, so a **missing `annotations` object
on a read tool** is the signal — not a `false` value); and **no tool publishes an `outputSchema`**.

That last one pairs with an assertion on the call side, and this skill is where it belongs because this is
where the wire is actually read. A tool wrapped per `mcp-untrusted-content` must come back from `tools/call`
with `content[0]` the static preamble and `content[1]` a `<gitlab-data nonce="…" source="…">` block whose
closing tag repeats the **same** nonce, and with **no** `structuredContent` key at all. Both checks are one
line each, and together they catch a stray `UseStructuredContent = true` — which builds with zero warnings,
is invisible to `tools/list` names, and is what `mcp-untrusted-content` forbids.

### Step 5: Verify against a real GitLab

Two failure domains stack here. Separate them: get `tools/list` green with no token at all, **then** add the
token and re-run one read-only `tools/call`.

Configuration — verified behaviour of `GitLab.Client` 1.0.0:

| Setting | Default | Rule |
|---|---|---|
| `BaseAddress` | `https://gitlab.com/api/v4/` | Absolute http(s) URI whose path **ends with `/`**. Self-managed: `https://gitlab.example.com/api/v4/`. |
| `AccessToken` | *(none)* | Must be non-blank. Supply by environment variable or mounted secret only. |
| `AuthenticationMode` | `PersonalAccessToken` | `PersonalAccessToken` → `PRIVATE-TOKEN`; `OAuthBearer` → `Authorization: Bearer`; `JobToken` → `JOB-TOKEN`. |
| `Timeout` | `00:01:40` | Positive, or `Timeout.InfiniteTimeSpan`. |
| `UserAgent` | `GitLab.Client/1.0` | Must be a valid product-token list. |

**The trailing slash is enforced, not silent.** `AddGitLabClient` wires `ValidateOnStart`, so a bad
`BaseAddress` throws during `app.Run()` and the server never listens:

```text
OptionsValidationException: BaseAddress must be an absolute http(s) URI whose path ends with '/',
                            e.g. 'https://gitlab.example.com/api/v4/'.
```

So: **if the server is listening, `BaseAddress` already passed validation**, and a startup crash with that
message is the answer, not a mystery. What the trailing slash protects you from — and what still bites
anywhere you compose a `Uri` or set `HttpClient.BaseAddress` by hand — is relative-URI resolution silently
eating the last segment:

```text
new Uri("https://gitlab.example.com/api/v4/", "projects/42/issues")
  -> https://gitlab.example.com/api/v4/projects/42/issues     CORRECT
new Uri("https://gitlab.example.com/api/v4",  "projects/42/issues")
  -> https://gitlab.example.com/api/projects/42/issues        404, and the URL never appears in a log
```

**Confirm the auth header without a real token or a real GitLab.** Point `BaseAddress` at a throwaway local
echo endpoint and read what goes out; this also proves route composition and percent-encoding. Observed with
`AuthenticationMode = PersonalAccessToken` and `IProjectsClient.GetAsync("grp/proj")`:

```text
path  /api/v4/projects/grp%2Fproj          <- ProjectId percent-encodes namespace/path for you
PRIVATE-TOKEN: <token>
User-Agent: GitLab.Client/1.0
```

For the smoke test itself, use a scratch project and a `read_api` project access token; everything else
about scope choice, token type and keeping the token out of logs and error text belongs to
`mcp-untrusted-content`.

Interpreting what GitLab sends back is smoke-test work, and the three failures are not interchangeable:
`401` is a bad or expired token; `403` means the token lacks rights — a correct result, not a bug, and
retrying will not help; `404` from GitLab means *not found **or** not visible to this token* — never report
it as "does not exist" (`mcp-untrusted-content` §Step 7 owns that wording).

### Step 6: Point a client at it

The two config shapes are **not** interchangeable — the top-level key differs.

Claude Code writes `.mcp.json` with `mcpServers`:

```json
{ "mcpServers": { "GitlabMCP": { "type": "http", "url": "http://localhost:9080" } } }
```

```powershell
claude mcp add --transport http --scope project GitlabMCP http://localhost:9080
# --scope project writes .mcp.json into the repo; the default (local) keeps it out of the tree.
# Add headers with -H "Name: value" once the endpoint requires auth.
```

VS Code / Visual Studio use `servers`, exactly as `GitlabMCP/README.md` documents:

```json
{ "servers": { "GitlabMCP": { "type": "http", "url": "http://localhost:9080" } } }
```

Start the server **before** the client connects. A stateless HTTP server can do nothing for a client that
connected to a closed port, and most clients cache the failure until reload.

## Troubleshooting: `tools/list` Is Empty or a Tool Is Missing

Work down this list; each step rules out a different cause.

| # | Check | Signal | If it fails |
|---|---|---|---|
| 1 | Is anything listening on the port you are testing? | `netstat -ano \| findstr :9080` | Wrong port. On the host, `--no-launch-profile` binds `:5000`. The container binds `:9080` — `Dockerfile` sets `ASPNETCORE_HTTP_PORTS=9080`. `ASPNETCORE_URLS` overrides either. |
| 2 | Are you POSTing to `/`? | `/mcp` and `/sse` return **404**, not an MCP error | `MapMcp()` is called with no pattern. Use `/`. |
| 3 | Did the server declare a tools capability? | `initialize` → `capabilities` contains `"tools":{}` | No `"tools"` key means no `WithTools<T>()` ran at all. |
| 4 | Is the class registered? | `Program.cs` must name it: `.WithTools<YourTools>(GitLabJson.Options)` | Assembly scanning is banned by the AOT rules in `CLAUDE.md`, so nothing is discovered implicitly — a tool class that is never named simply does not exist. |
| 5 | Did the profile filter remove it? | Compare `tools/list` against the profile's grant list | Gating is working as designed; fix the grant, not the tool. See `mcp-profile-gating`. |
| 6 | Did schema generation fail? | Server **fails at startup**: `NotSupportedException: JsonTypeInfo metadata for type 'X' was not provided...` | Fires for every non-primitive **parameter** type and for a **record return type** — both are schema-generated while `WithTools<T>` builds the tool, so the process dies before it listens. Each needs a `[JsonSerializable]` entry in the project's `JsonSerializerContext`. See `mcp-tool-authoring` Step 6. |
| 6b | …or did it never generate one? | Server **starts**, `tools/list` is complete, and one tool fails on **every** `tools/call` with `"An error occurred invoking 'gitlab_x'."` | A payload type reached only through `GitLabContent.Wrap<T>` is never schema-generated, so its missing `[JsonSerializable]` entry surfaces on first call instead of at startup, thrown from `McpJsonUtilities.GetTypeInfo<T>`. Nothing on the wire names the type — the server log does. This is why Step 2 makes `tools/call` on every tool mandatory. |
| 7 | Is the name what you expect? | The emitted name in `tools/list` | With `Name` unset the SDK snake_cases the method and strips a trailing `Async` **only when the method is genuinely awaitable**. Verified on 2.2.0: `TaskSuffixAsync`/`Task<string>` → `task_suffix` and `ValueTaskSuffixAsync`/`ValueTask<string>` → `value_task_suffix`, but `SyncSuffixAsync`/`string` → **`sync_suffix_async`** — the suffix stays. Acronyms mangle too (`ListMRs` → `list_m_rs`). Always set `Name` explicitly; `mcp-tool-authoring` owns the full table. |

Symptoms that look like a missing tool but are not. The first three arrive as HTTP `200` + an SSE `data:`
frame; the last four are plain `application/json` at the HTTP status shown and have **no** `data:` frame at
all, so read the status before you read the body:

| Response | Meaning |
|---|---|
| `-32602 "Unknown tool: 'x'"` (a JSON-RPC `error`) | The name is genuinely absent from the collection. Go to checks 4-7. |
| `200` + `"isError":true` + `"An error occurred invoking 'x'."` | The tool ran and threw. The real exception is **only in the server log** — the SDK redacts anything that is not an `McpException`. |
| `-32601 "Method 'prompts/list' is not available."` | That capability was never registered — a different condition from "registered and empty". |
| `406` / `-32000 "Not Acceptable: Client must accept both application/json and text/event-stream"` | Your `Accept` header — wrong, or missing entirely. Not the server. |
| `415`, **empty body** (`Content-Length: 0`) | Your `Content-Type` is not `application/json`. This is the only response here with no body at all. |
| `400` / `-32600 "Bad Request: The POST body did not contain a valid JSON-RPC message."` | Malformed JSON, or valid JSON that is not a JSON-RPC message. The body **is** present — a 400 with content is still this. |
| `400` / `-32022` with `data.supported` | Your `MCP-Protocol-Version` value is not recognised. **`data.supported` is actively misleading here**: it lists only `["2026-07-28"]` — the one value a hand-rolled request *cannot* use (see the transport table) — while the four that do return `200` are absent from it. Ignore the list; go by the status code. |

## Where Automated Tests Go

**Update (2026-09-09):** `tests/GitlabMCP.Tests/` now exists — listed in `GitlabMCP.slnx` as
`<Project Path="tests/GitlabMCP.Tests/GitlabMCP.Tests.csproj" />` — but it covers exactly the pure,
dependency-free logic that doesn't need a running server: `GitLabMcpOptionsExtensions.ResolveProfile`/
`ResolveVersion`, `Grant.IsVisibleIn`, and `GitLabGraphQlEndpoint.Resolve` (self-hosted/custom-domain/
path-prefixed/non-standard-port GitLab base addresses). It does **not** yet do the `WebApplicationFactory`
in-process host testing this section's own investigation below found viable — that remains open work, and
everything below (the two verified findings, and the guidance to delegate writing/running to
`dotnet-test:code-testing-agent`/`dotnet-test:run-tests`) is still accurate for whoever picks it up:

1. Any *new* test project must be listed explicitly in `C:/Users/Arius/RiderProjects/GitlabMCP/GitlabMCP.slnx`.
   `.slnx` discovers nothing implicitly.
2. Delegate **writing** the tests to `dotnet-test:code-testing-agent` and **running** them to
   `dotnet-test:run-tests`. Do not hand-roll `dotnet test` invocations here.
3. Keep the two layers apart. In-process host tests (`WebApplicationFactory` over `Program`) cover
   registration, schema generation and the profile snapshot without binding a port. The curl /
   `Invoke-RestMethod` sequence in this skill stays as the *transport* check, and is the only thing that
   also works against the published single-file binary and the container image — keep it, and keep
   `GitlabMCP.http` current.
4. The tool-name snapshot is the first assertion to port, and it runs once per profile.

**The two things people expect to block this both turn out not to.** Measured against this tree, in a
scratch directory:

- **`WebApplicationFactory<Program>` works as-is.** `Program.cs` is top-level statements, so `Program` is
  internal — but the test compiled *and passed*, driving `tools/list` in-process and getting back
  `get_random_number`. No `public partial class Program`, no `InternalsVisibleTo`.
- **`NETSDK1151` does not fire for a test project.** `GitlabMCP.csproj` sets `SelfContained` and
  `PublishSelfContained` unconditionally, and a plain `OutputType=Exe` project referencing it *does* fail
  with `error NETSDK1151: … a self-contained executable cannot be referenced by a non-self-contained
  executable`. A test project does not: `_CalculateIsVSTestTestProject` in `Microsoft.NET.Sdk.targets`
  turns the check off whenever `IsTestProject` is `true`. Both `dotnet new mstest` (MSTest 4.0.2) and
  `dotnet new xunit` set it, and both built clean — 0 errors, 0 warnings — with nothing more than
  `<ProjectReference Include="GitlabMCP/GitlabMCP.csproj" />`.

So the first test project needs no csproj gymnastics. If you ever *do* hit `NETSDK1151` — a console tool or
a benchmark project, neither of which is `IsTestProject` — the fixes in order of preference are
`<IsTestProject>true</IsTestProject>` where it is honest, `ValidateExecutableReferencesMatchSelfContained=false`,
or making the app's `SelfContained` / `PublishSingleFile` publish-only
(`Condition="'$(_IsPublishing)' == 'true'"`) in `GitlabMCP.csproj`.

Follow-up check, not a blocker: after the test project lands, confirm `dotnet publish -r linux-musl-x64`
is still warning-free — it is a separate assembly that is never AOT-published, so the expected answer is
"unchanged", but `aot-publish-gate` is how you find out rather than assume.

## Checklist

- [ ] Server answers `ping` with `{"result":{}}` on the port you are actually testing.
- [ ] `initialize` returns `serverInfo.name == "GitlabMCP"` and `capabilities` contains `"tools":{}`.
- [ ] `notifications/initialized` returns `202` with an empty body.
- [ ] `tools/list` returns a non-empty array and matches the profile's golden list — the one
      `mcp-profile-gating` owns — exactly.
- [ ] Every advertised tool has a non-empty `description`; every read tool carries `readOnlyHint: true`.
- [ ] **Every** advertised tool was actually called, not just listed — a clean `tools/list` does not prove the
      `[JsonSerializable]` entries are complete — and each `tools/call` has `isError` absent or `false`,
      checked in the **body**, not via HTTP status.
- [ ] No tool advertises an `outputSchema`, and no `tools/call` result carries `structuredContent`; a wrapped
      tool's result is preamble + a nonce-matched `<gitlab-data>` block.
- [ ] Deliberate failure paths exercised: unknown tool name → `-32602`; a tool that throws → `200` +
      `isError`, with the real exception located in the server log.
- [ ] Whatever script you are running fails **loudly** on a transport-layer rejection: send one deliberately
      (`Accept: application/json` → `406`) and confirm you get a `FAIL` line, not silence.
- [ ] `GitlabMCP.http` has a request for every tool, and no token literal — the token comes from
      `http-client.env.json.user` or user secrets.
- [ ] Against a real instance: `BaseAddress` ends with `/`, the auth mode matches the token type, and one
      read-only call returns data.
- [ ] The client entry uses the right key for its host — `mcpServers` for Claude Code, `servers` for
      VS Code / Visual Studio.
