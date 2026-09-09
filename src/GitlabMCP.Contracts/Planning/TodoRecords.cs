namespace GitlabMCP.Contracts.Planning;

public sealed record TodoSummary(
    long Id,
    string? ActionName,
    string? State,
    string? TargetType,
    string? TargetUrl,
    string? ProjectPath,
    DateTimeOffset? CreatedAt);

public sealed record TodoListResult(IReadOnlyList<TodoSummary> Todos, bool Truncated);

/// <summary>Bare record: <c>ITodosClient.MarkAllAsDoneAsync</c> returns no body.</summary>
public sealed record MarkAllTodosDoneResult(bool Done);