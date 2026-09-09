using System.Text;
using GitLab.Client.Models;
using GitlabMCP.Contracts.Code;

namespace GitlabMCP.Mapping.Code;

/// <summary>Projects branch/tag/blame/file/tree DTOs into the owned "code" records.</summary>
public static class RepositoryMapper
{
    /// <summary>
    ///     Per-range cap on <see cref="BlameRangeSummary.Lines" />. A blame call omitting rangeStart/rangeEnd
    ///     blames the whole file, and a single contiguous run under one old commit can be the entire file's
    ///     line count — without a cap, one such range could dump an entire large source file into context.
    /// </summary>
    private const int MaxLinesPerBlameRange = 200;

    /// <summary>
    ///     Per-call cap on <see cref="FileContentResult.Content" />. Unlike every list tool in this domain,
    ///     <c>gitlab_get_file_content</c> has no caller-supplied size limit, so a large source file needs a
    ///     fixed cap of its own to avoid flooding the model's context.
    /// </summary>
    private const int MaxFileContentPreviewChars = 20_000;

    public static BranchSummary ToSummary(GitLabBranch branch)
    {
        return new BranchSummary(
            branch.Name,
            branch.Commit?.Id,
            branch.Commit?.Title,
            branch.Merged,
            branch.Protected,
            branch.Default,
            branch.WebUrl?.ToString());
    }

    public static TagSummary ToSummary(GitLabTag tag)
    {
        return new TagSummary(
            tag.Name,
            tag.Message,
            tag.Target,
            tag.Commit?.Id,
            tag.Protected);
    }

    public static BlameRangeSummary ToSummary(GitLabBlameRange range)
    {
        var (lines, linesTruncated) = TruncateLines(range.Lines);

        return new BlameRangeSummary(
            range.Commit?.Id,
            range.Commit?.AuthorName,
            range.Commit?.AuthorEmail,
            range.Commit?.AuthoredDate,
            range.Commit?.Message,
            lines,
            linesTruncated);
    }

    private static (IReadOnlyList<string> Lines, bool Truncated) TruncateLines(IReadOnlyList<string>? lines)
    {
        if (lines is null) return ([], false);

        return lines.Count > MaxLinesPerBlameRange
            ? (lines.Take(MaxLinesPerBlameRange).ToList(), true)
            : (lines, false);
    }

    public static RepositoryFileSummary ToSummary(GitLabRepositoryFile file)
    {
        return new RepositoryFileSummary(
            file.FileName,
            file.FilePath,
            file.Ref,
            file.Size,
            file.BlobId,
            file.CommitId,
            file.ExecuteFilemode);
    }

    public static TreeItemSummary ToSummary(GitLabTreeItem item)
    {
        return new TreeItemSummary(
            item.Id,
            item.Name,
            item.Type,
            item.Path,
            item.Mode);
    }

    /// <summary>
    ///     Decodes <see cref="GitLabRepositoryFile.Content" /> from Base64 when GitLab encoded it that way
    ///     (<c>gitlab_get_file_content</c>). Falls back to the raw value on anything that doesn't decode
    ///     cleanly — genuinely binary content is still returned rather than dropped.
    /// </summary>
    public static FileContentResult ToContentSummary(GitLabRepositoryFile file)
    {
        var content = file.Content;

        if (content is not null && string.Equals(file.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            try
            {
                content = Encoding.UTF8.GetString(Convert.FromBase64String(content));
            }
            catch (FormatException)
            {
                // Not valid Base64 despite the declared encoding - return GitLab's raw value rather
                // than fail the whole read.
            }

        var (preview, contentTruncated) = TruncateContent(content);

        return new FileContentResult(
            file.FileName,
            file.FilePath,
            file.Ref,
            file.Size,
            file.BlobId,
            file.CommitId,
            file.LastCommitId,
            file.ExecuteFilemode,
            preview,
            contentTruncated);
    }

    private static (string? Content, bool Truncated) TruncateContent(string? content)
    {
        if (content is null) return (null, false);

        return content.Length > MaxFileContentPreviewChars
            ? (content[..MaxFileContentPreviewChars], true)
            : (content, false);
    }
}