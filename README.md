# GitlabMCP

[![CI](https://github.com/AriusII/GitlabMCP/actions/workflows/ci.yml/badge.svg)](https://github.com/AriusII/GitlabMCP/actions/workflows/ci.yml)
[![Release](https://github.com/AriusII/GitlabMCP/actions/workflows/release.yml/badge.svg)](https://github.com/AriusII/GitlabMCP/actions/workflows/release.yml)

A GitLab-only MCP server (targets GitLab 19.x): C# 14 / .NET 10, Native AOT, shipped as a single-file
self-contained Docker image, speaking MCP over HTTP. It wraps the `GitLab.Client` NuGet package and
exposes GitLab's API as a **profile-gated** tool/prompt/resource surface — see `CLAUDE.md` for the
full architecture and `DECISIONS.md` for why each choice was made.

## Run it

```bash
docker run -d --name gitlabmcp -p 127.0.0.1:9080:9080 \
  -e GitLabMcp__Profile=Developer \
  -e GitLab__AccessToken="$YOUR_GITLAB_PAT" \
  <dockerhub-user>/gitlabmcp:latest-amd64
```

Or with Compose — `compose.yaml` at the repo root is a ready-to-edit example (profile, target GitLab
instance, and the token as a mounted secret rather than a bare `-e`):

```bash
cp compose.yaml my-compose.yaml     # edit the image line and GitLab__BaseAddress
printf '%s' "$YOUR_GITLAB_PAT" > gitlab_token.txt
docker compose -f my-compose.yaml up -d
```

### The three knobs that matter

| Env var | Default | Purpose |
|---|---|---|
| `GitLabMcp__Profile` | `Maintainer` | Which tools/prompts/resources this container advertises: `Maintainer`, `Developer`, `DevOps`, or `FullPermission`. Filtered before `tools/list` — a tool the profile doesn't grant never appears. |
| `GitLab__BaseAddress` | `https://gitlab.com/api/v4/` | Which GitLab instance to talk to. **Just set it to your instance's own URL** — a self-hosted domain (`https://gitlab.example.com`), a path-prefixed self-hosted install (`https://example.com/gitlab`), or a non-standard port (`https://gitlab.internal:8443`) — the server appends `api/v4/` automatically if it isn't already there. All of these are covered by `tests/GitlabMCP.Tests/Abstractions/GitLabBaseAddressNormalizerTests.cs` and `GitLabGraphQlEndpointTests.cs`. If you do include a path that already contains `api/` but the wrong version (e.g. `.../api/v5/`), that's left alone and fails fast at startup with a clear message instead of being silently rewritten. |
| `GitLab__AccessToken` | — (required) | A GitLab personal/project access token. Set by `-e`, or mount it as `/run/secrets/GitLab__AccessToken` (Docker/Compose secret) so it never lands in `docker inspect`. |

The active profile and GitLab target are logged on startup (never the token) and readable at runtime
via the `server_info` MCP resource. The GitLab token's own permissions are the real ceiling — a
profile narrows the tool surface, it cannot widen what the token itself is allowed to do.

## Building and testing locally

```bash
dotnet build GitlabMCP.slnx                    # solution is .slnx (XML), not .sln
dotnet test GitlabMCP.slnx
dotnet run --project src/GitlabMCP             # http://localhost:9080
docker build -t gitlabmcp:musl .               # the image that ships; see .claude/skills/docker-aot-image
```

## CI/CD

- **`.github/workflows/ci.yml`** — every push/PR to `main`: `dotnet build`+`dotnet test` in Release,
  then a full `docker build` (linux/amd64) with no push. That Docker build stage runs the real Native
  AOT publish for `linux-musl-x64` inside the container, so it doubles as the AOT gate on every PR.
  Needs no secrets, so it runs the same way on a fork's pull request.
- **`.github/workflows/release.yml`** — push a tag `vX.Y.Z` to cut a release: re-runs the same
  build+test gate, then builds and pushes the image to Docker Hub as
  `<user>/gitlabmcp:X.Y.Z-amd64`, `:X.Y.Z`, `:latest-amd64` and `:latest`, then publishes a GitHub
  Release with auto-generated notes. arm64 is out of scope repo-wide (see `CLAUDE.md`), so every tag
  is a single linux/amd64 image, never a multi-arch manifest.

### Required repository secrets

To let `release.yml` push to Docker Hub, add these under **Settings → Secrets and variables →
Actions** on this repository:

| Secret | Value |
|---|---|
| `DOCKERHUB_USERNAME` | Your Docker Hub username (the image is pushed to `<this>/gitlabmcp`). |
| `DOCKERHUB_TOKEN` | A Docker Hub **Personal Access Token** with Read & Write scope — [Docker Hub → Account Settings → Security → New Access Token](https://hub.docker.com/settings/security). Do not use your Docker Hub password. |

`ci.yml` needs no secrets at all. `release.yml`'s GitHub Release step uses the built-in
`GITHUB_TOKEN`, which needs no setup.
