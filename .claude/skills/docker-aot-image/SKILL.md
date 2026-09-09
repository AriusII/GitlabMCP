---
name: docker-aot-image
description: >
  GitlabMCP's container image: a Native AOT single-file binary on a minimal x64 base, where the
  RID/libc pairing governs every other choice.
  USE FOR: writing `Dockerfile` / `.dockerignore`, SDK and `runtime-deps` tags,
  `exec /app/GitlabMCP: no such file or directory`, linux-musl-x64 vs linux-x64, Alpine vs chiseled,
  USER / APP_UID, ASPNETCORE_HTTP_PORTS, why a localhost bind is unreachable, ASPNETCORE_ALLOWEDHOSTS
  and the configuration-key index, the GitLab token by env or mounted secret, the profile per
  container, image and build-context size, NETSDK1064, HEALTHCHECK, the `docker build`/`docker run`
  loop.
  DO NOT USE FOR: a host-side publish failure, MSB3073 / vswhere, IL2xxx/IL3xxx (use
  `aot-publish-gate`, then dotnet-upgrade:dotnet-aot-compat); JSON-RPC beyond a one-request liveness
  check (use `mcp-server-smoke-test`); what a tool hands back to the model (use
  `mcp-untrusted-content`); which tools a profile grants (use `mcp-profile-gating`); arm64 — this
  repo has no arm64 container target.
---

# Docker AOT Image

The container is the shipping vehicle for this server, and it is unusual in one respect: the final
image contains **no .NET at all**. `PublishAot` + `SelfContained` make ILCompiler emit one native ELF
that already carries the GC, the BCL and the execution engine. The publish output is literally three
files and not a single managed assembly:

```text
/app/GitlabMCP                                 23,106,152 B   <- the whole app
/app/GitlabMCP.dbg                             59,489,384 B   <- separated symbols, NOT shipped
/app/GitlabMCP.staticwebassets.endpoints.json          53 B
```

(`docker build --target build`, then `ls -la /app`. The binary reproduces to the byte across builds;
the `.dbg` drifts by a few hundred bytes, so read it as "~59 MB, three times the thing you ship".)

So the runtime stage needs only the platform libraries that ELF is dynamically linked against. That is
exactly what `mcr.microsoft.com/dotnet/runtime-deps` is for. `aspnet:10.0` or `runtime:10.0` would add
a shared framework the binary can never load.

## When to Use This Skill

- Creating or editing `/Dockerfile` or `/.dockerignore` at the repo root, or bumping a base image tag.
- A container that builds fine and then dies instantly on `docker run`.
- Deciding how the GitLab token, the active profile or any other configuration key reaches a running
  container — §6 indexes all five and their environment spellings.
- A container answering `400 Bad Request - Invalid Hostname`, or accepting a `Host` it should not.
- Someone asks "why is the image 60 MB / 33 MB / neither".

## When Not to Use

- The publish fails on the Windows host (`MSB3073`, exit 123, vswhere) — that is `aot-publish-gate`.
- A publish emits `IL2026` / `IL3050` — `aot-publish-gate` decides pass/fail, then
  dotnet-upgrade:dotnet-aot-compat fixes it. Nothing here changes IL behaviour.
- You want to exercise the MCP protocol properly — `mcp-server-smoke-test`.

## Critical Rules

| Rule | Consequence if broken |
|---|---|
| **The RID and the final base must share a libc.** `linux-musl-x64` → Alpine. `linux-x64` → glibc (Ubuntu / chiselled). | `exec /app/GitlabMCP: no such file or directory`, exit 255, at `execve`. The build succeeds; the failure is at run time. |
| Build in an **`-aot` SDK tag**. | The plain SDK images have no clang, no `zlib.h`, no libc dev headers. ILCompiler cannot link. |
| `USER $APP_UID` in the final stage. | `runtime-deps:10.0-alpine` has an **empty** `Config.User` — the server runs as root. |
| `ENV ASPNETCORE_HTTP_PORTS=9080`. | Every base image ships `ASPNETCORE_HTTP_PORTS=8080`. Silently wrong port. |
| Bind the wildcard (`+`, or `ASPNETCORE_HTTP_PORTS` which implies it), never `localhost`. | Container is unreachable through `-p`. See §5. |
| **Never** put the token in an `ARG`, an `ENV` line, or a COPY'd `appsettings.*.json`. | `docker history --no-trunc` prints the value. It travels with the image. See §6. |
| `ENV ASPNETCORE_ALLOWEDHOSTS` listing **all three** loopback spellings, `127.0.0.1;localhost;[::1]`. | Without it `AllowedHosts` defaults to `*`: every `Host` header is accepted (`Host: evil.example` → 200, measured), which is the DNS-rebinding surface. It also crash-loops the image the moment `mcp-untrusted-content`'s startup check lands. Drop `[::1]` and an IPv6 loopback client gets 400. See §5. |
| The **same** `--mount=type=cache` on both `dotnet restore` and `dotnet publish`. | `error NETSDK1064: Package Microsoft.NET.ILLink.Tasks, version 10.0.12 was not found.` |
| **x64 only.** arm64 is out of scope repo-wide; `<RuntimeIdentifiers>` is `win-x64;linux-x64;linux-musl-x64`. | If a `Dockerfile.cross-arm64` is sitting in the tree, delete it — it targets a RID the csproj cannot restore. |

## 1. The libc rule

A Native AOT binary is a dynamically linked PIE with the loader path baked into its ELF header:

```console
$ MSYS_NO_PATHCONV=1 docker run --rm --user 0 --entrypoint /bin/sh gitlabmcp:musl \
    -c 'apk add -q file && file /app/GitlabMCP'
/app/GitlabMCP: ELF 64-bit LSB pie executable, x86-64, ... dynamically linked,
interpreter /lib/ld-musl-x86_64.so.1, stripped
```

