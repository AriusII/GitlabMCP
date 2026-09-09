namespace GitlabMCP.Contracts.Code;

/// <summary>
///     Push-mirror projection records (domain "code"), from <c>IRemoteMirrorsClient</c>. A push mirror is a
///     distinct resource from the project's single pull mirror (<see cref="PullMirrorSummary" /> in
///     <c>ProtectionRecords.cs</c>) — a project may configure several push mirrors, each targeting a
///     different remote that GitLab pushes every update to.
/// </summary>
public sealed record RemoteMirrorHostKeySummary(string? FingerprintSha256);

/// <summary>
///     <see cref="Url" /> is GitLab's own read of the configured remote: GitLab scrubs any embedded
///     basic-auth userinfo to <c>*****:*****</c> before returning it, so this is not a raw-credential
///     exposure, but it is still GitLab-authored configuration text — wrap regardless.
/// </summary>
public sealed record RemoteMirrorSummary(
    long Id,
    bool? Enabled,
    string? Url,
    string? UpdateStatus,
    DateTimeOffset? LastUpdateAt,
    DateTimeOffset? LastUpdateStartedAt,
    DateTimeOffset? LastSuccessfulUpdateAt,
    string? LastError,
    bool? OnlyProtectedBranches,
    bool? KeepDivergentRefs,
    string? AuthMethod,
    string? MirrorBranchRegex,
    IReadOnlyList<RemoteMirrorHostKeySummary> HostKeys);

public sealed record RemoteMirrorListResult(IReadOnlyList<RemoteMirrorSummary> Mirrors, bool Truncated);