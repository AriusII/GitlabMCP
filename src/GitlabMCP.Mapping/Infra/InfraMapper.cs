using GitLab.Client.Models;
using GitlabMCP.Contracts.Infra;

namespace GitlabMCP.Mapping.Infra;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> DTOs for the "infra" domain into the owned records in
///     <see cref="GitlabMCP.Contracts.Infra" /> — never hands back the library's DTO.
/// </summary>
public static class InfraMapper
{
    public static DependencySummary ToSummary(GitLabDependency dependency)
    {
        return new DependencySummary(
            dependency.Name,
            dependency.Version,
            dependency.PackageManager,
            dependency.DependencyFilePath,
            dependency.Malware,
            (dependency.Vulnerabilities ?? []).Select(ToSummary).ToList(),
            (dependency.Licenses ?? []).Select(ToSummary).ToList());
    }

    /// <summary>Public: also used directly by <c>InfraTools.ListDependencyOccurrenceVulnerabilitiesAsync</c>.</summary>
    public static DependencyVulnerabilitySummary ToSummary(GitLabDependencyVulnerability vulnerability)
    {
        return new DependencyVulnerabilitySummary(
            vulnerability.Id,
            vulnerability.Name,
            vulnerability.Severity,
            vulnerability.Url?.ToString());
    }

    private static DependencyLicenseSummary ToSummary(GitLabDependencyLicense license)
    {
        return new DependencyLicenseSummary(
            license.SpdxIdentifier,
            license.Name,
            license.Url?.ToString());
    }

    public static ClusterAgentTokenSummary ToSummary(GitLabClusterAgentToken token)
    {
        return new ClusterAgentTokenSummary(
            token.Id,
            token.Name,
            token.Description,
            token.AgentId,
            token.Status,
            token.CreatedAt,
            token.CreatedByUserId,
            token.LastUsedAt);
    }

    /// <summary>
    ///     The one call whose result deliberately carries the plaintext secret — see
    ///     <see cref="ClusterAgentTokenCreated" />.
    /// </summary>
    public static ClusterAgentTokenCreated ToCreated(GitLabClusterAgentTokenWithSecret token)
    {
        return new ClusterAgentTokenCreated(
            token.Id,
            token.Name,
            token.Description,
            token.AgentId,
            token.Status,
            token.CreatedAt,
            token.CreatedByUserId,
            token.LastUsedAt,
            token.Token);
    }

    public static TerraformStateProtectionRuleSummary ToSummary(GitLabTerraformStateProtectionRule rule)
    {
        return new TerraformStateProtectionRuleSummary(
            rule.Id,
            rule.ProjectId,
            rule.StateName,
            rule.MinimumAccessLevelForWrite,
            rule.AllowedFrom);
    }

    public static ErrorTrackingClientKeySummary ToSummary(GitLabErrorTrackingClientKey key)
    {
        return new ErrorTrackingClientKeySummary(
            key.Id,
            key.Active,
            key.PublicKey,
            key.SentryDsn?.ToString());
    }

    public static ClusterAgentSummary ToSummary(GitLabClusterAgent agent)
    {
        return new ClusterAgentSummary(
            agent.Id,
            agent.Name,
            agent.ConfigProject is { } project ? ToSummary(project) : null,
            agent.CreatedAt,
            agent.CreatedByUserId,
            agent.IsReceptive);
    }

    private static ClusterAgentConfigProjectSummary ToSummary(GitLabProjectIdentity project)
    {
        return new ClusterAgentConfigProjectSummary(
            project.Id,
            project.Name,
            project.PathWithNamespace);
    }

    public static ClusterAgentUrlConfigurationSummary ToSummary(GitLabClusterAgentUrlConfiguration configuration)
    {
        return new ClusterAgentUrlConfigurationSummary(
            configuration.Id,
            configuration.AgentId,
            configuration.Url?.ToString(),
            configuration.PublicKey,
            configuration.ClientCert,
            configuration.CaCert,
            configuration.TlsHost);
    }

    public static DependencyExportSummary ToSummary(GitLabDependencyListExport export)
    {
        return new DependencyExportSummary(
            export.Id,
            export.HasFinished,
            export.Self?.ToString(),
            export.Download?.ToString());
    }

    // --- Backlog part 2/2: dependency attestations, SBOM scans, error tracking settings, alert metric images ---

    public static AttestationSummary ToSummary(GitLabAttestation attestation)
    {
        return new AttestationSummary(
            attestation.Id,
            attestation.Iid,
            attestation.CreatedAt,
            attestation.UpdatedAt,
            attestation.ExpireAt,
            attestation.ProjectId,
            attestation.BuildId,
            attestation.Status,
            attestation.PredicateKind,
            attestation.PredicateType,
            attestation.SubjectDigest,
            attestation.DownloadUrl?.ToString());
    }

    public static SbomScanResult ToSummary(GitLabSbomScan scan)
    {
        return new SbomScanResult(
            scan.Id,
            scan.DownloadUrl?.ToString(),
            scan.Throttled,
            scan.ProjectThrottlingResetsIn,
            scan.AdvisoryDbState);
    }

    public static ErrorTrackingSettingsSummary ToSummary(GitLabErrorTrackingSettings settings)
    {
        return new ErrorTrackingSettingsSummary(
            settings.Active,
            settings.ProjectName,
            settings.SentryExternalUrl?.ToString(),
            settings.ApiUrl?.ToString(),
            settings.Integrated);
    }

    public static AlertMetricImageSummary ToSummary(GitLabMetricImage image)
    {
        return new AlertMetricImageSummary(
            image.Id,
            image.CreatedAt,
            image.Filename,
            image.FilePath,
            image.Url?.ToString(),
            image.Caption);
    }
}