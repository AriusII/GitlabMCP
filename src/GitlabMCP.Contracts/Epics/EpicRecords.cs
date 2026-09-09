namespace GitlabMCP.Contracts.Epics;

/// <summary>
///     Epic/work-item projection records (DEC-011=C/DEC-020). Every string here is GitLab-authored — every tool
///     returning these wraps via <see cref="GitLabContent" /> (DEC-007).
/// </summary>
public sealed record EpicSummary(
    string Id,
    string Iid,
    string Title,
    string State,
    string? WebUrl,
    string? Description,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Assignees,
    string? StartDate,
    string? DueDate,
    string? HealthStatus);

public sealed record EpicDetail(
    string Id,
    string Iid,
    string Title,
    string State,
    string? WebUrl,
    string? Description,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Assignees,
    string? StartDate,
    string? DueDate,
    string? HealthStatus,
    bool HasParent,
    bool HasChildren,
    string? ParentTitle);

public sealed record EpicListResult(IReadOnlyList<EpicSummary> Epics, bool HasMore, string? NextCursor);

public sealed record EpicDeleteResult(string GroupFullPath);

public sealed record EpicNoteResult(string NoteId, DateTimeOffset CreatedAt);