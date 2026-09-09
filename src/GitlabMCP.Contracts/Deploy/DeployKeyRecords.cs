namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Deploy key metadata. Deliberately omits <c>GitLabDeployKey.Key</c> (the raw SSH public key text) —
///     not on CLAUDE.md's closed secret-field list (a deploy key's public key is not a credential by
///     definition), but excluded anyway to keep the payload to what identifies and scopes the key
///     (fingerprint, not the key material itself). Title and the associated project paths are
///     GitLab-authored text, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DeployKeySummary(
    long Id,
    string? Title,
    string? Fingerprint,
    string? UsageType,
    bool? CanPush,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? ProjectWithWriteAccess,
    string? ProjectWithReadonlyAccess);

public sealed record DeployKeyListResult(IReadOnlyList<DeployKeySummary> DeployKeys, bool Truncated);

/// <summary>
///     Result of <c>gitlab_delete_deploy_key</c>. Carries no GitLab-authored text at all (just a
///     confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record DeployKeyDeleteResult(long DeployKeyId, bool Deleted);

/// <summary>
///     Deploy token metadata only — <c>GitLabDeployToken</c> carries no secret value itself (the
///     plaintext token is returned once, by the create call, as a separate <c>GitLabDeployTokenWithSecret</c>
///     type this domain never touches).
/// </summary>
public sealed record DeployTokenSummary(
    long Id,
    string? Name,
    string? Username,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> Scopes,
    bool? Revoked,
    bool? Expired);

public sealed record DeployTokenListResult(IReadOnlyList<DeployTokenSummary> DeployTokens, bool Truncated);

/// <summary>
///     A freshly created deploy token together with its plaintext password — the only moment GitLab ever
///     discloses it (<c>GitLabDeployTokenWithSecret</c>'s own doc: "cannot be retrieved afterwards... every
///     read-side operation returns the secret-free <c>GitLabDeployToken</c> instead"). A deliberate secret
///     exception alongside People's <c>ImpersonationTokenCreated</c>, <c>RunnerRegistrationResult</c> and
///     <c>ResourceAccessTokenCreated</c>: without <see cref="Token" /> this tool cannot do what it exists to
///     do, since GitLab can never show the value again after this one response.
/// </summary>
public sealed record DeployTokenCreated(
    long Id,
    string? Name,
    string? Username,
    string Token,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> Scopes);

/// <summary>
///     Result of <c>gitlab_delete_deploy_token</c>. Carries no GitLab-authored text at all (just a
///     confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record DeployTokenDeleteResult(long DeployTokenId, bool Deleted);