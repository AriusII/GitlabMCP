using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class RunnerControllerMapper
{
    public static RunnerControllerSummary ToSummary(GitLabRunnerController controller)
    {
        return new RunnerControllerSummary(
            controller.Id,
            controller.Description,
            controller.State?.ToString(),
            controller.CreatedAt,
            controller.UpdatedAt,
            controller.Connected);
    }

    public static RunnerControllerScopeSummary ToScopeSummary(GitLabRunnerControllerScope scope)
    {
        return new RunnerControllerScopeSummary(
            scope.RunnerId,
            scope.CreatedAt,
            scope.UpdatedAt);
    }

    public static RunnerControllerTokenSummary ToTokenSummary(GitLabRunnerControllerToken token)
    {
        return new RunnerControllerTokenSummary(
            token.Id,
            token.RunnerControllerId,
            token.Description,
            token.CreatedAt,
            token.LastUsedAt);
    }

    /// <summary>
    ///     Deliberately drops <see cref="GitLabRunnerControllerTokenWithSecret.Token" /> - the
    ///     secret is never returned by this server (CLAUDE.md's closed secret-field list).
    /// </summary>
    public static RunnerControllerTokenSummary ToTokenSummary(GitLabRunnerControllerTokenWithSecret token)
    {
        return new RunnerControllerTokenSummary(
            token.Id,
            token.RunnerControllerId,
            token.Description,
            token.CreatedAt,
            token.LastUsedAt);
    }
}