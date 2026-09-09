using System.ComponentModel;

namespace GitlabMCP.Contracts.Code;

/// <summary>
///     One file operation inside a <c>gitlab_create_commit</c> call. This is caller-authored MCP input, not
///     GitLab-sourced data — it is never passed to <see cref="GitLabContent" />. Mirrors (a subset of)
///     <c>GitLab.Client.Models.CommitAction</c>; kept as an owned type so the MCP-facing parameter shape does
///     not change if the SDK's request DTO does.
/// </summary>
public sealed record CommitFileAction(
    [property: Description("One of: \"create\", \"update\", \"delete\", \"move\", \"chmod\".")]
    string Action,
    [property: Description("Full path from the repository root of the file this action applies to.")]
    string FilePath,
    [property: Description("Required only for action \"move\": the file's path before the move.")]
    string? PreviousPath = null,
    [property: Description("File content for \"create\"/\"update\". Plain text unless encoding is \"base64\".")]
    string? Content = null,
    [property: Description("Set to \"base64\" if content is Base64-encoded binary data. Omit for plain text.")]
    string? Encoding = null,
    [property:
        Description(
            "For action \"chmod\" (or to set on create): true marks the file executable (100755), false non-executable (100644).")]
    bool? ExecuteFilemode = null);