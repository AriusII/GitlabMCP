namespace GitlabMCP.Contracts.Planning;

public sealed record IterationSummary(
    long Id,
    long? Iid,
    string? Title,
    string? Description,
    string State,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? WebUrl);

public sealed record IterationListResult(IReadOnlyList<IterationSummary> Iterations, bool Truncated);