`MSYS_NO_PATHCONV=1` is the Git Bash guard — without it every `docker` argument starting with `/` is
rewritten into a Windows path and the run dies before the container starts. Exact error in §4;
PowerShell needs no guard.

`/lib/ld-musl-x86_64.so.1` does not exist on a glibc image, so the kernel refuses the `execve`.
Verified by putting the musl binary on `runtime-deps:10.0` on purpose — the image builds clean:

```console
$ docker run --rm gitlabmcp:mismatch
exec /app/GitlabMCP: no such file or directory      # exit 255
$ MSYS_NO_PATHCONV=1 docker run --rm --entrypoint /usr/bin/ls gitlabmcp:mismatch -l /app/GitlabMCP
-rwxr-xr-x 1 app app 23106152 ... /app/GitlabMCP    # the file is right there
```

⚠️ **The error message names the wrong file.** "no such file or directory" is about the *loader*, not
your binary. Anyone reading it literally will start debugging `COPY` paths and find nothing wrong.
This is the single most likely way to lose an hour on this image.

The rule is symmetric — a `linux-x64` binary on `runtime-deps:10.0-alpine` fails identically. There is
no shim, no compat layer, no fallback.

## 2. Base images

Verified 2026-09-09 with `docker manifest inspect` (works registry-direct, no daemon needed) and by
probing running containers. Both `-aot` SDK images carry **SDK 10.0.401** — the same SDK the Windows
host builds with.

| Stage | Tag | What is in it |
|---|---|---|
| build, musl | `mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot` | Alpine 3.24.1, clang 22.1.3, `musl-dev`, `zlib-dev`, `build-base`, `/usr/include/zlib.h` |
| build, glibc | `mcr.microsoft.com/dotnet/sdk:10.0-aot` | Ubuntu 24.04.4, clang 18.1.3, `libc6-dev`, `zlib1g-dev`, `/usr/include/zlib.h` |
| final, musl | `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine` | busybox `sh` / `wget` / `nc`, `apk`, ca-certificates. **`Config.User` empty.** |
| final, glibc | `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled` | 67 files. No shell, no package manager, nothing under `bin/`. `Config.User=1654`. |

Both final images export `APP_UID=1654`, `ASPNETCORE_HTTP_PORTS=8080`,
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` and `DOTNET_RUNNING_IN_CONTAINER=true`. Neither ships
`/usr/share/zoneinfo`, so a `TimeZoneInfo` lookup by IANA id will fail — irrelevant today, a real
constraint the moment a tool formats a GitLab timestamp in a named zone.

**Not a tag:** `runtime-deps:10.0-alpine-chiseled` → `no such manifest`. Chiselled is Ubuntu-only.

**Do not build on the plain SDK images.** `sdk:10.0-alpine` has neither clang nor `zlib.h`.
`apk add --no-cache clang build-base zlib-dev musl-dev` does resolve, but it re-downloads hundreds of
megabytes of packages on every cold build to reach a state the `-aot` tag already ships as a cached
base layer.

Re-verify a tag before writing it into the Dockerfile:
`docker manifest inspect <tag> >/dev/null && echo OK`.

## 3. The Dockerfile

Lives at the repo root. Default build is musl/Alpine; the glibc flavour is the same file with three
build args.

```dockerfile
# syntax=docker/dockerfile:1

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine

# ----------------------------- build stage ---------------------------------
FROM ${SDK_IMAGE} AS build
ARG RID=linux-musl-x64
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION=0.0.0-dev   # baked into AssemblyInformationalVersion + ENV GitlabMcp__Version below; not a secret
WORKDIR /src

# Restore on its own layer so source edits do not re-download packages. (Simplified to one project here —
# the real Dockerfile restores all 7 src/* projects individually; see the file on disk, §below.)
COPY GitlabMCP/GitlabMCP.csproj GitlabMCP/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore GitlabMCP/GitlabMCP.csproj --runtime "$RID"

COPY GitlabMCP/ GitlabMCP/

# PublishAot / SelfContained / PublishSingleFile / InvariantGlobalization all come
# from the .csproj. Do not restate them here; two sources of truth will diverge.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish GitlabMCP/GitlabMCP.csproj \
        --configuration "$BUILD_CONFIGURATION" \
        --runtime "$RID" \
        --no-restore \
        --output /app \
        -p:Version="$APP_VERSION" -p:InformationalVersion="$APP_VERSION"

# ----------------------------- final stage ---------------------------------
FROM ${RUNTIME_IMAGE} AS final
ARG APP_VERSION=0.0.0-dev   # ARGs don't cross FROM — redeclare to export it as ENV below
USER $APP_UID
WORKDIR /app

# Name the ONE file. `COPY --from=build /app/ ./` would drag in the 59 MB .dbg
# and roughly triple the image.
COPY --from=build --chown=$APP_UID:$APP_UID /app/GitlabMCP ./GitlabMCP

ENV ASPNETCORE_HTTP_PORTS=9080
EXPOSE 9080

# HostFilteringMiddleware reads `AllowedHosts`; its default is "*". There is no
# appsettings.json, so the image is the only place to set it. Host part only,
# case-insensitive, never a port. All three loopback spellings are required. §5.
ENV ASPNETCORE_ALLOWEDHOSTS="127.0.0.1;localhost;[::1]"

ENV GitlabMcp__Version=$APP_VERSION   # §6/§7 — the one config key an operator never sets with -e

