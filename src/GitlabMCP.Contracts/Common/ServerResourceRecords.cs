namespace GitlabMCP.Contracts.Common;

/// <summary>
///     Server-authored, non-GitLab structural data for the "gitlab-mcp://server/info" resource (DEC-029).
///     Never wrapped via <see cref="GitLabContent" /> — nothing here originates from GitLab, so the
///     untrusted-content envelope would misrepresent it as untrusted input it is not.
/// </summary>
public sealed record ServerInfoResource(string Profile, string Version, string GitLabBaseAddress, DateTimeOffset ServerTimeUtc);

/// <summary>One row of CLAUDE.md's "Target architecture: profiles" persona table.</summary>
public sealed record ProfileDescription(string Profile, string Persona, string Scope);

/// <summary>
///     Server-authored description of all four selectable profiles (DEC-029) — static content mirroring
///     CLAUDE.md's persona table, so an MCP client can introspect the choice of profile without reading
///     the repo. Never wrapped via <see cref="GitLabContent" /> for the same reason as
///     <see cref="ServerInfoResource" />.
/// </summary>
public sealed record ServerProfilesResource(IReadOnlyList<ProfileDescription> Profiles);