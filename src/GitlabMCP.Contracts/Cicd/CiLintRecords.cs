namespace GitlabMCP.Contracts.Cicd;

/// <summary>One <c>include:</c> entry resolved while linting a CI configuration.</summary>
public sealed record CiLintIncludeSummary(
    string? Type,
    string? Location,
    string? Blob,
    string? Raw,
    string? ContextProject,
    string? ContextSha);

/// <summary>
///     Result of <c>gitlab_validate_ci_config</c>. <c>GitLabCiLintResult.Jobs</c> (a raw
///     <c>IReadOnlyList&lt;JsonElement&gt;</c>) is deliberately not projected here — it is not needed to
///     answer "is this YAML valid", and a per-domain <see cref="System.Text.Json.Serialization.JsonSerializerContext" />
///     in Metadata mode has no generated metadata for an open-ended <see cref="System.Text.Json.JsonElement" />
///     tree, so leaving it out avoids that entirely rather than working around it.
///     <see cref="MergedYaml" /> is capped per call (see <c>CiLintMapper.MaxMergedYamlPreviewChars</c>) —
///     the fully expanded config for a deep include chain can be large, and this field is otherwise
///     unbounded; <see cref="MergedYamlTruncated" /> reports whether it was cut.
/// </summary>
public sealed record CiLintResultSummary(
    bool? Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string? MergedYaml,
    bool MergedYamlTruncated,
    IReadOnlyList<CiLintIncludeSummary> Includes);