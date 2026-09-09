using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Discussion;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every Discussion-domain payload record crossing the MCP boundary
///     (notes, discussions, draft notes, reactions, rendered markdown, events, suggestions).
///     ALL [JsonSerializable] attributes for this domain live in THIS ONE FILE — see
///     <see cref="GitlabMcpJsonContext" />'s own header comment for the verified generator bug this avoids
///     (CS8785 from splitting a JsonSerializerContext's [JsonSerializable] attributes across files). This is
///     a separate context/class from <see cref="GitlabMcpJsonContext" /> (shared by Epics/Ping) by design —
///     the host inserts this context's <c>Default</c> into <c>GitLabJson.Options</c>'s resolver chain
///     alongside the others.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(NoteSummary))]
[JsonSerializable(typeof(NoteListResult))]
[JsonSerializable(typeof(DiscussionSummary))]
[JsonSerializable(typeof(DiscussionListResult))]
[JsonSerializable(typeof(DraftNoteSummary))]
[JsonSerializable(typeof(DraftNoteListResult))]
[JsonSerializable(typeof(AwardEmojiSummary))]
[JsonSerializable(typeof(AwardEmojiListResult))]
[JsonSerializable(typeof(RenderedMarkdownResult))]
[JsonSerializable(typeof(EventSummary))]
[JsonSerializable(typeof(EventListResult))]
[JsonSerializable(typeof(SuggestionResult))]
[JsonSerializable(typeof(NoteDeleteResult))]
[JsonSerializable(typeof(DiscussionNoteDeleteResult))]
[JsonSerializable(typeof(AwardEmojiDeleteResult))]
[JsonSerializable(typeof(DraftNoteDeleteResult))]
[JsonSerializable(typeof(DraftNotePublishResult))]
[JsonSerializable(typeof(DraftNotesPublishAllResult))]
public sealed partial class DiscussionJsonContext : JsonSerializerContext;