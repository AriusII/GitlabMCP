namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabLicenseTemplate</c> — metadata only (key/name/nickname/
///     popular flag/links), never <c>Content</c> (the full license text), which would blow up the token cost
///     of a list call; a caller who wants the full text has GitLab's own template page from
///     <see cref="HtmlUrl" />. Every string here is GitLab-served text, so the tool wraps via
///     <see cref="GitLabContent" /> — the flat string test applies even to catalog data like "MIT License".
/// </summary>
public sealed record LicenseTemplateSummary(
    string Key,
    string? Name,
    string? Nickname,
    bool? Popular,
    string? HtmlUrl,
    string? SourceUrl);

public sealed record LicenseTemplateListResult(IReadOnlyList<LicenseTemplateSummary> Templates, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabTemplateSummary</c> — one Dockerfile/<c>.gitignore</c>/CI-YAML
///     template's key and display name (metadata only, no body). Both fields are GitLab-shipped catalog text,
///     so every tool returning this wraps via <see cref="GitLabContent" /> — the flat string test applies even
///     to catalog data, same as <see cref="LicenseTemplateSummary" /> above.
/// </summary>
public sealed record TemplateCatalogEntry(string Key, string Name);

public sealed record TemplateCatalogListResult(IReadOnlyList<TemplateCatalogEntry> Templates, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabTemplate</c> — one Dockerfile/<c>.gitignore</c>/CI-YAML
///     template's full body. <see cref="Content" /> is GitLab-shipped template text (for CI YAML, executable
///     pipeline configuration), so the tool wraps via <see cref="GitLabContent" />. Some instance templates
///     (e.g. <c>Auto-DevOps.gitlab-ci.yml</c>) run to tens of KB, so <see cref="Content" /> is capped like
///     every other unbounded-text tool in this domain; <see cref="ContentTruncated" /> says whether it was cut.
/// </summary>
public sealed record TemplateDetail(string Name, string Content, bool ContentTruncated);

/// <summary>
///     Full projection of <c>GitLab.Client.Models.GitLabLicenseTemplate</c>, including <see cref="Content" />
///     (with the <c>[project]</c>/<c>[fullname]</c> placeholders filled in when the caller supplied them) —
///     unlike <see cref="LicenseTemplateSummary" /> above, which a list caller uses and which deliberately
///     omits the full text specifically to avoid its token cost. Every string here is GitLab-served text, so
///     the tool wraps via <see cref="GitLabContent" />. A full license text (e.g. GPL-3.0/AGPL-3.0) can run to
///     ~35,000 characters, so <see cref="Content" /> is capped like every other unbounded-text tool in this
///     domain; <see cref="ContentTruncated" /> says whether it was cut.
/// </summary>
public sealed record LicenseTemplateDetail(
    string Key,
    string? Name,
    string? Nickname,
    string? HtmlUrl,
    string? SourceUrl,
    bool? Popular,
    string? Description,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations,
    string? Content,
    bool ContentTruncated);