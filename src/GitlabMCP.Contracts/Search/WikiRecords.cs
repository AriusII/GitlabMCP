namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabWikiPage</c> — used both for a listed page and for the
///     page just created/read back. Title/Content/Format are wiki-editor-authored (member roles per
///     mcp-untrusted-content's field inventory), so every tool returning this wraps via
///     <see cref="GitLabContent" />. <see cref="Content" /> is capped per page (see
///     <c>WikiMapper.MaxWikiContentPreviewChars</c>) so a handful of large pages cannot fill the model's
///     context window on their own; <see cref="ContentTruncated" /> reports whether this page's content was cut.
/// </summary>
public sealed record WikiPageSummary(
    string Slug,
    string? Title,
    string? Format,
    string? Content,
    bool ContentTruncated,
    string? Encoding);

public sealed record WikiPageListResult(IReadOnlyList<WikiPageSummary> Pages, bool Truncated);

/// <summary>Slug is caller-supplied (echoed back, not GitLab-authored), so this result is returned bare.</summary>
public sealed record WikiPageDeleteResult(string Slug, bool Deleted);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabWikiAttachment</c>. <see cref="Markdown" /> is
///     GitLab-generated ready-to-paste markdown embedding the uploaded file, so every tool returning this
///     wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record WikiAttachmentSummary(
    string? FileName,
    string? FilePath,
    string? Branch,
    string? Markdown);