using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabLicenseTemplate" /> into the owned <see cref="LicenseTemplateSummary" /> — omits
///     <c>Content</c>/<c>Conditions</c>/<c>Permissions</c>/<c>Limitations</c>, which a list caller does not need.
/// </summary>
public static class TemplateMapper
{
    /// <summary>
    ///     Cap on <see cref="TemplateDetail.Content" />/<see cref="LicenseTemplateDetail.Content" />, matching
    ///     <c>SearchTools.MaxRawTextLength</c> — the same 20,000-character convention every other unbounded-text
    ///     tool in the "search" domain uses. An instance CI-YAML template (e.g. <c>Auto-DevOps.gitlab-ci.yml</c>)
    ///     or an open-source license body (e.g. GPL-3.0/AGPL-3.0, ~35,000 characters) can otherwise dwarf every
    ///     other payload this domain returns.
    /// </summary>
    private const int MaxTemplateContentChars = 20_000;

    public static LicenseTemplateSummary ToSummary(GitLabLicenseTemplate template)
    {
        return new LicenseTemplateSummary(
            template.Key,
            template.Name,
            template.Nickname,
            template.Popular,
            template.HtmlUrl?.ToString(),
            template.SourceUrl?.ToString());
    }

    public static TemplateCatalogEntry ToCatalogEntry(GitLabTemplateSummary template)
    {
        return new TemplateCatalogEntry(
            template.Key,
            template.Name);
    }

    public static TemplateDetail ToDetail(GitLabTemplate template)
    {
        var (content, truncated) = Truncate(template.Content);
        return new TemplateDetail(
            template.Name,
            content,
            truncated);
    }

    public static LicenseTemplateDetail ToLicenseDetail(GitLabLicenseTemplate template)
    {
        var (content, truncated) = TruncateNullable(template.Content);
        return new LicenseTemplateDetail(
            template.Key,
            template.Name,
            template.Nickname,
            template.HtmlUrl?.ToString(),
            template.SourceUrl?.ToString(),
            template.Popular,
            template.Description,
            template.Conditions ?? [],
            template.Permissions ?? [],
            template.Limitations ?? [],
            content,
            truncated);
    }

    private static (string Content, bool Truncated) Truncate(string content)
    {
        return content.Length > MaxTemplateContentChars
            ? (content[..MaxTemplateContentChars], true)
            : (content, false);
    }

    private static (string? Content, bool Truncated) TruncateNullable(string? content)
    {
        if (content is null) return (null, false);

        return content.Length > MaxTemplateContentChars
            ? (content[..MaxTemplateContentChars], true)
            : (content, false);
    }
}