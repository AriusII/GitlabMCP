using GitLab.Client.Models;
using GitlabMCP.Contracts.Discussion;

namespace GitlabMCP.Mapping.Discussion;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> DTOs into the owned Discussion-domain records — never hands back the
///     library's own type.
/// </summary>
public static class DiscussionMapper
{
    /// <summary>
    ///     Max notes embedded inline in a single discussion thread's summary; callers needing more page through
    ///     gitlab_list_notes.
    /// </summary>
    public const int NoteInThreadLimit = 20;

    public static NoteSummary ToSummary(GitLabNote note)
    {
        return new NoteSummary(
            note.Id,
            note.Body,
            note.Author?.Username,
            note.CreatedAt,
            note.UpdatedAt,
            note.System,
            note.Resolvable,
            note.Resolved,
            note.Internal);
    }

    public static DiscussionSummary ToSummary(GitLabDiscussion discussion)
    {
        var allNotes = discussion.Notes?.Select(ToSummary).ToList() ?? [];
        var truncated = allNotes.Count > NoteInThreadLimit;

        return new DiscussionSummary(
            discussion.Id,
            discussion.IndividualNote,
            discussion.Resolvable,
            discussion.Resolved,
            truncated ? allNotes.Take(NoteInThreadLimit).ToList() : allNotes,
            truncated);
    }

    public static DraftNoteSummary ToSummary(GitLabDraftNote draft)
    {
        return new DraftNoteSummary(
            draft.Id,
            draft.Note,
            draft.DiscussionId,
            draft.CommitId,
            draft.ResolveDiscussion);
    }

    public static AwardEmojiSummary ToSummary(GitLabAwardEmoji award)
    {
        return new AwardEmojiSummary(
            award.Id,
            award.Name,
            award.User?.Username,
            award.CreatedAt,
            award.Url?.ToString());
    }

    public static EventSummary ToSummary(GitLabEvent evt)
    {
        return new EventSummary(
            evt.Id,
            evt.ActionName,
            evt.TargetType,
            evt.TargetTitle,
            evt.Author?.Username ?? evt.AuthorUsername,
            evt.CreatedAt,
            evt.ProjectId);
    }

    public static SuggestionResult ToResult(GitLabSuggestion suggestion)
    {
        return new SuggestionResult(
            suggestion.Id,
            suggestion.FromLine,
            suggestion.ToLine,
            suggestion.Appliable,
            suggestion.Applied,
            suggestion.FromContent,
            suggestion.ToContent);
    }
}