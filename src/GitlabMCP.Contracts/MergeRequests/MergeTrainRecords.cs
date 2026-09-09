namespace GitlabMCP.Contracts.MergeRequests;

public sealed record MergeTrainCarSummary(
    long Id,
    long? MergeRequestIid,
    string? MergeRequestTitle,
    string? TargetBranch,
    string? Status,
    string? User,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? MergedAt);

public sealed record MergeTrainListResult(IReadOnlyList<MergeTrainCarSummary> Cars, bool Truncated);