namespace GitlabMCP.Contracts.Planning;

/// <summary>
///     Planning-domain projection of <c>GitLab.Client.Models.GitLabIssue</c> — the handful of fields a
///     planning caller needs, never the 45-property library DTO. Every string here is GitLab-authored, so
///     every tool returning it (directly or nested in a list result) wraps via <c>GitLabContent</c>.
/// </summary>
public sealed record IssueSummary(
    long Iid,
    string? Title,
    string? State,
    string? Author,
    IReadOnlyList<string> Labels,
    string? MilestoneTitle,
    int? Weight,
    string? Severity,
    bool? Confidential,
    DateTimeOffset? UpdatedAt,
    string? WebUrl);

public sealed record IssueListResult(IReadOnlyList<IssueSummary> Issues, bool Truncated);

/// <summary>
///     One entry from <c>IIssuesClient.ListLinksAsync</c> — GitLab answers with the linked issue itself,
///     carrying its link id/type rather than a separate link entity (see the XML doc on that method).
/// </summary>
public sealed record IssueLinkSummary(
    long Iid,
    string? Title,
    string? State,
    long? IssueLinkId,
    string? LinkType,
    string? WebUrl);

public sealed record IssueLinkListResult(IReadOnlyList<IssueLinkSummary> Links, bool Truncated);

public sealed record IssueSubscriptionResult(long Iid, string? Title, bool? Subscribed, string? WebUrl);

/// <summary>
///     A participant in an issue's discussion — deliberately excludes every PII/secret field on
///     <c>GitLabUser</c> (Email, CommitEmail, Identities, ScimIdentities, IsAdmin, Note, CustomAttributes).
/// </summary>
public sealed record ParticipantSummary(long Id, string? Username, string? Name, string? WebUrl);

public sealed record ParticipantListResult(IReadOnlyList<ParticipantSummary> Participants, bool Truncated);

/// <summary>
///     Bare record: both fields are server/caller-supplied scalars, no GitLab-authored string
///     involved (<c>IIssuesClient.DeleteAsync</c> returns no body).
/// </summary>
public sealed record IssueDeleteResult(long Iid, bool Deleted);

/// <summary>
///     Bare record: both fields are server/caller-supplied scalars, no GitLab-authored string
///     involved (<c>IIssuesClient.DeleteLinkAsync</c> returns no body).
/// </summary>
public sealed record IssueUnlinkResult(long IssueLinkId, bool Deleted);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabTimeStats</c>. The human-readable fields are
///     GitLab-formatted text, so a tool returning this wraps via <c>GitLabContent</c>.
/// </summary>
public sealed record TimeStatsSummary(
    long TimeEstimateSeconds,
    long TotalTimeSpentSeconds,
    string? HumanTimeEstimate,
    string? HumanTotalTimeSpent);

/// <summary>
///     Bare record: <c>GitLab.Client.Models.GitLabIssueCounts</c> is three plain integers with no
///     GitLab-authored string anywhere in the payload (DEC-007's rare all-scalar bucket).
/// </summary>
public sealed record IssueStatisticsResult(int? Opened, int? Closed, int? All);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabIssueLink</c> as returned by
///     <c>IIssuesClient.CreateLinkAsync</c>, distinct from <see cref="IssueLinkSummary" />, which projects
///     the issue-shaped entries <c>ListLinksAsync</c> returns.
/// </summary>
public sealed record IssueLinkResult(
    long LinkId,
    string? LinkType,
    IssueSummary? SourceIssue,
    IssueSummary? TargetIssue);

/// <summary>
///     A minimal projection of <c>GitLab.Client.Models.GitLabMergeRequest</c> for
///     <c>gitlab_list_issue_related_merge_requests</c>, never the 36-property library DTO.
/// </summary>
public sealed record RelatedMergeRequestSummary(
    long Iid,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? Author,
    string? WebUrl);

public sealed record RelatedMergeRequestListResult(
    IReadOnlyList<RelatedMergeRequestSummary> MergeRequests,
    bool Truncated);