using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabSnippet" /> into the owned <see cref="SnippetSummary" /> — never hands back the
///     library DTO.
/// </summary>
public static class SnippetMapper
{
    public static SnippetSummary ToSummary(GitLabSnippet snippet)
    {
        return new SnippetSummary(
            snippet.Id,
            snippet.Title,
            snippet.Description,
            snippet.Visibility?.ToString(),
            snippet.Author?.Username,
            snippet.ProjectId,
            snippet.Files?.Select(static f => f.Path).OfType<string>().ToList() ?? [],
            snippet.CreatedAt,
            snippet.UpdatedAt,
            snippet.WebUrl?.ToString());
    }

    public static SnippetUserAgentDetailSummary ToUserAgentDetail(long snippetId, GitLabUserAgentDetail detail)
    {
        return new SnippetUserAgentDetailSummary(
            snippetId,
            detail.UserAgent,
            detail.IpAddress,
            detail.AkismetSubmitted);
    }
}