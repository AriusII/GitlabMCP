namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     A project or group Debian (APT) repository distribution/codename (<c>GitLabDebianDistribution</c>).
///     Codename, suite and description are GitLab-authored (set by whoever configured the distribution), so
///     every tool returning this wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record DebianDistributionSummary(
    long Id,
    string? Codename,
    string? Suite,
    string? Origin,
    string? Label,
    string? Version,
    string? Description,
    IReadOnlyList<string> Components,
    IReadOnlyList<string> Architectures);

public sealed record DebianDistributionListResult(
    IReadOnlyList<DebianDistributionSummary> Distributions,
    bool Truncated);

/// <summary>
///     Confirmation of a delete. <see cref="Codename" /> is the caller's own input echoed back, not
///     GitLab-authored text, so the tool returns this bare (unwrapped) — same reasoning as
///     <c>LabelDeleteResult</c>/<c>WikiPageDeleteResult</c> elsewhere in this codebase.
/// </summary>
public sealed record DebianDistributionDeleteResult(string Codename, bool Deleted);