using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>Projects job and job-artifact-entry DTOs into the owned "cicd" records.</summary>
public static class JobMapper
{
    public static JobSummary ToSummary(GitLabJob job)
    {
        return new JobSummary(
            job.Id,
            job.Status,
            job.Stage,
            job.Name,
            job.Ref,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.Duration,
            job.WebUrl?.ToString(),
            job.AllowFailure,
            job.Coverage,
            job.FailureReason,
            job.User?.Username,
            job.Pipeline?.Id,
            job.Commit?.ShortId,
            job.DownstreamPipeline?.Id);
    }

    public static JobArtifactEntrySummary ToSummary(GitLabJobArtifactEntry entry)
    {
        return new JobArtifactEntrySummary(
            entry.Name,
            entry.Path,
            entry.Type?.ToString(),
            entry.Size,
            entry.Mode);
    }
}