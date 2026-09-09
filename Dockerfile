# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# GitlabMCP - Native AOT, self-contained, single-file ASP.NET Core MCP server.
#
# Defaults build the musl/Alpine flavour:
#   docker build -t gitlabmcp:musl .
#
# glibc/Ubuntu flavour:
#   docker build -t gitlabmcp:glibc \
#     --build-arg RID=linux-x64 \
#     --build-arg SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-aot \
#     --build-arg RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled .
#
# The RID and the final image's libc MUST match. See README notes.
# ---------------------------------------------------------------------------

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine

# ----------------------------- build stage ---------------------------------
FROM ${SDK_IMAGE} AS build
ARG RID=linux-musl-x64
ARG BUILD_CONFIGURATION=Release
# The pushed release tag with its leading "v" stripped (CI computes this — see release.yml), e.g.
# "1.2.3". Left at its dev default for a local `docker build`. Purely a label baked into the binary's
# own AssemblyInformationalVersion and re-exposed at runtime as GitlabMcp__Version (below) — it drives
# no build behaviour, so getting it wrong never breaks a build, only what `server_info` reports.
ARG APP_VERSION=0.0.0-dev
WORKDIR /src

# Restore first, on its own layer, so source edits don't re-download packages.
# DEC-018: the solution is 7 src/* projects (6 class libraries + the host) under
# Central Package Management — every .csproj in the reference graph, plus the three
# solution-wide files, must be present before `dotnet restore` can resolve it at all
# (CPM's version-less PackageReference items fail restore with NU1008 without
# Directory.Packages.props specifically).
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/GitlabMCP.Abstractions/GitlabMCP.Abstractions.csproj src/GitlabMCP.Abstractions/
COPY src/GitlabMCP.Contracts/GitlabMCP.Contracts.csproj       src/GitlabMCP.Contracts/
COPY src/GitlabMCP.Mapping/GitlabMCP.Mapping.csproj           src/GitlabMCP.Mapping/
COPY src/GitlabMCP.GraphQL/GitlabMCP.GraphQL.csproj           src/GitlabMCP.GraphQL/
COPY src/GitlabMCP.Profiles/GitlabMCP.Profiles.csproj         src/GitlabMCP.Profiles/
COPY src/GitlabMCP.Tools/GitlabMCP.Tools.csproj               src/GitlabMCP.Tools/
COPY src/GitlabMCP/GitlabMCP.csproj                           src/GitlabMCP/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/GitlabMCP/GitlabMCP.csproj --runtime "$RID"

# Now the real source, all six libraries plus the host, under one src/ COPY.
# tests/ is deliberately never copied here - irrelevant to publishing the host,
# and dragging in Microsoft.NET.Test.Sdk would only slow the image down.
COPY src/ src/

# PublishAot / SelfContained / PublishSingleFile / InvariantGlobalization
# all come from the host's .csproj; nothing to override here. The five class
# libraries carry none of those properties (Directory.Build.targets enforces
# it) - only the host publishes.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/GitlabMCP/GitlabMCP.csproj \
        --configuration "$BUILD_CONFIGURATION" \
        --runtime "$RID" \
        --no-restore \
        --output /app \
        -p:Version="$APP_VERSION" -p:InformationalVersion="$APP_VERSION"

# ----------------------------- final stage ---------------------------------
FROM ${RUNTIME_IMAGE} AS final

# ARGs do not cross the FROM boundary — redeclare to bring APP_VERSION into this stage's ENV below.
ARG APP_VERSION=0.0.0-dev

# runtime-deps:10.0-alpine defines the `app` user (UID/GID 1654) and exports
# APP_UID, but does NOT set USER. The chiseled/distroless variants already do.
USER $APP_UID
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/GitlabMCP ./GitlabMCP

# Kestrel binds http://+:$ASPNETCORE_HTTP_PORTS. The base image ships 8080;
# this project uses 9080. Binding to `+` (all interfaces) is required - a
# loopback-only bind is unreachable from outside the container.
ENV ASPNETCORE_HTTP_PORTS=9080
EXPOSE 9080

# HostFilteringMiddleware is already in the pipeline and reads the `AllowedHosts`
# configuration key; its default is "*", which accepts ANY Host header and leaves
# the server open to DNS rebinding. This project has no appsettings.json, so the
# image is the only place the key can be set. Matched on the host part only and
# case-insensitively - never include ports. All three loopback spellings are
# required: omitting [::1] returns 400 to a genuine IPv6 loopback client (verified).
# A compose service name is NOT covered - override with
# `docker run -e ASPNETCORE_ALLOWEDHOSTS=...` when reaching the server by another name.
ENV ASPNETCORE_ALLOWEDHOSTS="127.0.0.1;localhost;[::1]"

# The ONLY three knobs an operator actually turns per container (docker-aot-image §6/§7):
#   -e GitLabMcp__Profile=Developer|DevOps|Maintainer|FullPermission   (absent -> Maintainer, DEC-004)
#   -e GitLab__BaseAddress=https://gitlab.example.com/api/v4/         (absent -> gitlab.com, DEC-012)
#   -e GitLab__AccessToken=...  or a mounted /run/secrets/GitLab__AccessToken file (DEC-013, Tier 2)
# GitlabMcp__Version is baked in at build time from APP_VERSION, never set by `docker run`.
ENV GitlabMcp__Version=$APP_VERSION

ENTRYPOINT ["/app/GitlabMCP"]
