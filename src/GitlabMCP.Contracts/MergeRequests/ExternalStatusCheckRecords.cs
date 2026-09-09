namespace GitlabMCP.Contracts.MergeRequests;

/// <summary>
///     Serves two shapes from one record: the project's configured checks (<c>ProtectedBranches</c> populated,
///     <c>Status</c> null) and one merge request's live check results (<c>Status</c> populated, <c>ProtectedBranches</c>
///     empty).
/// </summary>
public sealed record ExternalStatusCheckSummary(
    long Id,
    string? Name,
    string? ExternalUrl,
    string? Status,
    IReadOnlyList<string> ProtectedBranches);

public sealed record ExternalStatusCheckListResult(IReadOnlyList<ExternalStatusCheckSummary> Checks, bool Truncated);