using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class PagesMapper
{
    public static PagesDomainSummary ToSummary(GitLabPagesDomainSummary domain)
    {
        return new PagesDomainSummary(
            domain.Domain,
            domain.Url?.ToString(),
            domain.ProjectId,
            domain.Verified,
            domain.VerificationCode,
            domain.AutoSslEnabled,
            domain.EnabledUntil,
            domain.CertificateExpiration?.Expired,
            domain.CertificateExpiration?.Expiration);
    }

    public static PagesDomainDetail ToDetail(GitLabPagesDomain domain)
    {
        return new PagesDomainDetail(
            domain.Domain,
            domain.Url?.ToString(),
            domain.Verified,
            domain.VerificationCode,
            domain.AutoSslEnabled,
            domain.EnabledUntil,
            domain.Certificate?.Expired,
            domain.Certificate?.Subject);
    }

    public static PagesDeploymentSummary ToDeploymentSummary(GitLabPagesDeployment deployment)
    {
        return new PagesDeploymentSummary(
            deployment.CreatedAt,
            deployment.Url?.ToString(),
            deployment.PathPrefix,
            deployment.RootDirectory);
    }

    public static PagesSettingsSummary ToSettingsSummary(GitLabPagesSettings settings)
    {
        return new PagesSettingsSummary(
            settings.Url?.ToString(),
            settings.IsUniqueDomainEnabled,
            settings.ForceHttps,
            settings.PrimaryDomain,
            settings.Deployments?.Select(ToDeploymentSummary).ToList() ?? []);
    }
}