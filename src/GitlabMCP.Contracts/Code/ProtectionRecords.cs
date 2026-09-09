namespace GitlabMCP.Contracts.Code;

/// <summary>Protected-branch/tag, push-rule and pull-mirror projection records (domain "code").</summary>
public sealed record AccessLevelSummary(int AccessLevel, string? AccessLevelDescription);

public sealed record ProtectedBranchSummary(
    long? Id,
    string? Name,
    bool? AllowForcePush,
    IReadOnlyList<AccessLevelSummary> PushAccessLevels,
    IReadOnlyList<AccessLevelSummary> MergeAccessLevels,
    IReadOnlyList<AccessLevelSummary> UnprotectAccessLevels,
    bool? CodeOwnerApprovalRequired,
    bool? Inherited);

public sealed record ProtectedBranchListResult(IReadOnlyList<ProtectedBranchSummary> Branches, bool Truncated);

public sealed record ProtectedTagSummary(
    string? Name,
    IReadOnlyList<AccessLevelSummary> CreateAccessLevels);

public sealed record ProtectedTagListResult(IReadOnlyList<ProtectedTagSummary> Tags, bool Truncated);

/// <summary>Bare record: <see cref="Unprotected" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record BranchUnprotectResult(bool Unprotected);

/// <summary>Bare record: <see cref="Unprotected" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record TagUnprotectResult(bool Unprotected);

/// <summary>
///     A project's push rule (<c>gitlab_get_push_rule</c>). The regex/email/filename fields are project
///     configuration set by a maintainer — GitLab-sourced text, not server-computed — so this still wraps.
/// </summary>
public sealed record PushRuleSummary(
    long Id,
    string? CommitMessageRegex,
    string? CommitMessageNegativeRegex,
    string? BranchNameRegex,
    bool? DenyDeleteTag,
    bool? MemberCheck,
    bool? PreventSecrets,
    string? AuthorEmailRegex,
    string? FileNameRegex,
    int? MaxFileSize,
    bool? CommitCommitterCheck,
    bool? CommitCommitterNameCheck,
    bool? RejectUnsignedCommits,
    bool? RejectNonDcoCommits);

/// <summary>
///     A project's pull-mirror configuration (<c>gitlab_get_pull_mirror</c>). <c>Url</c> is the mirror source
///     as GitLab itself returns it: GitLab redacts embedded basic-auth credentials in this field before
///     answering the API, so this is not a raw-secret exposure, but it still carries a GitLab-configured
///     string — wrap regardless.
/// </summary>
public sealed record PullMirrorSummary(
    long Id,
    string? UpdateStatus,
    string? Url,
    string? LastError,
    DateTimeOffset? LastUpdateAt,
    DateTimeOffset? LastSuccessfulUpdateAt,
    bool? Enabled,
    bool? OnlyMirrorProtectedBranches);