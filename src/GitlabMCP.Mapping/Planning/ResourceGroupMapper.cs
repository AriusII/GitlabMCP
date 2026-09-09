using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

public static class ResourceGroupMapper
{
    public static ResourceGroupSummary ToSummary(GitLabResourceGroup resourceGroup)
    {
        return new ResourceGroupSummary(
            resourceGroup.Id,
            resourceGroup.Key,
            resourceGroup.ProcessMode,
            resourceGroup.CreatedAt,
            resourceGroup.UpdatedAt);
    }

    public static JobSummary ToJobSummary(GitLabJob job)
    {
        return new JobSummary(
            job.Id,
            job.Status,
            job.Stage,
            job.Name,
            job.Ref,
            job.CreatedAt,
            job.WebUrl?.ToString());
    }

    public static ResourceGroupDetail ToDetail(
        GitLabResourceGroup resourceGroup, JobSummary? currentJob, IReadOnlyList<JobSummary> upcomingJobs,
        bool upcomingJobsTruncated)
    {
        return new ResourceGroupDetail(
            resourceGroup.Id,
            resourceGroup.Key,
            resourceGroup.ProcessMode,
            resourceGroup.CreatedAt,
            resourceGroup.UpdatedAt,
            currentJob,
            upcomingJobs,
            upcomingJobsTruncated);
    }
}