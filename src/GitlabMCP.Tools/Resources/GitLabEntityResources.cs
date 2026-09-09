using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitlabMCP.Contracts;
using GitlabMCP.GraphQL.Operations.WorkItems;
using GitlabMCP.Mapping.Cicd;
using GitlabMCP.Mapping.Code;
using GitlabMCP.Mapping.Deploy;
using GitlabMCP.Mapping.Epics;
using GitlabMCP.Mapping.Planning;
using GitlabMCP.Mapping.Search;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using MergeRequestMapper = GitlabMCP.Mapping.MergeRequests.MergeRequestMapper;

namespace GitlabMCP.Tools.Resources;

/// <summary>
///     Templated resources addressing a single GitLab entity by URI (DEC-029) — lets an MCP client attach
///     one specific GitLab entity as context directly, instead of the model deciding to call a tool for
///     it. Every result carries GitLab-authored text, so every method wraps via
///     <see cref="GitLabContent.WrapResource{T}" /> (DEC-028) — the resource-shaped sibling of DEC-007's
///     tool rule. Reuses the exact same client calls and mappers as the equivalent
///     <c>gitlab_get_*</c> tool, and is granted identically to it (DEC-029/DEC-031).
/// </summary>
[McpServerResourceType]
public sealed class GitLabEntityResources(
    IIssuesClient issues,
    IMergeRequestsClient mergeRequests,
    GitLabWorkItemsClient workItems,
    IRepositoryFilesClient repositoryFiles,
    IWikisClient wikis,
    IPipelinesClient pipelines,
    IReleasesClient releases)
{
    [McpServerResource(Name = "gitlab_issue", UriTemplate = "gitlab://issue/{projectId}/{iid}",
        MimeType = "application/json")]
    [Description("One GitLab issue's full detail, addressable directly as context.")]
    public async Task<ReadResourceResult> GetIssueAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description("The issue's project-scoped iid.")]
        long iid,
        CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(projectId, iid, cancellationToken);
        return GitLabContent.WrapResource(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid",
            $"gitlab://issue/{projectId}/{iid}");
    }

    [McpServerResource(Name = "gitlab_merge_request", UriTemplate = "gitlab://merge_request/{projectId}/{iid}",
        MimeType = "application/json")]
    [Description("One GitLab merge request's full detail, addressable directly as context.")]
    public async Task<ReadResourceResult> GetMergeRequestAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description("The merge request's project-scoped iid.")]
        long iid,
        CancellationToken cancellationToken)
    {
        var mr = await mergeRequests.GetAsync(projectId, iid, cancellationToken);
        return GitLabContent.WrapResource(MergeRequestMapper.ToResult(mr), "projects/:id/merge_requests/:iid",
            $"gitlab://merge_request/{projectId}/{iid}");
    }

    [McpServerResource(Name = "gitlab_epic", UriTemplate = "gitlab://epic/{groupId}/{iid}",
        MimeType = "application/json")]
    [Description("One GitLab epic's full detail (requires Premium/Ultimate), addressable directly as context.")]
    public async Task<ReadResourceResult> GetEpicAsync(
        [Description("Group: numeric id or URL-encoded full path.")]
        string groupId,
        [Description("The epic's iid, as a string.")]
        string iid,
        CancellationToken cancellationToken)
    {
        var node = await workItems.GetEpicByIidAsync(groupId, iid, cancellationToken);
        return GitLabContent.WrapResource(EpicMapper.ToDetail(node), "groups/:id/workItems/:iid",
            $"gitlab://epic/{groupId}/{iid}");
    }

    [McpServerResource(Name = "gitlab_file", UriTemplate = "gitlab://project/{projectId}/file/{filePath}/{refName}",
        MimeType = "application/json")]
    [Description(
        "One repository file's text content (decoded from Base64) at a branch, tag or commit SHA, addressable directly as context.")]
    public async Task<ReadResourceResult> GetFileAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description("Full path from the repository root, URL-encoded (e.g. \"src%2FProgram.cs\").")]
        string filePath,
        [Description("The branch, tag or commit SHA to read at.")]
        string refName,
        CancellationToken cancellationToken)
    {
        var file = await repositoryFiles.GetAsync(projectId, filePath, refName, cancellationToken);
        return GitLabContent.WrapResource(RepositoryMapper.ToContentSummary(file),
            "projects/:id/repository/files/:file_path", $"gitlab://project/{projectId}/file/{filePath}/{refName}");
    }

    [McpServerResource(Name = "gitlab_wiki_page", UriTemplate = "gitlab://wiki/{projectId}/{slug}",
        MimeType = "application/json")]
    [Description("One project wiki page's current content by slug, addressable directly as context.")]
    public async Task<ReadResourceResult> GetWikiPageAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description(
            "The page's slug, URL-encoded (nested pages use \"home%2Fsetup\" form).")]
        string slug,
        CancellationToken cancellationToken)
    {
        var page = await wikis.GetForProjectAsync(projectId, slug, null, false, cancellationToken);
        return GitLabContent.WrapResource(WikiMapper.ToSummary(page), "projects/:id/wikis/:slug",
            $"gitlab://wiki/{projectId}/{slug}");
    }

    [McpServerResource(Name = "gitlab_pipeline", UriTemplate = "gitlab://pipeline/{projectId}/{pipelineId}",
        MimeType = "application/json")]
    [Description("One CI/CD pipeline's full status, timing and metadata by id, addressable directly as context.")]
    public async Task<ReadResourceResult> GetPipelineAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description("The pipeline's numeric id.")]
        long pipelineId,
        CancellationToken cancellationToken)
    {
        var pipeline = await pipelines.GetAsync(projectId, pipelineId, cancellationToken);
        return GitLabContent.WrapResource(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/:pipeline_id",
            $"gitlab://pipeline/{projectId}/{pipelineId}");
    }

    [McpServerResource(Name = "gitlab_release", UriTemplate = "gitlab://release/{projectId}/{tagName}",
        MimeType = "application/json")]
    [Description("One release's notes, milestones and metadata by its Git tag, addressable directly as context.")]
    public async Task<ReadResourceResult> GetReleaseAsync(
        [Description("Project: numeric id or URL-encoded \"namespace/path\".")]
        string projectId,
        [Description("The release's Git tag name.")]
        string tagName,
        CancellationToken cancellationToken)
    {
        var release = await releases.GetAsync(projectId, tagName, cancellationToken);
        return GitLabContent.WrapResource(ReleaseMapper.ToSummary(release), "projects/:id/releases/:tag_name",
            $"gitlab://release/{projectId}/{tagName}");
    }
}