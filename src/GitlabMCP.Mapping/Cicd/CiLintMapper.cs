using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>
///     Projects a CI lint result into the owned "cicd" record. <see cref="GitLabCiLintResult.Jobs" /> is deliberately
///     dropped — see <see cref="CiLintResultSummary" />'s docstring.
/// </summary>
public static class CiLintMapper
{
    /// <summary>
    ///     Per-call cap on <see cref="CiLintResultSummary.MergedYaml" />. The fully expanded/merged
    ///     .gitlab-ci.yml can be large for a deep include chain; this is meant to be genuinely useful CI
    ///     debugging content, so the cap is generous rather than a tight preview.
    /// </summary>
    private const int MaxMergedYamlPreviewChars = 16_000;

    public static CiLintResultSummary ToSummary(GitLabCiLintResult result)
    {
        var (mergedYaml, truncated) = Truncate(result.MergedYaml);

        return new CiLintResultSummary(
            result.Valid,
            result.Errors ?? [],
            result.Warnings ?? [],
            mergedYaml,
            truncated,
            result.Includes?.Select(ToSummary).ToList() ?? []);
    }

    private static (string? MergedYaml, bool Truncated) Truncate(string? mergedYaml)
    {
        if (mergedYaml is null) return (null, false);

        return mergedYaml.Length > MaxMergedYamlPreviewChars
            ? (mergedYaml[..MaxMergedYamlPreviewChars], true)
            : (mergedYaml, false);
    }

    private static CiLintIncludeSummary ToSummary(GitLabCiLintInclude include)
    {
        return new CiLintIncludeSummary(
            include.Type,
            include.Location,
            include.Blob?.ToString(),
            include.Raw?.ToString(),
            include.ContextProject,
            include.ContextSha);
    }
}