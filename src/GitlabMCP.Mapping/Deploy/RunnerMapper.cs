using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

/// <summary>Projects <see cref="GitLabRunner" /> and runner-controller DTOs into this domain's owned records.</summary>
public static class RunnerMapper
{
    public static RunnerSummary ToSummary(GitLabRunner runner)
    {
        return new RunnerSummary(
            runner.Id,
            runner.Description,
            runner.RunnerType,
            runner.Status,
            runner.Paused,
            runner.Online,
            runner.IsShared,
            runner.Locked,
            runner.TagList ?? [],
            runner.AccessLevel,
            runner.Version,
            runner.IpAddress,
            runner.ContactedAt,
            runner.MaintenanceNote);
    }

    public static RunnerJobSummary ToJobSummary(GitLabJob job)
    {
        return new RunnerJobSummary(
            job.Id,
            job.Status,
            job.Stage,
            job.Name,
            job.Ref,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.Duration,
            job.WebUrl?.ToString());
    }

    public static RunnerManagerSummary ToManagerSummary(GitLabRunnerManager manager)
    {
        return new RunnerManagerSummary(
            manager.Id,
            manager.SystemId,
            manager.Version,
            manager.Revision,
            manager.Platform,
            manager.Architecture,
            manager.CreatedAt,
            manager.ContactedAt,
            manager.IpAddress,
            manager.Status,
            manager.JobExecutionStatus);
    }

    public static RunnerProjectSummary ToProjectSummary(GitLabProject project)
    {
        return new RunnerProjectSummary(
            project.Id,
            project.Name,
            project.PathWithNamespace,
            project.WebUrl?.ToString());
    }

    /// <summary>Deliberately drops <see cref="GitLabRunnerToken.Token" /> — see <see cref="RunnerTokenResetResult" />.</summary>
    public static RunnerTokenResetResult ToTokenResetResult(GitLabRunnerToken token)
    {
        return new RunnerTokenResetResult(
            token.TokenExpiresAt);
    }
}