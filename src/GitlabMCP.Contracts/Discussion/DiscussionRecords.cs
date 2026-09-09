namespace GitlabMCP.Contracts.Discussion;

/// <summary>
///     Notes, discussions and reactions projection records. Every string here can originate from GitLab
///     (comment bodies, usernames, emoji names, event titles, rendered markdown, suggestion diff content) —
///     every tool returning one of these wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record NoteSummary(
    long Id,
    string? Body,
    string? AuthorUsername,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool? System,
    bool? Resolvable,
    bool? Resolved,
    bool? Internal);

public sealed record NoteListResult(IReadOnlyList<NoteSummary> Notes, bool Truncated);

public sealed record DiscussionSummary(
    string Id,
    bool? IndividualNote,
    bool? Resolvable,
    bool? Resolved,
    IReadOnlyList<NoteSummary> Notes,
    bool Truncated);

public sealed record DiscussionListResult(IReadOnlyList<DiscussionSummary> Discussions, bool Truncated);

public sealed record DraftNoteSummary(
    long Id,
    string? Note,
    string? DiscussionId,
    string? CommitId,
    bool? ResolveDiscussion);

public sealed record DraftNoteListResult(IReadOnlyList<DraftNoteSummary> DraftNotes, bool Truncated);

public sealed record AwardEmojiSummary(
    long Id,
    string? Name,
    string? AuthorUsername,
    DateTimeOffset? CreatedAt,
    string? Url);

public sealed record AwardEmojiListResult(IReadOnlyList<AwardEmojiSummary> Reactions, bool Truncated);

public sealed record RenderedMarkdownResult(string? Html);

public sealed record EventSummary(
    long Id,
    string? ActionName,
    string? TargetType,
    string? TargetTitle,
    string? AuthorUsername,
    DateTimeOffset? CreatedAt,
    long? ProjectId);

public sealed record EventListResult(IReadOnlyList<EventSummary> Events, bool Truncated);

public sealed record SuggestionResult(
    long Id,
    int? FromLine,
    int? ToLine,
    bool? Appliable,
    bool? Applied,
    string? FromContent,
    string? ToContent);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record NoteDeleteResult(long NoteId, bool Deleted);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record DiscussionNoteDeleteResult(long NoteId, bool Deleted);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record AwardEmojiDeleteResult(long AwardId, bool Deleted);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record DraftNoteDeleteResult(long DraftNoteId, bool Deleted);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record DraftNotePublishResult(long DraftNoteId, bool Published);

/// <summary>Bare record: every field is a server-computed scalar, no GitLab-authored string involved.</summary>
public sealed record DraftNotesPublishAllResult(bool Published);