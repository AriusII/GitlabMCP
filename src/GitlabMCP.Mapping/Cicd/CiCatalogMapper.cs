using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>Projects a CI/CD Catalog publish result into the owned "cicd" record.</summary>
public static class CiCatalogMapper
{
    public static CiCatalogPublishResult ToSummary(GitLabCiCatalogPublishResult result)
    {
        return new CiCatalogPublishResult(
            result.CatalogUrl?.ToString());
    }
}