ENTRYPOINT ["/app/GitlabMCP"]
```

**This illustrates the shape, not a byte-for-byte copy** — the real Dockerfile restores/copies all 7
`src/*` projects individually rather than the one-project `COPY` shown above (DEC-018), and both differ
only in that mechanical repetition, not in structure. Read `Dockerfile` at the repo root for the exact
current text; a skill snippet that tries to stay byte-identical to a file that keeps growing (adding a
project, adding `ARG APP_VERSION`) is a guaranteed drift generator — this one already drifted once
(2026-09-09, when `APP_VERSION` landed in the real file first). The publish `RUN` no longer ends with
`&& rm -f /app/*.dbg /app/*.pdb`; that step was removed and verified to change nothing (identical
binary, identical exported rootfs), because the single-file `COPY` in the final stage is already the
`.dbg` exclusion strategy. Do not reintroduce it — it invites the belief that a directory `COPY`
would be safe.

`CLAUDE.md`'s Docker paragraph is maintained separately and is not this skill's to edit. The two
constraints it must not contradict are here: the RID↔libc pairing (§1), and token by environment or
mounted secret, never baked in (§6).

Lines that look optional and are not:

| Line | Why |
|---|---|
| `# syntax=docker/dockerfile:1` | Pins the Dockerfile frontend to the 1.x line, so this syntax stays stable across Docker versions and picks up frontend fixes. It is **not** a prerequisite for `--mount=type=cache`: the same `RUN --mount` builds fine with no directive on Docker 29.7.2 (verified, `--no-cache`, DONE 0.2s). Reproducibility insurance, not a requirement. |
| `--mount` on **both** `RUN`s | With the mount, restored packages live in the cache, not in the layer. Drop it from the publish `RUN` and you get `NETSDK1064: Package Microsoft.NET.ILLink.Tasks, version 10.0.12 was not found` — verified. |
| `--no-restore` | Without it, publish re-restores and the separate restore layer buys nothing. |
| `COPY --from=build /app/GitlabMCP` (single file) | Selective copy *is* the `.dbg` exclusion strategy. A `rm -f /app/*.dbg` step in the build stage is redundant. |
| `USER $APP_UID` **before** `WORKDIR` | `WORKDIR` creates `/app` as the *current* user. `USER` first → `/app` is `1654:1654`; the reverse leaves it `0:0`, unwritable by the app user the moment anything needs a scratch or log path there. Measured both orderings with `stat -c %u:%g`. |
| `--chown=$APP_UID:$APP_UID` on the `COPY` | Orthogonal to the ordering above, and not optional: `COPY` ignores `USER`, so without `--chown` the binary lands `0:0` even under `USER $APP_UID` (verified). It still executes at mode 0755 — but nothing in `/app` then belongs to the process. |
| `EXPOSE 9080` | Documentation plus `-P`. It publishes nothing on its own. |
| `ENV ASPNETCORE_ALLOWEDHOSTS=...` | The only place this repo can set the key — there is no `appsettings.json`. Unset means `AllowedHosts` = `*`, i.e. every `Host` header accepted. §5 has the measured matrix and the `[::1]` trap. |

## 4. Build and verify

```bash
# musl / Alpine (default)
docker build -t gitlabmcp:musl .

# glibc / chiselled
docker build -t gitlabmcp:glibc \
  --build-arg RID=linux-x64 \
  --build-arg SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-aot \
  --build-arg RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled .
```

The same command in PowerShell 7 needs backticks, not backslashes, for the line continuations;
everything else is identical (verified).

Then run one liveness request. Publish to loopback on a workstation — the endpoint is unauthenticated
and carries the GitLab token's full authority (see `mcp-untrusted-content`).

```bash
docker run -d --name gmcp -p 127.0.0.1:19099:9080 gitlabmcp:musl
docker logs gmcp | grep 'Now listening'          # => Now listening on: http://[::]:9080
docker exec gmcp id                              # => uid=1654(app) gid=1654(app)
curl -s -X POST http://127.0.0.1:19099/ \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -H 'MCP-Protocol-Version: 2025-11-25' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
docker rm -f gmcp
```

Expected shape — `Content-Type: text/event-stream`, SSE framing, not bare JSON:

```text
event: message
data: {"result":{"tools":[{"name":"get_random_number",...}]},"id":1,"jsonrpc":"2.0"}
```

One POST really is sufficient: `Program.cs` runs `Stateless = true`, so `initialize` is **not** a
prerequisite for `tools/list`, and no `Mcp-Session-Id` response header is ever issued. Verified on the
built image — `initialize` answers on its own connection too, and neither response carries a session
header. The full handshake is `mcp-server-smoke-test`'s job, not this one's.

The same request in PowerShell (`curl` is not an alias in PowerShell 7; call `curl.exe`):

```powershell
curl.exe -s -X POST http://127.0.0.1:19099/ -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' -H 'MCP-Protocol-Version: 2025-11-25' -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

> **Git Bash on Windows:** any `docker` argument starting with `/` gets rewritten into a Windows path —
> `--entrypoint /bin/sh` fails with `stat C:/Program Files/Git/usr/bin/sh: no such file or directory`.
> Prefix the command with `MSYS_NO_PATHCONV=1`. PowerShell needs no such thing.

## 5. Ports, and why `localhost` is fatal in a container

`GitlabMCP/Properties/launchSettings.json` sets `http://localhost:9080` for `dotnet run`. **It is not
published and does not ship.** The container's port comes entirely from the image's `ENV` or from
`docker run -e`.

| Setting | Result |
|---|---|
| image `ENV ASPNETCORE_HTTP_PORTS=9080` | `Now listening on: http://[::]:9080` |
| `-e ASPNETCORE_URLS=http://+:7777` (image ENV still 9080) | `http://[::]:7777` — **`ASPNETCORE_URLS` wins.** Set one, not both. |
| `-e ASPNETCORE_URLS=http://localhost:9080` with `-p 19100:9080` | `Now listening on: http://localhost:9080`, and `curl` → `http_code 000`, exit 52 |

The last row is the trap. `-p` forwards to the container's **eth0** address. A loopback bind listens
only on the container's own `lo`, so the forwarded packet arrives on an interface with nothing on it.
The container looks healthy from the inside and is dead from the outside. Bind `+` (or leave
`ASPNETCORE_HTTP_PORTS` to imply it) and control exposure with the **host** side of `-p`
(`127.0.0.1:19099:9080`), never with the bind address.

Non-root is not a constraint here: Docker's default capability set includes `CAP_NET_BIND_SERVICE`, so
even port 80 binds as uid 1654. Do not "fix" a port problem by adding privileges.

### Host filtering — `ASPNETCORE_ALLOWEDHOSTS`

`HostFilteringMiddleware` is already in the ASP.NET Core pipeline and reads the `AllowedHosts`
configuration key. Its default is `*`, i.e. **any** `Host` header accepted — the DNS-rebinding surface
`mcp-untrusted-content` Step 5 describes. This project has no `appsettings.json`, so the image is the
only place the key can be set, and the `Dockerfile` sets it (§3).

Measured against the built image, run as `-p 127.0.0.1:19099:9080`:

| `Host:` sent | Result |
|---|---:|
| `127.0.0.1:19099` | 200 |
| `localhost:19099` | 200 |
| `LOCALHOST:19099` | 200 — matching is case-insensitive |
| `[::1]:19099` | 200 — **only because `[::1]` is listed** |
| `evil.example:19099` | 400 |
| `gitlabmcp` (a compose service name) | 400 |

Three things that are easy to get wrong:

- **Entries carry no port.** The middleware matches the host part only, so a `127.0.0.1:9080` entry
  matches nothing.
- **`[::1]` is not optional.** Kestrel binds `[::]`, so a genuine IPv6 loopback client sends
  `Host: [::1]:<port>`. Omit it and that client gets 400 while `127.0.0.1` keeps working — a
  half-broken state nobody debugs quickly. Verified both ways. `mcp-untrusted-content` §Step 5
  prescribes the same three-entry value; if the two files ever disagree, the image is what runs.
- **The rejection is not JSON-RPC.** It is Kestrel's own `400 Bad Request`, `Content-Type: text/html`,
  body `<h2>Bad Request - Invalid Hostname</h2>` (334 bytes). An MCP client reports a *transport*
  failure; nothing in the protocol layer names the cause.

Reaching the container under any other name — a compose service name, the bridge IP — needs an
explicit override, and `-e` **replaces** the image `ENV` rather than adding to it:

```bash
docker run -e ASPNETCORE_ALLOWEDHOSTS="gitlabmcp;127.0.0.1" ... gitlabmcp:musl
```

Verified with exactly that value: `Host: gitlabmcp` → 200 and `Host: localhost` → 400.

The startup check that *refuses to boot* on a wildcard, and the `Origin` 403 middleware, belong to
`mcp-untrusted-content` Step 5. This `ENV` is what keeps that check from crash-looping the image on
the day it lands, so the two land together.

## 6. Configuration keys, and the GitLab token

### The key index

Every configuration key this container can carry, with the environment spelling `docker run -e` wants.
`__` is the environment encoding of the configuration `:` separator; the `ASPNETCORE_` prefix is
stripped by the host's own provider.

| Configuration key | Environment variable | Who reads it | Owner file | Status today |
|---|---|---|---|---|
| `GitLab:AccessToken` | `GitLab__AccessToken` | `GitLabClientOptions.AccessToken`, bound by `AddGitLabClient(configuration)`; required, `ValidateOnStart` fails fast if absent | `gitlab-client-navigation` §Registration and options | **Live** |
| `GitLab:BaseAddress` | `GitLab__BaseAddress` | `GitLabClientOptions.BaseAddress`; absent → `https://gitlab.com/api/v4/`. DEC-034: `GitLabBaseAddressNormalizer.Normalize` (a `PostConfigure<GitLabClientOptions>` in `Program.cs`, runs before validation) appends `api/v4/` automatically if the given URL doesn't already have an `api` segment — set this to just the bare self-hosted domain, a path-prefixed install, or a non-standard port and it works. A path that already contains `api/` but the wrong version is left alone and `ValidateGitLabClientOptions` rejects it at startup with a clear message. `GitLabBaseAddressNormalizerTests`/`GitLabGraphQlEndpointTests` pin all of this down | `gitlab-client-navigation` §Registration and options | **Live** |
| `GitLabMcp:Profile` | `GitLabMcp__Profile` | `GitLabMcpOptionsExtensions.ResolveProfile()`, applied once per request by `ProfileGate.Apply` in `Program.cs`'s `ConfigureSessionOptions`; absent → `Maintainer`, present-but-unparseable → throw at startup (`ValidateOnStart`) | `mcp-profile-gating` | **Live** |
| `GitLabMcp:Version` | `GitLabMcp__Version` | `GitLabMcpOptionsExtensions.ResolveVersion()`; **image-build-time only** — set via the Dockerfile's `ARG APP_VERSION`, never by `docker run -e`. Absent → `"dev"`. Surfaced in the `server_info` resource and the startup log line (§3, §7) | this skill (§3) | **Live** |
| `AllowedHosts` | `ASPNETCORE_ALLOWEDHOSTS` | `HostFilteringMiddleware`, already in the pipeline | `mcp-untrusted-content` §Step 5 owns the policy; **this skill ships the value** (§3, §5) | **Live** |
| `Mcp:AllowedOrigins` | `Mcp__AllowedOrigins` | the `Origin` → 403 middleware | `mcp-untrusted-content` §Step 5 | **Not written** — nothing reads it |

Only `Mcp:AllowedOrigins` still does nothing — the app half of that one contract has not landed yet;
the container half does not change when it does, which is why it can be documented now.

### Where a value can come from, and which wins

`WebApplication.CreateBuilder(args)` builds this provider chain — **later wins.** Dumped from a
`net10.0` `Microsoft.NET.Sdk.Web` app carrying this repo's own `UserSecretsId`:

```text
 0  MemoryConfigurationProvider
 1  EnvironmentVariables  Prefix: 'ASPNETCORE_'       <- ASPNETCORE_ALLOWEDHOSTS lands here
 2  MemoryConfigurationProvider
 3  EnvironmentVariables  Prefix: 'DOTNET_'
 4  appsettings.json                        (Optional)  <- does not exist in this repo
 5  appsettings.<Environment>.json          (Optional)  <- does not exist
 6  GitlabMCP.settings.json                 (Optional)  <- app-name-derived; does not exist
 7  GitlabMCP.settings.<Environment>.json   (Optional)  <- does not exist
 8  secrets.json                            (Optional)  <- Development ONLY
 9  EnvironmentVariables  (no prefix)                    <- GitLab__AccessToken, GitLabMcp__Profile, …
10  ChainedConfigurationProvider
```

Four consequences that actually bite in a container:

- **User secrets are not a container mechanism.** Slot 8 exists only when
  `ASPNETCORE_ENVIRONMENT=Development`. A container defaults to `Production`, so the same binary shows
  ten providers and no `secrets.json` there and eleven with it under `Development` (measured). The
  csproj's `UserSecretsId` is for `dotnet run` on the host and nothing else.
- **Unprefixed environment beats prefixed environment.** With both set, `AllowedHosts=from-plain` wins
  over `ASPNETCORE_ALLOWEDHOSTS=from-prefixed` (measured) — slot 9 is later than slot 1. Set one.
- **There is no `appsettings.json` fallback**, and adding one is the wrong answer for the token: it
  would be a shipped layer. See ❌ below.
- **A `/run/secrets` provider, if it is ever added, outranks `-e`.** `AddKeyPerFile` called on
  `builder.Configuration` after `CreateBuilder` appends at slot 10, past the environment provider —
  measured, a `GitLab__AccessToken` file beat `-e GitLab__AccessToken`. That ordering is a feature for
  the Tier 2 design below, but it is not obvious from reading either half alone.

### ❌ Wrong — the token becomes part of the image

```dockerfile
ARG GITLAB_TOKEN                            # build arg
ENV GitLab__AccessToken=$GITLAB_TOKEN       # baked into the image
COPY appsettings.Production.json .          # secret in a layer, shipped to every puller
```

Build that with `--build-arg GITLAB_TOKEN=glpat-...` and:

```console
$ docker history --no-trunc --format '{{.CreatedBy}}' argleak:demo
ENV GitLab__AccessToken=glpat-NOTAREALTOKEN123
ARG GITLAB_TOKEN=glpat-NOTAREALTOKEN123
$ docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' argleak:demo
GitLab__AccessToken=glpat-NOTAREALTOKEN123
```

Both are readable by anyone who pulls the image. A later `ENV GITLAB_TOKEN=`, deleting the file in a
later layer, or squashing do **not** unsay it — the only remedy is rebuilding *and* rotating the token.
Neither `appsettings.json` (absent) nor the csproj's `UserSecretsId` (slot 8, `Development` only) is a
container secret mechanism — see the precedence list above.

### ✅ Right — the token arrives at run time

**Tier 1, environment.** Zero *container* changes. `GitLab__AccessToken` reaches `IConfiguration` as
`GitLab:AccessToken`, bound to `GitLabClientOptions.AccessToken` by `AddGitLabClient(builder.Configuration)`
(§6 index — live today, not inert).

```bash
docker run -d --name gmcp -p 127.0.0.1:19099:9080 \
  -e GitLab__AccessToken="$GITLAB_TOKEN" \
  -e GitLab__BaseAddress="https://gitlab.example.com/api/v4/" \
  gitlabmcp:musl
```

Honest limit: a runtime `-e` is not in the image, but it **is** in
`docker inspect -f '{{.Config.Env}}'` on the host, and `--env-file` is no different. Anyone with
access to the docker socket reads it.

**Tier 2, mounted secret file.** The value never enters `Config.Env` or image history.

```yaml
services:
  gitlabmcp:
    image: gitlabmcp:musl
    ports: ["127.0.0.1:19099:9080"]
    environment:
      GitLabMcp__Profile: Developer
    secrets: [gitlab_token]
secrets:
  gitlab_token:
    file: ./gitlab_token.txt
```

Verified: compose mounts it at `/run/secrets/gitlab_token`, readable by the app user, contents
byte-exact.

⚠️ **Write the file with `printf '%s' "$TOKEN" > gitlab_token.txt`, not `echo`.** The trailing newline
survives all the way into the `PRIVATE-TOKEN` header.

**DEC-013 settled this as option A, and `Program.cs` implements it:**
`builder.Configuration.AddKeyPerFile("/run/secrets", true, false)` — a file named
`GitLab__AccessToken` becomes configuration key `GitLab:AccessToken` (`KeyPerFile`'s `SectionDelimiter`
defaults to `__`). Ships **in the ASP.NET Core shared framework** — no extra `PackageReference`. Composes
with the same `AddGitLabClient(configuration)` binding as Tier 1, and lands *after* the environment
provider, so the file wins over `-e` (measured). ⚠️ **The path must be absolute**, and `optional: true`
does not save you: a relative path throws `ArgumentException: The path must be absolute. (Parameter
'root')` out of `PhysicalFileProvider` before the server starts. `/run/secrets` is fine; a dev-time
relative path is not. Verified AOT-clean by this repo's own publish gate. `Program.cs` also trims the
token in `PostConfigure<GitLabClientOptions>` (DEC-013's newline trap) — the newline survives into the
file otherwise.

Whichever is chosen, the token must never reach a log line, a tool result or a diagnostic endpoint —
that is `mcp-untrusted-content`'s territory, and it applies to the container's stdout too.

## 7. The profile as a container-level knob

**One image; one profile per container; selected by environment.** `mcp-profile-gating` settles on
`GitLabMcp__Profile`, resolved once at startup, and that is exactly the shape a container wants:

```bash
docker run -d -e GitLabMcp__Profile=Maintainer -e GitLab__AccessToken="$TOKEN_RO"  ... gitlabmcp:musl
docker run -d -e GitLabMcp__Profile=DevOps     -e GitLab__AccessToken="$TOKEN_OPS" ... gitlabmcp:musl
```

- **Do not build a per-profile image.** The Dockerfile knows nothing about profiles and should stay
  that way; a `gitlabmcp:devops` tag is a full ILCompiler rebuild for the sake of one string.
- **Do not set the profile with `ENV` in the Dockerfile.** A default baked into the image is a default
  somebody forgets to override. But be exact about what an unset variable does: `mcp-profile-gating`'s
  `Program.cs` wiring does **not** fail on a missing value — an absent `GitLabMcp:Profile` falls back
  to `McpProfile.Maintainer` (narrowest surface), and only a *present but unparseable* value throws.
  So an unset `GitLabMcp__Profile` is silent, not loud. Set it explicitly in every `docker run` and
  every compose service. If fail-fast on absence is wanted instead, that change belongs in
  `mcp-profile-gating`, not the Dockerfile.
- The payoff is per-container least privilege: because each container has its own environment, each
  gets its own GitLab token, scoped to match its profile. That is the only arrangement in which a
  read-only profile is genuinely read-only. A profile narrows the tool surface; the token is the
  ceiling, and when they disagree you get GitLab 403s, not tool bugs — see `mcp-profile-gating`.

Verified live (`docker run` with `-e GitLabMcp__Profile=Developer -e GitLab__BaseAddress=https://gitlab.example.internal/api/v4/`):
the startup log prints `GitlabMCP <version> starting: profile=Developer gitlab=https://gitlab.example.internal/api/v4/`,
and `resources/read` on `gitlab-mcp://server/info` echoes the same two values back over the wire —
the two knobs actually reach the running container, not just `IConfiguration`.

## 8. HEALTHCHECK

**Ship no `HEALTHCHECK` today.** Two verified reasons, both concrete.

The MCP endpoint answers `POST` only, so a naive probe fails forever:

```console
$ docker exec gmcp sh -c 'wget -q -O /dev/null http://127.0.0.1:9080/ ; echo exit=$?'
wget: server returned error: HTTP/1.1 405 Method Not Allowed
exit=1
```

`GET /` → 405, `HEAD /` → 405, `GET /healthz` → 404. A `HEALTHCHECK` built on any of those marks the
container permanently unhealthy while it is serving traffic perfectly — a lie in the expensive
direction, because orchestrators restart on it.

A `POST tools/list` probe *does* work on the Alpine base and exits 0:

```bash
wget -q -O - --header='Content-Type: application/json' \
  --header='Accept: application/json, text/event-stream' \
  --header='MCP-Protocol-Version: 2025-11-25' \
  --post-data='{"jsonrpc":"2.0","id":1,"method":"tools/list"}' http://127.0.0.1:9080/
```

It still works with host filtering on, because it reaches `127.0.0.1:9080` and `127.0.0.1` is in
`ASPNETCORE_ALLOWEDHOSTS` (§5) — a probe written against the container's own hostname would get 400
instead. Do not ship it either, for three reasons: it proves only that Kestrel and the MCP pipeline are up and
says nothing about GitLab reachability or token validity; it is **impossible on the chiselled base**,
which has 67 files, no shell and nothing under `bin/`, so `HEALTHCHECK CMD` has nothing to execute; and
once real tools exist, walking the full listing path every interval is a recurring cost for no signal.

The honest fix, when it is wanted, is a dedicated endpoint — `app.MapGet("/healthz", ...)` — plus a
deliberate decision about liveness versus readiness. If it touches GitLab it is a readiness check and
must be cached or heavily throttled, or the probe interval quietly becomes a rate-limit consumer. Until
that endpoint exists, omit `HEALTHCHECK` and let the orchestrator's TCP check do its (limited) job.

## 9. `.dockerignore`

Lives at the **context root**. BuildKit falls back to the context-root file for any Dockerfile,
including one passed with `-f` — but a sibling `<dockerfile-name>.dockerignore` takes precedence and
*replaces* it rather than merging. Verified: with `-f Alt.Dockerfile`, a root `.dockerignore`
excluding `drop.txt` and an `Alt.Dockerfile.dockerignore` excluding `keep.txt`, the context contained
`drop.txt` and not `keep.txt` — the root file was ignored entirely. Keep exactly one `.dockerignore`,
at the context root; do not add per-Dockerfile ignore files.

```gitignore
# Build outputs
**/bin/
**/obj/
# Tooling / VCS
.git/
.gitignore
.idea/
.vs/
.vscode/
.claude/
**/*.user
**/*.log
# Docs & docker
Dockerfile*
.dockerignore
**/*.md
LICENSE
```

That is the file on disk, byte for byte (172 bytes).

This is not tidiness. Measured in this repo: `GitlabMCP/bin` is **479 MB** and `GitlabMCP/obj` is
**144 MB**. Without the file, every build — including a no-op rebuild — uploads hundreds of megabytes
to the daemon before Docker reads the first `FROM`. `**/obj/` matters for correctness too: a host
`obj/` restored for `win-x64` has no business inside a `linux-musl-x64` build.

### What was leaking, and the two glob rules behind it

- **`*.md` is root-only.** Docker matches a pattern against the whole slash-joined path, so `*.md`
  excludes `/README.md` and never `GitlabMCP/README.md` — which was therefore in the context *and*
  copied into the build stage by `COPY GitlabMCP/ GitlabMCP/`. `**/*.md` is the fix; `**/*.log` is the
  same shape.
- **`Dockerfile*` is a prefix glob**, not "anything Dockerfile-ish". It excludes `Dockerfile.probe`
  and does **not** exclude `probe.Dockerfile`. Name auxiliary Dockerfiles `Dockerfile.<thing>` — or
  keep them outside the context entirely, as the probe below does.
- `.claude/` was not matched by any pattern at all: 12 files, ~320 KB, uploaded on every build.

### How to actually measure the context

**Do not read `transferring context` as the context size.** BuildKit prints that line twice, for two
different things, and the first one is not the context at all:

```text
#3 [internal] load .dockerignore
#3 transferring context: 214B done      <- the .dockerignore FILE (172 B + 42 B of framing)
#4 [internal] load build context
#4 transferring context: 3.83kB done    <- the context, and only on a cold builder
```

The first figure tracks the ignore file's own length — grow `.dockerignore` by 21 bytes and it moves
193B → 214B — so it would not budge if the context were 600 MB. The second is a *differential* wire
figure: warm, it reports only what the filesync had to resend, which can be a few hundred bytes.

The durable check is the file list. Build a throwaway image whose Dockerfile lives **outside** the
context, so it is not itself part of what you are measuring:

```bash
{ echo 'FROM busybox:latest'; echo 'COPY . /ctx'; } > "$TEMP/ctx.Dockerfile"
docker builder prune -a -f
docker build --no-cache -f "$TEMP/ctx.Dockerfile" -t ctxprobe:now .
MSYS_NO_PATHCONV=1 docker run --rm ctxprobe:now   sh -c 'find /ctx -type f | wc -l; find /ctx -type f -exec cat {} + | wc -c'
docker rmi -f ctxprobe:now
```

Measured with the `.dockerignore` above, back when this was still the template's single-project layout:
**6 files, 3,376 bytes** — `GitlabMCP.slnx`, the `.csproj`, `GitlabMCP.http`, `Program.cs`,
`Properties/launchSettings.json`, `Tools/RandomNumberTools.cs`. Before `.claude/`, `**/*.log` and
`**/*.md` were added it was 19 files and 329,548 bytes. **Re-measured 2026-09-09, post-DEC-018's 7-project
split (and post `Tools/RandomNumberTools.cs`/nested `README.md` removal as dead template code, and the
new `tests/`, `.github/workflows/`, `compose.yaml`, `global.json`):** 222 files, 2,513,677 bytes — the
growth is exactly the real 13-domain tool surface plus its tests and CI/CD, not context-filtering rot; the
method above (a throwaway `COPY . /ctx` probe) is what to re-run rather than trusting either number.

BuildKit hashes **content, not mtimes**: `touch Program.cs` invalidates nothing. Do not reason about
this cache with `make` intuition.

## 10. Size and speed

Measure the real rootfs with `docker export`; `docker images` reports something else entirely (it
counts compressed blobs, and buildx also exports an attestation manifest alongside the image).

```bash
cid=$(docker create gitlabmcp:musl); docker export "$cid" | wc -c; docker rm "$cid"
```

```powershell
$cid = docker create gitlabmcp:musl
docker export $cid -o "$env:TEMP\rootfs.tar"; docker rm $cid | Out-Null
(Get-Item "$env:TEMP\rootfs.tar").Length
```

The **binary** on Alpine is one `ls`:

```bash
MSYS_NO_PATHCONV=1 docker run --rm --entrypoint /bin/ls gitlabmcp:musl -l /app/GitlabMCP
# -rwxr-xr-x 1 app app 23106152 ... /app/GitlabMCP
```

The chiselled base has no shell and nothing under `bin/`, so there it needs `docker create` +
`docker cp` instead — there is nothing to exec:

```bash
cid=$(docker create gitlabmcp:glibc); docker cp "$cid:/app/GitlabMCP" "$TEMP/gl.bin"; docker rm "$cid"
stat -c %s "$TEMP/gl.bin"      # => 23106400
```

| Image | base rootfs | + binary | total rootfs | `docker images` claims |
|---|---:|---:|---:|---:|
| musl / `runtime-deps:10.0-alpine` | 11,691,008 | 23,106,152 | **34,798,592** (33.2 MiB) | 59.3 MB |
| glibc / `runtime-deps:10.0-noble-chiseled` | 14,223,872 | 23,106,400 | **37,331,456** (35.6 MiB) | 53.8 MB |

The arithmetic closes to within ~1.4 KB, so the mental model is simple: **base plus one binary, nothing
else.** Alpine is the smaller of the two on disk despite `docker images` implying the opposite, and the
~2.5 MB gap is not a reason to pick either. Pick on what the final stage gives you: Alpine has a shell
and `apk` (debuggable, and the only one where a `HEALTHCHECK` is even possible); chiselled has neither
(smaller surface, already non-root). **Default to musl/Alpine.** Both RIDs produce a ~23.1 MB binary, so
a size regression means the *app* grew — `aot-publish-gate` owns that signal.

Timings measured on this machine (x64). Round numbers — ILCompiler dominates and varies run to run:

| Scenario | Time |
|---|---|
| Cold build, `--no-cache`, builder pruned | ~56–62 s (musl) / ~54–55 s (glibc) |
| `Generating native code` (ILCompiler) alone | ~45–50 s of that |
| Warm rebuild, nothing changed | **~2 s** |
| Warm rebuild after a real source edit (append a line to `Program.cs`, rebuild) | ~50 s |

ILCompiler is the floor for any source change and it cannot be cached across edits. Do not add cache
mounts on `obj/` chasing it — a cache mount over `obj/` combined with a separate restore layer masks
the restore's output and fails with `NETSDK1004: Assets file ... project.assets.json not found`. The
layer order in §3 (csproj → restore → sources → publish) is the whole optimisation and needs nothing
more.

## Common Pitfalls

| Pitfall | Fix |
|---|---|
| `exec /app/GitlabMCP: no such file or directory` | RID/base libc mismatch. The message is about the ELF loader, not your file. §1. |
| Container logs "Now listening" but `curl` gets `000` / exit 52 | `ASPNETCORE_URLS=http://localhost:...`. Bind `+`. §5. |
| Server answers on 8080 | Missing `ENV ASPNETCORE_HTTP_PORTS=9080`; every base ships 8080. |
| Both `ASPNETCORE_URLS` and `ASPNETCORE_HTTP_PORTS` set | `URLS` silently wins. Set one. |
| `docker exec gmcp id` → `uid=0(root)` | Missing `USER $APP_UID` on the Alpine base (its `Config.User` is empty). |
| Image is ~95 MB | `COPY --from=build /app/ ./` pulled in the 59 MB `.dbg`. Name the single file. |
| `error NETSDK1064: Package Microsoft.NET.ILLink.Tasks ... not found` | The publish `RUN` is missing the NuGet `--mount=type=cache` that restore had. |
| `error NETSDK1004: Assets file ... not found` | A cache mount over `obj/` masking the restore layer. Do not cache `obj/`. |
| `error NETSDK1112: The runtime pack for Microsoft.NETCore.App.Runtime.linux-musl-x64 was not downloaded` | The restore `RUN` is still `CACHED` but the NuGet cache mount was emptied (`docker builder prune`) — layer cache and cache-mount contents have independent lifetimes, so `--no-restore` publishes against nothing. Rebuild with `--no-cache`. |
| Build uploads 200+ MB of context | `.dockerignore` missing, not at the context root, or shadowed by a sibling `<dockerfile>.dockerignore`. |
| `stat C:/Program Files/Git/usr/bin/sh: no such file` | Git Bash path conversion. Prefix `MSYS_NO_PATHCONV=1`. |
| Token appears in `docker history` | It was an `ARG` or `ENV`. Rebuild **and rotate the token** — a later layer cannot unsay it. |
| GitLab rejects a token read from a mounted secret | Trailing newline from `echo`. Use `printf '%s'`, or `.Trim()` in code. |
| `TimeZoneInfo.FindSystemTimeZoneById` throws | No `tzdata` on Alpine or chiselled. Add `tzdata`, use `-chiseled-extra`, or stay on UTC. |
| Container marked unhealthy while working fine | A `HEALTHCHECK` doing `GET /`. It is a 405. §8. |
| `400 Bad Request - Invalid Hostname` (Kestrel **HTML**, not a JSON-RPC error) | The `Host` header is not in `ASPNETCORE_ALLOWEDHOSTS`. A compose service name, a bridge IP or `[::1]` is not covered unless listed. Override with `-e ASPNETCORE_ALLOWEDHOSTS=...`, which replaces the image value. §5. |
| Every `Host` header accepted, `evil.example` included | `ASPNETCORE_ALLOWEDHOSTS` is unset, so `AllowedHosts` is `*`. §5. |
| Image starts, exits with `InvalidOperationException` about `AllowedHosts`, restarts, repeat | `mcp-untrusted-content`'s startup check landed without the `ENV`. They belong in one commit. §3. |

