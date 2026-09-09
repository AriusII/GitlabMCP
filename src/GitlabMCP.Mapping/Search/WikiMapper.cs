using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabWikiPage" /> into the owned <see cref="WikiPageSummary" /> — used for both a listed
///     page and a just-created/read-back page.
/// </summary>
public static class WikiMapper
{
    /// <summary>
    ///     Per-page cap on <see cref="WikiPageSummary.Content" />. A wiki page's raw markdown is
    ///     author-controlled and unbounded, and <c>gitlab_list_wiki_pages(withContent: true)</c> can return
    ///     up to 100 of them in one call — without a cap, a handful of large pages could fill the model's
    ///     context window on their own.
    /// </summary>
    private const int MaxWikiContentPreviewChars = 8_000;

    public static WikiPageSummary ToSummary(GitLabWikiPage page)
    {
        var (content, truncated) = Truncate(page.Content);
        return new WikiPageSummary(
            page.Slug,
            page.Title,
            page.Format,
            content,
            truncated,
            page.Encoding);
    }

    private static (string? Content, bool Truncated) Truncate(string? content)
    {
        if (content is null) return (null, false);

        return content.Length > MaxWikiContentPreviewChars
            ? (content[..MaxWikiContentPreviewChars], true)
            : (content, false);
    }

    /// <summary>Projects <see cref="GitLabWikiAttachment" /> into the owned <see cref="WikiAttachmentSummary" />.</summary>
    public static WikiAttachmentSummary ToAttachmentSummary(GitLabWikiAttachment attachment)
    {
        return new WikiAttachmentSummary(
            attachment.FileName,
            attachment.FilePath,
            attachment.Branch,
            attachment.Link?.Markdown);
    }
}