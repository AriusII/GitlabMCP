using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Discussion;
using GitlabMCP.Mapping.Discussion;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Notes, discussions & reactions tools. Every payload here can carry GitLab-authored text (comment
///     bodies, usernames, emoji names, rendered markdown, event titles, suggestion diff content), so every
///     tool wraps its result via <see cref="GitLabContent" />.
/// </summary>
[McpServerToolType]
public sealed class DiscussionTools(
    IDiscussionsClient discussions,
    INotesClient notes,
    IDraftNotesClient draftNotes,
    IAwardEmojiClient awardEmoji,
    IMarkdownClient markdown,
    IEventsClient events,
    ISuggestionsClient suggestions)
{
    private const int MaxLimit = 100;

    private static long ParseId(string value, string parameterName)
    {
        if (!long.TryParse(value, out var id)) throw new McpException($"{parameterName} must be a numeric id.");

        return id;
    }

    private static string RequireNoteId(string? noteId)
    {
        return noteId ?? throw new McpException("noteId is required when the target is a note.");
    }

    [McpServerTool(Name = "gitlab_list_discussions", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the threaded, resolvable discussion threads on an issue, merge request, commit, snippet or epic. " +
        "Each entry embeds up to 20 of its own notes (each entry's own truncated " +
        "flag says whether it holds more) -- use gitlab_list_discussion_notes to page through the rest of one " +
        "specific thread. Bounded by limit.")]
    public async Task<CallToolResult> ListDiscussionsAsync(
        [Description(
            "Which kind of thing carries the discussions: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string. For commit, the full commit SHA instead.")]
        string id,
        [Description("Maximum discussions to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var stream = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.ListForIssueAsync(projectOrGroup, ParseId(id, "id"), cancellationToken),
            "merge_request" => discussions.ListForMergeRequestAsync(projectOrGroup, ParseId(id, "id"),
                cancellationToken),
            "commit" => discussions.ListForCommitAsync(projectOrGroup, id, cancellationToken),
            "snippet" => discussions.ListForSnippetAsync(projectOrGroup, ParseId(id, "id"), cancellationToken),
            "epic" => discussions.ListForEpicAsync(projectOrGroup, ParseId(id, "id"), cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        List<DiscussionSummary> collected = [];
        var truncated = false;

        await foreach (var discussion in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(discussion));
        }

        return GitLabContent.Wrap(new DiscussionListResult(collected, truncated),
            "projects|groups/:id/{noteable}/:iid/discussions");
    }

    [McpServerTool(Name = "gitlab_get_discussion", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Reads one discussion thread and up to 20 of its notes in one call. " +
        "The result's truncated flag says whether the thread holds more -- use gitlab_list_discussion_notes to page " +
        "through the rest.")]
    public async Task<CallToolResult> GetDiscussionAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        CancellationToken cancellationToken = default)
    {
        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.GetForIssueAsync(projectOrGroup, ParseId(id, "id"), discussionId, cancellationToken),
            "merge_request" => discussions.GetForMergeRequestAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                cancellationToken),
            "commit" => discussions.GetForCommitAsync(projectOrGroup, id, discussionId, cancellationToken),
            "snippet" => discussions.GetForSnippetAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                cancellationToken),
            "epic" => discussions.GetForEpicAsync(projectOrGroup, ParseId(id, "id"), discussionId, cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        var discussion = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(discussion),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id");
    }

    [McpServerTool(Name = "gitlab_list_discussion_notes", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every note inside one specific discussion thread, bounded by limit. Use this to page through a " +
        "thread whose notes gitlab_list_discussions or gitlab_get_discussion reported as truncated.")]
    public async Task<CallToolResult> ListDiscussionNotesAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("Maximum notes to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var stream = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.ListNotesInIssueDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                cancellationToken),
            "merge_request" => discussions.ListNotesInMergeRequestDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, cancellationToken),
            "commit" => discussions.ListNotesInCommitDiscussionAsync(projectOrGroup, id, discussionId,
                cancellationToken),
            "snippet" => discussions.ListNotesInSnippetDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                cancellationToken),
            "epic" => discussions.ListNotesInEpicDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        List<NoteSummary> collected = [];
        var truncated = false;

        await foreach (var note in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(note));
        }

        return GitLabContent.Wrap(new NoteListResult(collected, truncated),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id/notes");
    }

    [McpServerTool(Name = "gitlab_create_discussion", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Starts a new resolvable discussion thread on an issue, merge request, commit, snippet or epic. On merge_request or commit, supply the diff-position fields to anchor it to a line and make it a review comment instead of a plain one.")]
    public async Task<CallToolResult> CreateDiscussionAsync(
        [Description(
            "Which kind of thing to start the discussion on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's opening comment body, in GitLab-flavored Markdown.")]
        string body,
        [Description(
            "Diff anchoring (merge_request/commit only): the base commit SHA of the diff being commented on. Omit for a plain, unanchored discussion.")]
        string? baseSha = null,
        [Description(
            "Diff anchoring (merge_request/commit only): the start commit SHA of the diff being commented on.")]
        string? startSha = null,
        [Description("Diff anchoring (merge_request/commit only): the head commit SHA of the diff being commented on.")]
        string? headSha = null,
        [Description(
            "Diff anchoring (merge_request/commit only): the file path on the old (before) side of the diff. Omit for a new file.")]
        string? oldPath = null,
        [Description(
            "Diff anchoring (merge_request/commit only): the file path on the new (after) side of the diff. Omit for a deleted file.")]
        string? newPath = null,
        [Description(
            "Diff anchoring (merge_request/commit only): the line number on the old side to anchor to. Omit if the line is only on the new side.")]
        int? oldLine = null,
        [Description(
            "Diff anchoring (merge_request/commit only): the line number on the new side to anchor to. Omit if the line is only on the old side.")]
        int? newLine = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = noteableType.ToLowerInvariant();

        GitLabNotePosition? position = null;

        if (baseSha is not null || startSha is not null || headSha is not null || oldPath is not null ||
            newPath is not null || oldLine is not null || newLine is not null)
        {
            if (normalizedType is not ("merge_request" or "commit"))
                throw new McpException(
                    "Diff anchoring fields (baseSha/startSha/headSha/oldPath/newPath/oldLine/newLine) are only valid when noteableType is \"merge_request\" or \"commit\".");

            if (baseSha is null || startSha is null || headSha is null)
                throw new McpException(
                    "baseSha, startSha, and headSha must all be supplied together to anchor a discussion to a diff line.");

            position = new GitLabNotePosition
            {
                PositionType = GitLabNotePositionType.Text,
                BaseSha = baseSha,
                StartSha = startSha,
                HeadSha = headSha,
                OldPath = oldPath,
                NewPath = newPath,
                OldLine = oldLine,
                NewLine = newLine
            };
        }

        var request = new CreateDiscussionRequest { Body = body, Position = position };

        var task = normalizedType switch
        {
            "issue" => discussions.CreateForIssueAsync(projectOrGroup, ParseId(id, "id"), request, cancellationToken),
            "merge_request" => discussions.CreateForMergeRequestAsync(projectOrGroup, ParseId(id, "id"), request,
                cancellationToken),
            "commit" => discussions.CreateForCommitAsync(projectOrGroup, id, request, cancellationToken),
            "snippet" => discussions.CreateForSnippetAsync(projectOrGroup, ParseId(id, "id"), request,
                cancellationToken),
            "epic" => discussions.CreateForEpicAsync(projectOrGroup, ParseId(id, "id"), request, cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        var discussion = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(discussion),
            "projects|groups/:id/{noteable}/:iid/discussions (create)");
    }

    [McpServerTool(Name = "gitlab_list_notes", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the flat comments and system-generated notes on an issue, merge request, snippet, vulnerability, wiki page or epic, bounded by limit.")]
    public async Task<CallToolResult> ListNotesAsync(
        [Description(
            "Which kind of thing carries the notes: \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", or \"group_wiki_page\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/snippet/vulnerability/project_wiki_page: the project's numeric id or \"namespace/path\". For epic/group_wiki_page: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier as a numeric string: the issue/merge-request iid, the snippet/vulnerability numeric database id, the epic iid, or (for a wiki page) its numeric wiki-page meta id -- not the page slug.")]
        string id,
        [Description("Maximum notes to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new NoteListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        var parsedId = ParseId(id, "id");

        var stream = noteableType.ToLowerInvariant() switch
        {
            "issue" => notes.ListIssueNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            "merge_request" => notes.ListMergeRequestNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            "snippet" => notes.ListSnippetNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            "vulnerability" => notes.ListVulnerabilityNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            "project_wiki_page" => notes.ListProjectWikiPageNotesAsync(projectOrGroup, parsedId, options,
                cancellationToken),
            "epic" => notes.ListEpicNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            "group_wiki_page" =>
                notes.ListGroupWikiPageNotesAsync(projectOrGroup, parsedId, options, cancellationToken),
            _ => throw new McpException(
                "noteableType must be one of \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", \"group_wiki_page\".")
        };

        List<NoteSummary> collected = [];
        var truncated = false;

        await foreach (var note in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(note));
        }

        return GitLabContent.Wrap(new NoteListResult(collected, truncated),
            "projects|groups/:id/{noteable}/:iid/notes");
    }

    [McpServerTool(Name = "gitlab_update_note", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Replaces the body of an existing note the caller authored (or has maintainer rights over). The previous body is not recoverable once replaced.")]
    public async Task<CallToolResult> UpdateNoteAsync(
        [Description(
            "Which kind of thing the note is on: \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", or \"group_wiki_page\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/snippet/vulnerability/project_wiki_page: the project's numeric id or \"namespace/path\". For epic/group_wiki_page: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier as a numeric string: the issue/merge-request iid, the snippet/vulnerability numeric database id, the epic iid, or (for a wiki page) its numeric wiki-page meta id -- not the page slug.")]
        string id,
        [Description("The note's numeric id (from gitlab_list_notes).")]
        string noteId,
        [Description("The new body to replace the note's current text with, in GitLab-flavored Markdown.")]
        string body,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateNoteRequest { Body = body };
        var parsedId = ParseId(id, "id");
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => notes.UpdateIssueNoteAsync(projectOrGroup, parsedId, parsedNoteId, request, cancellationToken),
            "merge_request" => notes.UpdateMergeRequestNoteAsync(projectOrGroup, parsedId, parsedNoteId, request,
                cancellationToken),
            "snippet" => notes.UpdateSnippetNoteAsync(projectOrGroup, parsedId, parsedNoteId, request,
                cancellationToken),
            "vulnerability" => notes.UpdateVulnerabilityNoteAsync(projectOrGroup, parsedId, parsedNoteId, request,
                cancellationToken),
            "project_wiki_page" => notes.UpdateProjectWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId, request,
                cancellationToken),
            "epic" => notes.UpdateEpicNoteAsync(projectOrGroup, parsedId, parsedNoteId, request, cancellationToken),
            "group_wiki_page" => notes.UpdateGroupWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId, request,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be one of \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", \"group_wiki_page\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/notes/:note_id (update)");
    }

    [McpServerTool(Name = "gitlab_list_draft_notes", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the caller's own pending, unpublished review comments on a merge request, bounded by limit.")]
    public async Task<CallToolResult> ListDraftNotesAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("Maximum draft notes to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<DraftNoteSummary> collected = [];
        var truncated = false;

        await foreach (var draft in draftNotes.ListAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"),
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(draft));
        }

        return GitLabContent.Wrap(new DraftNoteListResult(collected, truncated),
            "projects/:id/merge_requests/:iid/draft_notes");
    }

    [McpServerTool(Name = "gitlab_create_draft_note", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Stages a review comment on a merge request without publishing it yet. Optionally posts it as a reply to an existing discussion, and optionally resolves that discussion once the draft is published.")]
    public async Task<CallToolResult> CreateDraftNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The draft comment's body, in GitLab-flavored Markdown.")]
        string note,
        [Description(
            "Optional discussion id (a 40-character hex string) to post this draft as a reply to. Omit to start a new discussion when published.")]
        string? inReplyToDiscussionId = null,
        [Description(
            "If true, publishing this draft also resolves the discussion it replies to. Default false. Ignored when inReplyToDiscussionId is omitted.")]
        bool resolveDiscussion = false,
        [Description(
            "Optional commit SHA to anchor this draft to, for a diff-anchored review comment. Omit for a plain comment.")]
        string? commitId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDraftNoteRequest
        {
            Note = note,
            InReplyToDiscussionId = inReplyToDiscussionId,
            ResolveDiscussion = resolveDiscussion,
            CommitId = commitId
        };

        var draft = await draftNotes.CreateAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"), request,
            cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(draft),
            "projects/:id/merge_requests/:iid/draft_notes (create)");
    }

    [McpServerTool(Name = "gitlab_list_reactions", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the emoji reactions on an issue, merge request, snippet, epic, or a note on any of them, bounded by limit.")]
    public async Task<CallToolResult> ListReactionsAsync(
        [Description(
            "What carries the reactions: \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", or \"epic_note\".")]
        string targetType,
        [Description(
            "For issue/issue_note/merge_request/merge_request_note/snippet/snippet_note: the project's numeric id or \"namespace/path\". For epic/epic_note: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string.")]
        string id,
        [Description("The note's numeric id. Required when targetType ends in \"_note\"; omit otherwise.")]
        string? noteId = null,
        [Description("Maximum reactions to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var parsedId = ParseId(id, "id");

        var stream = targetType.ToLowerInvariant() switch
        {
            "issue" => awardEmoji.ListForIssueAsync(projectOrGroup, parsedId, cancellationToken),
            "issue_note" => awardEmoji.ListForIssueNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), cancellationToken),
            "merge_request" => awardEmoji.ListForMergeRequestAsync(projectOrGroup, parsedId, cancellationToken),
            "merge_request_note" => awardEmoji.ListForMergeRequestNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), cancellationToken),
            "snippet" => awardEmoji.ListForSnippetAsync(projectOrGroup, parsedId, cancellationToken),
            "snippet_note" => awardEmoji.ListForSnippetNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), cancellationToken),
            "epic" => awardEmoji.ListForEpicAsync(projectOrGroup, parsedId, cancellationToken),
            "epic_note" => awardEmoji.ListForEpicNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), cancellationToken),
            _ => throw new McpException(
                "targetType must be one of \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", \"epic_note\".")
        };

        List<AwardEmojiSummary> collected = [];
        var truncated = false;

        await foreach (var award in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(award));
        }

        return GitLabContent.Wrap(new AwardEmojiListResult(collected, truncated),
            "projects|groups/:id/{target}/:iid/award_emoji");
    }

    [McpServerTool(Name = "gitlab_add_reaction", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Reacts with an emoji to an issue, merge request, snippet, epic, or a note on any of them. The name is the emoji's short name without colons, e.g. \"thumbsup\".")]
    public async Task<CallToolResult> AddReactionAsync(
        [Description(
            "What to react to: \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", or \"epic_note\".")]
        string targetType,
        [Description(
            "For issue/issue_note/merge_request/merge_request_note/snippet/snippet_note: the project's numeric id or \"namespace/path\". For epic/epic_note: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string.")]
        string id,
        [Description("The emoji's short name without colons, e.g. \"thumbsup\", \"tada\", \"eyes\".")]
        string name,
        [Description("The note's numeric id. Required when targetType ends in \"_note\"; omit otherwise.")]
        string? noteId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateAwardEmojiRequest { Name = name };
        var parsedId = ParseId(id, "id");

        var task = targetType.ToLowerInvariant() switch
        {
            "issue" => awardEmoji.AddToIssueAsync(projectOrGroup, parsedId, request, cancellationToken),
            "issue_note" => awardEmoji.AddToIssueNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), request, cancellationToken),
            "merge_request" => awardEmoji.AddToMergeRequestAsync(projectOrGroup, parsedId, request, cancellationToken),
            "merge_request_note" => awardEmoji.AddToMergeRequestNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), request, cancellationToken),
            "snippet" => awardEmoji.AddToSnippetAsync(projectOrGroup, parsedId, request, cancellationToken),
            "snippet_note" => awardEmoji.AddToSnippetNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), request, cancellationToken),
            "epic" => awardEmoji.AddToEpicAsync(projectOrGroup, parsedId, request, cancellationToken),
            "epic_note" => awardEmoji.AddToEpicNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), request, cancellationToken),
            _ => throw new McpException(
                "targetType must be one of \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", \"epic_note\".")
        };

        var award = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(award),
            "projects|groups/:id/{target}/:iid/award_emoji (create)");
    }

    [McpServerTool(Name = "gitlab_render_markdown", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Previews how GitLab Flavored Markdown (or plain CommonMark) will render to HTML, optionally resolving #/!/@/~ references against a project.")]
    public async Task<CallToolResult> RenderMarkdownAsync(
        [Description("The markdown source text to render.")]
        string text,
        [Description(
            "If true (default), render as GitLab Flavored Markdown with #/!/@/~ reference expansion. If false, render as plain CommonMark with no reference resolution.")]
        bool gfm = true,
        [Description(
            "Optional project (numeric id or \"namespace/path\") to resolve #/!/@/~ references against. Omit to render without reference resolution.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RenderMarkdownRequest { Text = text, Gfm = gfm, Project = project };
        var rendered = await markdown.RenderAsync(request, cancellationToken);
        return GitLabContent.Wrap(new RenderedMarkdownResult(rendered.Html), "markdown");
    }

    [McpServerTool(Name = "gitlab_list_events", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists recent activity-feed events for the caller, a project, or a user, bounded by limit. This is not an audit log: it excludes epic and merge-request events and is retention-limited.")]
    public async Task<CallToolResult> ListEventsAsync(
        [Description("Whose events to list: \"me\" (the authenticated user), \"project\", or \"user\".")]
        string scope,
        [Description(
            "Required when scope is \"project\": the project's numeric id or \"namespace/path\". Ignored otherwise.")]
        string? project = null,
        [Description("Required when scope is \"user\": the user's numeric id, as a string. Ignored otherwise.")]
        string? userId = null,
        [Description("Maximum events to return (1-100). Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new EventListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        var stream = scope.ToLowerInvariant() switch
        {
            "me" => events.ListAsync(options, cancellationToken),
            "project" => events.ListForProjectAsync(
                project ?? throw new McpException("project is required when scope is \"project\"."),
                options,
                cancellationToken),
            "user" => events.ListForUserAsync(
                ParseId(userId ?? throw new McpException("userId is required when scope is \"user\"."), "userId"),
                options,
                cancellationToken),
            _ => throw new McpException("scope must be \"me\", \"project\", or \"user\".")
        };

        List<EventSummary> collected = [];
        var truncated = false;

        await foreach (var evt in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DiscussionMapper.ToSummary(evt));
        }

        return GitLabContent.Wrap(new EventListResult(collected, truncated),
            "events|projects/:id/events|users/:id/events");
    }

    [McpServerTool(Name = "gitlab_apply_suggestion", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Applies one code suggestion from a review comment, committing it to the merge request's source branch. This cannot be undone by re-applying. The caller must already have the suggestion id -- it is not discoverable through the other tools on this server.")]
    public async Task<CallToolResult> ApplySuggestionAsync(
        [Description("The suggestion's numeric id, as a string.")]
        string suggestionId,
        [Description(
            "Optional custom commit message for the applied suggestion. Omit to use GitLab's default message.")]
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ApplySuggestionRequest { CommitMessage = commitMessage };
        var suggestion =
            await suggestions.ApplyAsync(ParseId(suggestionId, "suggestionId"), request, cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToResult(suggestion), "suggestions/:id/apply");
    }

    [McpServerTool(Name = "gitlab_get_note", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Fetches one specific note (comment) by its numeric id once its parent and id are known.")]
    public async Task<CallToolResult> GetNoteAsync(
        [Description(
            "Which kind of thing the note is on: \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", or \"group_wiki_page\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/snippet/vulnerability/project_wiki_page: the project's numeric id or \"namespace/path\". For epic/group_wiki_page: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier as a numeric string: the issue/merge-request iid, the snippet/vulnerability numeric database id, the epic iid, or (for a wiki page) its numeric wiki-page meta id -- not the page slug.")]
        string id,
        [Description("The note's numeric id, as a string (from gitlab_list_notes).")]
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var parsedId = ParseId(id, "id");
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => notes.GetIssueNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "merge_request" =>
                notes.GetMergeRequestNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "snippet" => notes.GetSnippetNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "vulnerability" => notes.GetVulnerabilityNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            "project_wiki_page" => notes.GetProjectWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            "epic" => notes.GetEpicNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "group_wiki_page" => notes.GetGroupWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be one of \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", \"group_wiki_page\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/notes/:note_id");
    }

    [McpServerTool(Name = "gitlab_add_note", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Posts a stand-alone comment on an issue, merge request, snippet, vulnerability, wiki page or epic. Unlike gitlab_create_discussion, this does not start a resolvable thread.")]
    public async Task<CallToolResult> AddNoteAsync(
        [Description(
            "Which kind of thing to comment on: \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", or \"group_wiki_page\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/snippet/vulnerability/project_wiki_page: the project's numeric id or \"namespace/path\". For epic/group_wiki_page: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier as a numeric string: the issue/merge-request iid, the snippet/vulnerability numeric database id, the epic iid, or (for a wiki page) its numeric wiki-page meta id -- not the page slug.")]
        string id,
        [Description("The comment body, in GitLab-flavored Markdown.")]
        string body,
        [Description("Creates the note as an internal note, visible only to project or group members. Default false.")]
        bool internalNote = false,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateNoteRequest { Body = body, Internal = internalNote };
        var parsedId = ParseId(id, "id");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => notes.CreateIssueNoteAsync(projectOrGroup, parsedId, request, cancellationToken),
            "merge_request" => notes.CreateMergeRequestNoteAsync(projectOrGroup, parsedId, request, cancellationToken),
            "snippet" => notes.CreateSnippetNoteAsync(projectOrGroup, parsedId, request, cancellationToken),
            "vulnerability" => notes.CreateVulnerabilityNoteAsync(projectOrGroup, parsedId, request, cancellationToken),
            "project_wiki_page" => notes.CreateProjectWikiPageNoteAsync(projectOrGroup, parsedId, request,
                cancellationToken),
            "epic" => notes.CreateEpicNoteAsync(projectOrGroup, parsedId, request, cancellationToken),
            "group_wiki_page" => notes.CreateGroupWikiPageNoteAsync(projectOrGroup, parsedId, request,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be one of \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", \"group_wiki_page\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/notes (create)");
    }

    [McpServerTool(Name = "gitlab_delete_note", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently removes a note (comment). This cannot be undone.")]
    public async Task<NoteDeleteResult> DeleteNoteAsync(
        [Description(
            "Which kind of thing the note is on: \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", or \"group_wiki_page\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/snippet/vulnerability/project_wiki_page: the project's numeric id or \"namespace/path\". For epic/group_wiki_page: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier as a numeric string: the issue/merge-request iid, the snippet/vulnerability numeric database id, the epic iid, or (for a wiki page) its numeric wiki-page meta id -- not the page slug.")]
        string id,
        [Description("The note's numeric id, as a string (from gitlab_list_notes).")]
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var parsedId = ParseId(id, "id");
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => notes.DeleteIssueNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "merge_request" => notes.DeleteMergeRequestNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            "snippet" => notes.DeleteSnippetNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "vulnerability" => notes.DeleteVulnerabilityNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            "project_wiki_page" => notes.DeleteProjectWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            "epic" => notes.DeleteEpicNoteAsync(projectOrGroup, parsedId, parsedNoteId, cancellationToken),
            "group_wiki_page" => notes.DeleteGroupWikiPageNoteAsync(projectOrGroup, parsedId, parsedNoteId,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be one of \"issue\", \"merge_request\", \"snippet\", \"vulnerability\", \"project_wiki_page\", \"epic\", \"group_wiki_page\".")
        };

        await task;
        return new NoteDeleteResult(parsedNoteId, true);
    }

    [McpServerTool(Name = "gitlab_resolve_discussion", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Marks a whole discussion thread resolved, or reopens it, on an issue, merge request or epic. GitLab has no route to resolve an entire thread on a commit or snippet.")]
    public async Task<CallToolResult> ResolveDiscussionAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", or \"epic\". (\"commit\" and \"snippet\" are rejected -- GitLab has no route to resolve a whole thread there.)")]
        string noteableType,
        [Description(
            "For issue/merge_request: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description("The parent's iid (issue, merge request, or epic), as a numeric string.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("True to mark the thread resolved, false to reopen it. Default true.")]
        bool resolved = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = noteableType.ToLowerInvariant();

        if (normalizedType is "commit" or "snippet")
            throw new McpException(
                "GitLab has no route to resolve a whole commit or snippet discussion thread; resolve individual notes instead where supported.");

        var request = new ResolveDiscussionRequest { Resolved = resolved };
        var parsedId = ParseId(id, "id");

        var task = normalizedType switch
        {
            "issue" => discussions.ResolveForIssueAsync(projectOrGroup, parsedId, discussionId, request,
                cancellationToken),
            "merge_request" => discussions.ResolveForMergeRequestAsync(projectOrGroup, parsedId, discussionId, request,
                cancellationToken),
            "epic" => discussions.ResolveForEpicAsync(projectOrGroup, parsedId, discussionId, request,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", or \"epic\" (\"commit\" and \"snippet\" have no thread-resolve route).")
        };

        var discussion = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(discussion),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id (resolve)");
    }

    [McpServerTool(Name = "gitlab_reply_to_discussion", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a reply note into an existing discussion thread, instead of starting a new one with gitlab_create_discussion.")]
    public async Task<CallToolResult> ReplyToDiscussionAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request/epic iid, as a numeric string, or the snippet's numeric database id. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("The reply body, in GitLab-flavored Markdown.")]
        string body,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDiscussionNoteRequest { Body = body };

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.AddNoteToIssueDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                request, cancellationToken),
            "merge_request" => discussions.AddNoteToMergeRequestDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, request, cancellationToken),
            "commit" => discussions.AddNoteToCommitDiscussionAsync(projectOrGroup, id, discussionId, request,
                cancellationToken),
            "snippet" => discussions.AddNoteToSnippetDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                request, cancellationToken),
            "epic" => discussions.AddNoteToEpicDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId, request,
                cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id/notes (reply)");
    }

    [McpServerTool(Name = "gitlab_get_discussion_note", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Reads a single reply note inside a discussion thread by id.")]
    public async Task<CallToolResult> GetDiscussionNoteAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request/epic iid, as a numeric string, or the snippet's numeric database id. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("The note's numeric id, as a string.")]
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.GetNoteInIssueDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, cancellationToken),
            "merge_request" => discussions.GetNoteInMergeRequestDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, parsedNoteId, cancellationToken),
            "commit" => discussions.GetNoteInCommitDiscussionAsync(projectOrGroup, id, discussionId, parsedNoteId,
                cancellationToken),
            "snippet" => discussions.GetNoteInSnippetDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, cancellationToken),
            "epic" => discussions.GetNoteInEpicDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id/notes/:note_id");
    }

    [McpServerTool(Name = "gitlab_update_discussion_note", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Rewrites the body of a reply note inside a discussion thread. The previous body is not recoverable once replaced.")]
    public async Task<CallToolResult> UpdateDiscussionNoteAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request/epic iid, as a numeric string, or the snippet's numeric database id. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("The note's numeric id, as a string.")]
        string noteId,
        [Description("The new body to replace the note's current text with, in GitLab-flavored Markdown.")]
        string body,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateDiscussionNoteRequest { Body = body };
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.UpdateNoteInIssueDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, request, cancellationToken),
            "merge_request" => discussions.UpdateNoteInMergeRequestDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, parsedNoteId, request, cancellationToken),
            "commit" => discussions.UpdateNoteInCommitDiscussionAsync(projectOrGroup, id, discussionId, parsedNoteId,
                request, cancellationToken),
            "snippet" => discussions.UpdateNoteInSnippetDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, request, cancellationToken),
            "epic" => discussions.UpdateNoteInEpicDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, request, cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        var note = await task;
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects|groups/:id/{noteable}/:iid/discussions/:discussion_id/notes/:note_id (update)");
    }

    [McpServerTool(Name = "gitlab_resolve_discussion_note", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Resolves or unresolves a single diff note inside a merge request discussion. Note-level resolve exists only for merge requests; GitLab rejects it for any other noteable type.")]
    public async Task<CallToolResult> ResolveDiscussionNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("The note's numeric id, as a string.")]
        string noteId,
        [Description("True to mark the note resolved, false to unresolve it. Default true.")]
        bool resolved = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ResolveDiscussionRequest { Resolved = resolved };
        var note = await discussions.ResolveNoteInMergeRequestDiscussionAsync(
            project, ParseId(mergeRequestIid, "mergeRequestIid"), discussionId, ParseId(noteId, "noteId"), request,
            cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(note),
            "projects/:id/merge_requests/:iid/discussions/:discussion_id/notes/:note_id (resolve)");
    }

    [McpServerTool(Name = "gitlab_delete_discussion_note", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a single reply note from inside a discussion thread. This cannot be undone.")]
    public async Task<DiscussionNoteDeleteResult> DeleteDiscussionNoteAsync(
        [Description(
            "Which kind of thing the discussion is on: \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")]
        string noteableType,
        [Description(
            "For issue/merge_request/commit/snippet: the project's numeric id or \"namespace/path\". For epic: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The parent's identifier: the issue/merge-request/epic iid, as a numeric string, or the snippet's numeric database id. For commit, the full commit SHA instead.")]
        string id,
        [Description("The discussion's id -- a 40-character hex string, not a number (from gitlab_list_discussions).")]
        string discussionId,
        [Description("The note's numeric id, as a string.")]
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var parsedNoteId = ParseId(noteId, "noteId");

        var task = noteableType.ToLowerInvariant() switch
        {
            "issue" => discussions.DeleteNoteFromIssueDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, cancellationToken),
            "merge_request" => discussions.DeleteNoteFromMergeRequestDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, parsedNoteId, cancellationToken),
            "commit" => discussions.DeleteNoteFromCommitDiscussionAsync(projectOrGroup, id, discussionId, parsedNoteId,
                cancellationToken),
            "snippet" => discussions.DeleteNoteFromSnippetDiscussionAsync(projectOrGroup, ParseId(id, "id"),
                discussionId, parsedNoteId, cancellationToken),
            "epic" => discussions.DeleteNoteFromEpicDiscussionAsync(projectOrGroup, ParseId(id, "id"), discussionId,
                parsedNoteId, cancellationToken),
            _ => throw new McpException(
                "noteableType must be \"issue\", \"merge_request\", \"commit\", \"snippet\", or \"epic\".")
        };

        await task;
        return new DiscussionNoteDeleteResult(parsedNoteId, true);
    }

    [McpServerTool(Name = "gitlab_remove_reaction", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes one of the caller's own emoji reactions. GitLab rejects (403) removing anyone else's reaction.")]
    public async Task<AwardEmojiDeleteResult> RemoveReactionAsync(
        [Description(
            "What the reaction is on: \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", or \"epic_note\".")]
        string targetType,
        [Description(
            "For issue/issue_note/merge_request/merge_request_note/snippet/snippet_note: the project's numeric id or \"namespace/path\". For epic/epic_note: the group's numeric id or \"namespace/path\".")]
        string projectOrGroup,
        [Description(
            "The issue/merge-request iid, the snippet's numeric database id, or the epic's iid, as a numeric string.")]
        string id,
        [Description("The reaction's numeric id (from gitlab_list_reactions).")]
        string awardId,
        [Description("The note's numeric id. Required when targetType ends in \"_note\"; omit otherwise.")]
        string? noteId = null,
        CancellationToken cancellationToken = default)
    {
        var parsedId = ParseId(id, "id");
        var parsedAwardId = ParseId(awardId, "awardId");

        var task = targetType.ToLowerInvariant() switch
        {
            "issue" => awardEmoji.DeleteFromIssueAsync(projectOrGroup, parsedId, parsedAwardId, cancellationToken),
            "issue_note" => awardEmoji.DeleteFromIssueNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), parsedAwardId, cancellationToken),
            "merge_request" => awardEmoji.DeleteFromMergeRequestAsync(projectOrGroup, parsedId, parsedAwardId,
                cancellationToken),
            "merge_request_note" => awardEmoji.DeleteFromMergeRequestNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), parsedAwardId, cancellationToken),
            "snippet" => awardEmoji.DeleteFromSnippetAsync(projectOrGroup, parsedId, parsedAwardId, cancellationToken),
            "snippet_note" => awardEmoji.DeleteFromSnippetNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), parsedAwardId, cancellationToken),
            "epic" => awardEmoji.DeleteFromEpicAsync(projectOrGroup, parsedId, parsedAwardId, cancellationToken),
            "epic_note" => awardEmoji.DeleteFromEpicNoteAsync(projectOrGroup, parsedId,
                ParseId(RequireNoteId(noteId), "noteId"), parsedAwardId, cancellationToken),
            _ => throw new McpException(
                "targetType must be one of \"issue\", \"issue_note\", \"merge_request\", \"merge_request_note\", \"snippet\", \"snippet_note\", \"epic\", \"epic_note\".")
        };

        await task;
        return new AwardEmojiDeleteResult(parsedAwardId, true);
    }

    [McpServerTool(Name = "gitlab_get_draft_note", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Reads one of the caller's own pending, unpublished review comments on a merge request by id.")]
    public async Task<CallToolResult> GetDraftNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The draft note's numeric id (from gitlab_list_draft_notes).")]
        string draftNoteId,
        CancellationToken cancellationToken = default)
    {
        var draft = await draftNotes.GetAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"),
            ParseId(draftNoteId, "draftNoteId"), cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(draft),
            "projects/:id/merge_requests/:iid/draft_notes/:note_id");
    }

    [McpServerTool(Name = "gitlab_update_draft_note", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Rewrites the text of a pending draft note before it is published. The previous text is not recoverable once replaced.")]
    public async Task<CallToolResult> UpdateDraftNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The draft note's numeric id (from gitlab_list_draft_notes).")]
        string draftNoteId,
        [Description("The new draft body, in GitLab-flavored Markdown.")]
        string note,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateDraftNoteRequest { Note = note };
        var draft = await draftNotes.UpdateAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"),
            ParseId(draftNoteId, "draftNoteId"), request, cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToSummary(draft),
            "projects/:id/merge_requests/:iid/draft_notes/:note_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_draft_note", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Discards a pending draft note without ever publishing it. This cannot be undone.")]
    public async Task<DraftNoteDeleteResult> DeleteDraftNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The draft note's numeric id (from gitlab_list_draft_notes).")]
        string draftNoteId,
        CancellationToken cancellationToken = default)
    {
        var parsedDraftNoteId = ParseId(draftNoteId, "draftNoteId");
        await draftNotes.DeleteAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"), parsedDraftNoteId,
            cancellationToken);
        return new DraftNoteDeleteResult(parsedDraftNoteId, true);
    }

    [McpServerTool(Name = "gitlab_publish_draft_note", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Publishes a single pending draft note, turning it into a visible comment. This cannot be undone.")]
    public async Task<DraftNotePublishResult> PublishDraftNoteAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description("The draft note's numeric id (from gitlab_list_draft_notes).")]
        string draftNoteId,
        CancellationToken cancellationToken = default)
    {
        var parsedDraftNoteId = ParseId(draftNoteId, "draftNoteId");
        await draftNotes.PublishAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"), parsedDraftNoteId,
            cancellationToken);
        return new DraftNotePublishResult(parsedDraftNoteId, true);
    }

    [McpServerTool(Name = "gitlab_publish_all_draft_notes", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Submits the caller's whole pending review as one event: publishes every one of their draft notes on this merge request, optionally records a reviewer state and posts a summary comment. This cannot be undone.")]
    public async Task<DraftNotesPublishAllResult> PublishAllDraftNotesAsync(
        [Description("The project's numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI), as a string.")]
        string mergeRequestIid,
        [Description(
            "Reviewer state to record after publishing: \"reviewed\" or \"requested_changes\". Omit to record none. This does not itself grant a formal approval.")]
        string? reviewerState = null,
        [Description(
            "Optional summary comment to post alongside publishing, in GitLab-flavored Markdown. Omit to post nothing extra.")]
        string? summaryNote = null,
        [Description(
            "If true and summaryNote is supplied, the summary comment is posted as an internal note visible only to project or group members. Default false; ignored when summaryNote is omitted.")]
        bool internalNote = false,
        CancellationToken cancellationToken = default)
    {
        if (reviewerState is not (null or "reviewed" or "requested_changes"))
            throw new McpException("reviewerState must be \"reviewed\", \"requested_changes\", or omitted.");

        var request = new PublishDraftNotesRequest
        {
            ReviewerState = reviewerState,
            Note = summaryNote,
            Internal = summaryNote is null ? null : internalNote
        };

        await draftNotes.PublishAllAsync(project, ParseId(mergeRequestIid, "mergeRequestIid"), request,
            cancellationToken);
        return new DraftNotesPublishAllResult(true);
    }

    [McpServerTool(Name = "gitlab_apply_suggestions_batch", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Applies several code suggestions together as a single commit. The caller must already have every suggestion id -- they are not discoverable through the other tools on this server.")]
    public async Task<CallToolResult> ApplySuggestionsBatchAsync(
        [Description("Comma-separated numeric ids of the suggestions to apply together, e.g. \"101,102,103\".")]
        string suggestionIds,
        [Description("Optional custom commit message for the combined commit. Omit to use GitLab's default message.")]
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        var ids = ParseRequiredLongList(suggestionIds, "suggestionIds");
        var request = new ApplySuggestionBatchRequest { Ids = ids, CommitMessage = commitMessage };
        var suggestion = await suggestions.ApplyBatchAsync(request, cancellationToken);
        return GitLabContent.Wrap(DiscussionMapper.ToResult(suggestion), "suggestions/batch_apply");
    }

    private static IReadOnlyList<long> ParseRequiredLongList(string csv, string parameterName)
    {
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0) throw new McpException($"{parameterName} must contain at least one numeric id.");

        var result = new List<long>(parts.Length);

        foreach (var part in parts)
        {
            if (!long.TryParse(part, out var value))
                throw new McpException(
                    $"{parameterName} must be a comma-separated list of numeric ids; \"{part}\" is not a number.");

            result.Add(value);
        }

        return result;
    }
}