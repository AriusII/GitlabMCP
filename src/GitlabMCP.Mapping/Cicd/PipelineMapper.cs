using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>Projects pipeline and pipeline-schedule DTOs into the owned "cicd" records.</summary>
public static class PipelineMapper
{
    public static PipelineSummary ToSummary(GitLabPipeline pipeline)
    {
        return new PipelineSummary(
            pipeline.Id,
            pipeline.Iid,
            pipeline.ProjectId,
            pipeline.Sha,
            pipeline.Ref,
            pipeline.Status,
            pipeline.Source,
            pipeline.Name,
            pipeline.WebUrl?.ToString(),
            pipeline.CreatedAt,
            pipeline.UpdatedAt,
            pipeline.StartedAt,
            pipeline.FinishedAt,
            pipeline.Duration,
            pipeline.Coverage,
            pipeline.User?.Username);
    }

    public static PipelineScheduleSummary ToSummary(GitLabPipelineSchedule schedule)
    {
        return new PipelineScheduleSummary(
            schedule.Id,
            schedule.Description,
            schedule.Ref,
            schedule.Cron,
            schedule.CronTimezone,
            schedule.NextRunAt,
            schedule.Active,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            schedule.Owner?.Username,
            schedule.LastPipeline?.Id,
            schedule.LastPipeline?.Status);
    }
}