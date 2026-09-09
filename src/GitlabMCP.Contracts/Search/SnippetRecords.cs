namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabSnippet</c>. Title/Description/AuthorUsername/FileNames
///     are GitLab-authored (personal snippets are GLOBAL — anyone can create one — per
///     mcp-untrusted-content's field inventory), so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SnippetSummary(
    long Id,
    string? Title,
    string? Description,
    string? Visibility,
    string? AuthorUsername,
    long? ProjectId,
    IReadOnlyList<string> FileNames,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? WebUrl);

public sealed record SnippetListResult(IReadOnlyList<SnippetSummary> Snippets, bool Truncated);

/// <summary>
///     One file to create in a new multi-file snippet. An MCP tool *input* type only — never returned — so
///     it carries no GitLab-authored data of its own, but it still needs a <c>[JsonSerializable]</c> entry
///     because a bare tool parameter type is a root lookup against the resolver chain.
/// </summary>
public sealed record SnippetFileInput(
    string FilePath,
    string Content);

/// <summary>SnippetId is caller-supplied (echoed back, not GitLab-authored), so this result is returned bare.</summary>
public sealed record SnippetDeleteResult(long SnippetId, bool Deleted);

/// <summary>
///     One file change to apply to an existing multi-file snippet via <c>gitlab_update_snippet</c>. An MCP
///     tool *input* type only — never returned — so it carries no GitLab-authored data of its own, but it
///     still needs a <c>[JsonSerializable]</c> entry because a bare tool parameter type is a root lookup
///     against the resolver chain.
/// </summary>
public sealed record UpdateSnippetFileInput(
    string Action,
    string FilePath,
    string? PreviousPath = null,
    string? Content = null);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabUserAgentDetail</c> — the user-agent/IP/spam-check details
///     GitLab recorded against a snippet, for abuse investigation. <see cref="UserAgent" />/<see cref="IpAddress" />
///     are GitLab-recorded but attacker-controlled (whatever the submitting client sent), so every tool
///     returning this wraps via <see cref="GitLabContent" />. Administrator-only per the library's own remark;
///     a non-admin token gets a 404. <see cref="SnippetId" /> is the caller's own input, echoed back.
/// </summary>
public sealed record SnippetUserAgentDetailSummary(
    long SnippetId,
    string? UserAgent,
    string? IpAddress,
    bool? AkismetSubmitted);