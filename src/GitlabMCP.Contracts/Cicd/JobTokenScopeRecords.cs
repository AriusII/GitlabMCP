namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     One entry on a project's CI/CD job token allowlist (domain "cicd") — either a project or a group,
///     unified into one shape by <c>gitlab_list_job_token_allowlist</c>'s <c>kind</c> switch.
///     <see cref="PathWithNamespace" /> is populated for a project entry and left null for a group entry.
/// </summary>
public sealed record AllowlistEntrySummary(
    long Id,
    string? Name,
    string? PathWithNamespace,
    string? WebUrl);

public sealed record JobTokenAllowlistResult(string Kind, IReadOnlyList<AllowlistEntrySummary> Entries, bool Truncated);

/// <summary>
///     Result of <c>gitlab_get_job_token_scope</c> (<c>GitLabProjectJobTokenScope</c>). Both flags are
///     server-computed booleans, with no GitLab-authored string anywhere in the shape, so this is the bare,
///     unwrapped payload per CLAUDE.md rule 4/DEC-007 rather than going through <see cref="GitLabContent" />.
/// </summary>
public sealed record JobTokenScopeSummary(bool? InboundEnabled, bool? OutboundEnabled);

/// <summary>
///     Result of <c>gitlab_set_job_token_scope</c>. Carries no GitLab-authored text at all (just the flag the
///     caller asked to set), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record JobTokenScopeUpdateResult(bool Enabled);

/// <summary>
///     Result of <c>gitlab_remove_job_token_allowlist_entry</c>. Carries no GitLab-authored text at all (just
///     a confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record AllowlistEntryDeleteResult(bool Removed);