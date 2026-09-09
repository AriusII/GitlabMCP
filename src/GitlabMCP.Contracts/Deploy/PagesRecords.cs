namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Instance-wide Pages domain view (metadata + certificate expiry only, never the certificate
///     itself). Domain/url are GitLab-stored text, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record PagesDomainSummary(
    string? Domain,
    string? Url,
    long? ProjectId,
    bool? Verified,
    string? VerificationCode,
    bool? AutoSslEnabled,
    DateTimeOffset? EnabledUntil,
    bool? CertificateExpired,
    DateTimeOffset? CertificateExpiration);

public sealed record PagesDomainListResult(IReadOnlyList<PagesDomainSummary> Domains, bool Truncated);

/// <summary>
///     A single project's Pages domain, as returned right after creation. Never carries the raw
///     certificate or private key text — only the certificate's subject and expiry.
/// </summary>
public sealed record PagesDomainDetail(
    string? Domain,
    string? Url,
    bool? Verified,
    string? VerificationCode,
    bool? AutoSslEnabled,
    DateTimeOffset? EnabledUntil,
    bool? CertificateExpired,
    string? CertificateSubject);

/// <summary>
///     One currently-live Pages deployment (a project can have several, e.g. per path prefix for
///     parallel deployments). <see cref="Url" /> is GitLab-served, so a record containing this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record PagesDeploymentSummary(
    DateTimeOffset? CreatedAt,
    string? Url,
    string? PathPrefix,
    string? RootDirectory);

/// <summary>
///     A project's Pages configuration (<c>gitlab_get_pages_settings</c>,
///     <c>gitlab_update_pages_settings</c>) — where the site is served, how it is secured, and what is
///     currently deployed. Every GitLab.Client member is nullable because the pinned spec declares this
///     response with no schema at all; that nullability survives unchanged here. Wraps via
///     <see cref="GitLabContent" /> — <see cref="Url" />/<see cref="PrimaryDomain" /> are GitLab-served text.
/// </summary>
public sealed record PagesSettingsSummary(
    string? Url,
    bool? UniqueDomainEnabled,
    bool? ForceHttps,
    string? PrimaryDomain,
    IReadOnlyList<PagesDeploymentSummary> Deployments);

/// <summary>
///     Result of <c>gitlab_unpublish_pages</c>. Carries no GitLab-authored text at all (just a confirmation
///     flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record PagesUnpublishResult(bool Unpublished);

/// <summary>
///     Result of <c>gitlab_check_pages_access</c>. GitLab answers this check with an empty 200 or throws
///     (<c>GitLabForbiddenException</c>/<c>GitLabNotFoundException</c>) rather than returning a body, so
///     <see cref="CanView" /> is derived by the tool from which of those happened. Carries no GitLab-authored
///     text at all, so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record PagesAccessResult(bool CanView);

/// <summary>
///     One project-scoped Pages domain as returned by <c>gitlab_list_pages_domains</c> — the same
///     shape as <see cref="PagesDomainDetail" />, listed rather than singular. Domain/url/certificate subject
///     are GitLab-served text, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ProjectPagesDomainListResult(IReadOnlyList<PagesDomainDetail> Domains, bool Truncated);

/// <summary>
///     <see cref="Domain" /> is the caller-supplied hostname being confirmed, not GitLab-authored
///     text, so this is the rare all-scalar bare-record result (mcp-tool-authoring Step 4).
/// </summary>
public sealed record PagesDomainDeleteResult(string Domain, bool Deleted);