namespace GitlabMCP.Contracts.Code;

/// <summary>Branch/tag/blame/file projection records (domain "code"). All GitLab-authored — wrap on every tool.</summary>
public sealed record BranchSummary(
    string? Name,
    string? CommitId,
    string? CommitTitle,
    bool? Merged,
    bool? Protected,
    bool? Default,
    string? WebUrl);

public sealed record BranchListResult(IReadOnlyList<BranchSummary> Branches, bool Truncated);

public sealed record TagSummary(
    string? Name,
    string? Message,
    string? Target,
    string? CommitId,
    bool? Protected);

public sealed record TagListResult(IReadOnlyList<TagSummary> Tags, bool Truncated);

/// <summary>
///     One run of contiguous lines sharing a commit, from <c>gitlab_get_file_blame</c>.
///     <see cref="Lines" /> is capped independently per range — omitting rangeStart/rangeEnd blames the
///     whole file, and a single contiguous run under one old commit can span the entire file.
/// </summary>
public sealed record BlameRangeSummary(
    string? CommitId,
    string? AuthorName,
    string? AuthorEmail,
    DateTimeOffset? AuthoredDate,
    string? CommitMessage,
    IReadOnlyList<string> Lines,
    bool LinesTruncated);

public sealed record BlameResult(IReadOnlyList<BlameRangeSummary> Ranges, bool Truncated);

/// <summary>
///     Result of creating a file (<c>gitlab_create_file</c>). Content is deliberately omitted — the caller
///     just sent it and does not need it echoed back.
/// </summary>
public sealed record RepositoryFileSummary(
    string? FileName,
    string? FilePath,
    string? Ref,
    long? Size,
    string? BlobId,
    string? CommitId,
    bool? ExecuteFilemode);

/// <summary>One entry from <c>gitlab_list_repository_tree</c> — a file or a subdirectory at a given ref.</summary>
public sealed record TreeItemSummary(
    string? Id,
    string? Name,
    string? Type,
    string? Path,
    string? Mode);

public sealed record TreeListResult(IReadOnlyList<TreeItemSummary> Items, bool Truncated);

/// <summary>
///     Result of <c>gitlab_get_file_content</c>. <see cref="Content" /> is decoded from Base64 when GitLab
///     returned it that way; left as GitLab returned it if decoding fails (e.g. genuinely binary content).
///     <see cref="Content" /> is then capped at a fixed character limit — see <see cref="ContentTruncated" /> —
///     since a file read has no caller-supplied size limit the way the list tools in this domain do.
/// </summary>
public sealed record FileContentResult(
    string? FileName,
    string? FilePath,
    string? Ref,
    long? Size,
    string? BlobId,
    string? CommitId,
    string? LastCommitId,
    bool? ExecuteFilemode,
    string? Content,
    bool ContentTruncated);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record FileDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record BranchDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record MergedBranchesDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record TagDeleteResult(bool Deleted);