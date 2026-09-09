namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     Result of <c>gitlab_publish_ci_catalog_version</c> (<c>GitLabCiCatalogPublishResult</c>). The catalog
///     URL is a GitLab-controlled link into the newly published version's page, so the tool wraps via
///     <see cref="GitLabContent" /> rather than returning this bare.
/// </summary>
public sealed record CiCatalogPublishResult(string? CatalogUrl);