using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

public static class TodoMapper
{
    public static TodoSummary ToSummary(GitLabTodo todo)
    {
        return new TodoSummary(
            todo.Id,
            todo.ActionName,
            todo.State,
            todo.TargetType,
            todo.TargetUrl?.ToString(),
            todo.Project?.PathWithNamespace ?? todo.Group?.FullPath,
            todo.CreatedAt);
    }
}