## Checklist

Before committing a Dockerfile change:

- [ ] `RID` and `RUNTIME_IMAGE` share a libc (musl↔Alpine, glibc↔Ubuntu/chiselled).
- [ ] Every base tag named in the file was confirmed with `docker manifest inspect` today.
- [ ] Build stage is an `-aot` SDK tag; no `apk add` / `apt-get install` for the toolchain.
- [ ] Both `RUN`s carry the identical `--mount=type=cache,target=/root/.nuget/packages`.
- [ ] The final stage copies **one named file**, not a directory.
- [ ] `USER $APP_UID` present; `docker exec <c> id` shows `uid=1654(app)`.
- [ ] `ENV ASPNETCORE_HTTP_PORTS=9080` and `EXPOSE 9080`; no `ASPNETCORE_URLS` in the image.
- [ ] `ENV ASPNETCORE_ALLOWEDHOSTS="127.0.0.1;localhost;[::1]"` present, all three spellings, no ports.
      `docker inspect -f '{{.Config.Env}}'` shows it; `Host: evil.example` gets 400 and `Host: [::1]`
      gets 200.
- [ ] No `ARG`, `ENV` or `COPY` anywhere near a token; `docker history --no-trunc` is clean.
- [ ] No `HEALTHCHECK`, or one pointed at an endpoint that actually returns 200.
- [ ] No arm64 anything: no second RID, no `--platform`, no multi-arch manifest, no QEMU stage.
- [ ] `.dockerignore` at the context root, and a `COPY . /ctx` probe (§9) lists **only** the project
      files — no `.claude/`, no `*.md`, no `bin/` or `obj/`. Do not use the `transferring context`
      line as the measure; the first one is the ignore file's own size.
- [ ] Both flavours build, and the musl image answers a `tools/list` POST with `event: message`.
- [ ] `docker export | wc -c` is within a few MB of base + binary; if not, something extra got copied.

## More Info

- `aot-publish-gate` — the host-side publish, the IL2xxx/IL3xxx pass-fail bar, and the vswhere/MSB3073
  blocker. Run it before blaming the Dockerfile for a build failure.
- `mcp-untrusted-content` — why a published port is a credential-equivalent surface, what must never
  reach stdout or a tool result, and (§Step 5) the `AllowedHosts` startup check and `Origin` 403 whose
  container-side value this skill ships.
- `mcp-profile-gating` — where `GitLabMcp__Profile` is defined and what each profile grants.
- `mcp-server-smoke-test` — the full JSON-RPC handshake, beyond the one-request liveness check here.
- dotnet-upgrade:dotnet-aot-compat — fixing an IL warning once `aot-publish-gate` reports one.
