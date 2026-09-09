namespace GitlabMCP.Contracts.Planning;

/// <summary>
///     Result of subscribing/unsubscribing the authenticated user to a merge request's
///     notifications, mirroring <see cref="IssueSubscriptionResult" />'s shape for the merge-request case.
///     This is the only merge-request-shaped record the Planning domain owns beyond
///     <see cref="RelatedMergeRequestSummary" /> — the full merge-request lifecycle lives in the
///     mergerequests domain.
/// </summary>
public sealed record MergeRequestSubscriptionResult(long Iid, string? Title, bool? Subscribed, string? WebUrl);