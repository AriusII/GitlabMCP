using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>
///     Projects <see cref="GitLabPipeline" /> into the owned <see cref="PackagePipelineSummary" />, for the
///     package-provenance ("which pipeline built this package") view specifically.
/// </summary>
public static class PackagePipelineMapper
{
    public static PackagePipelineSummary ToSummary(GitLabPipeline pipeline)
    {
        return new PackagePipelineSummary(
            pipeline.Id,
            pipeline.Iid,
            pipeline.Status,
            pipeline.Ref,
            pipeline.Sha,
            pipeline.CreatedAt,
            pipeline.WebUrl?.ToString());
    }
}