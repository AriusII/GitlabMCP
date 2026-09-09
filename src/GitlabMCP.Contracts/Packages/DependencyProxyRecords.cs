namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     All-scalar acknowledgement of a scheduled dependency proxy cache purge — GitLab performs the
///     deletion asynchronously (202 Accepted), so this confirms only that it was queued. No GitLab-authored
///     string, so the tool returns this bare (unwrapped).
/// </summary>
public sealed record DependencyProxyCachePurgeResult(bool Scheduled);