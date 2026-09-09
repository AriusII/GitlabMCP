using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Domain;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Infra;
using GitlabMCP.Mapping.Infra;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Terraform state protection, cluster-agent tokens, dependency scanning, error tracking and
///     attestations (domain "infra", DevOps profile). Every tool here returns GitLab-authored text (state
///     names, agent/token names, package identifiers, license names, …) so every tool declares
///     <c>Task&lt;CallToolResult&gt;</c> and wraps via <see cref="GitLabContent" />.
/// </summary>
[McpServerToolType]
public sealed class InfraTools(
    IDependenciesClient dependencies,
    IClusterAgentsClient clusterAgents,
    ITerraformStatesClient terraformStates,
    IErrorTrackingClient errorTracking,
    IAttestationsClient attestations,
    IAlertManagementClient alertManagement)
{
    private const int MaxLimit = 100;

    /// <summary>
    ///     Matches the other file-download caps in this class (Terraform state); a dependency export is a flat
    ///     list/CSV/SBOM document, not a binary artifact, so there is no reason for a larger allowance.
    /// </summary>
    private const int MaxDependencyExportBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     GitLab defines no response schema for the SAST file-scan endpoint (it is marked experimental), so the result
    ///     is raw JSON text truncated by character count, mirroring <c>SearchTools.MaxRawTextLength</c>.
    /// </summary>
    private const int MaxScanResponseChars = 20_000;

    /// <summary>An SBOM document is JSON, same reasoning and cap as <see cref="MaxTerraformStateBytes" />.</summary>
    private const int MaxSbomScanBytes = 8 * 1024 * 1024;

    /// <summary>A metric screenshot/graph is a small image; capped well under a context window once base64-encoded.</summary>
    private const int MaxMetricImageBytes = 4 * 1024 * 1024;

    /// <summary>
    ///     Generous for a sigstore-style provenance bundle, small enough to stay well clear of a context
    ///     window once base64-encoded (~+33%). A bundle over this is refused rather than truncated: a
    ///     partial signature/provenance bundle verifies as neither validly signed nor cleanly invalid, so a
    ///     silent cut here would be actively misleading rather than merely incomplete.
    /// </summary>
    private const int MaxAttestationBytes = 4 * 1024 * 1024;

    /// <summary>
    ///     Generous for a real infrastructure's tfstate document, capped well clear of a context window once
    ///     base64-encoded (~+33%). Refused rather than truncated in both directions (read and write): a
    ///     partial state document is not merely incomplete, it is actively dangerous to apply.
    /// </summary>
    private const int MaxTerraformStateBytes = 8 * 1024 * 1024;

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_dependencies", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the dependencies (name, version, package manager, and — when the caller may read vulnerabilities — known vulnerabilities and licenses) that a project's last dependency-scanning CI job found. GitLab Ultimate feature; expect 403/404 on lower plans.")]
    public async Task<CallToolResult> ListDependenciesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Optional exact package-manager filter, e.g. \"npm\", \"bundler\", \"maven\". Omit for every package manager.")]
        string? packageManager = null,
        [Description("Maximum dependencies to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new DependencyListOptions
        {
            PackageManager = packageManager is null ? null : [packageManager],
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<DependencySummary> collected = [];
        var truncated = false;

        await foreach (var dependency in dependencies.ListAsync((ProjectId)project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(dependency));
        }

        return GitLabContent.Wrap(new DependencyListResult(collected, truncated), "projects/:id/dependencies");
    }

    [McpServerTool(Name = "gitlab_list_cluster_agent_tokens", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a GitLab agent for Kubernetes' active authentication tokens (metadata only — name, status, timestamps — never the secret) to audit what is currently authorized to connect.")]
    public async Task<CallToolResult> ListClusterAgentTokensAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The cluster agent's numeric id (from gitlab_list_cluster_agent_tokens' caller or the GitLab UI).")]
        long agentId,
        [Description(
            "Maximum tokens to return (1-100). Default 20. An agent can hold at most 2 active tokens, so truncation here is unlikely.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ClusterAgentTokenSummary> collected = [];
        var truncated = false;

        await foreach (var token in clusterAgents.ListTokensAsync((ProjectId)project, agentId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(token));
        }

        return GitLabContent.Wrap(new ClusterAgentTokenListResult(collected, truncated),
            "projects/:id/cluster_agents/:agent_id/tokens");
    }

    [McpServerTool(Name = "gitlab_list_terraform_state_protection_rules", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the write-access protection rules configured for a project's Terraform states — which minimum role, and whether CI-only, may write each named state.")]
    public async Task<CallToolResult> ListTerraformStateProtectionRulesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum rules to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<TerraformStateProtectionRuleSummary> collected = [];
        var truncated = false;

        await foreach (var rule in terraformStates.ListProtectionRulesAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(rule));
        }

        return GitLabContent.Wrap(new TerraformStateProtectionRuleListResult(collected, truncated),
            "projects/:id/terraform_state/protection_rules");
    }

    [McpServerTool(Name = "gitlab_list_error_tracking_client_keys", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the client keys issued for a project's integrated error tracking (id, active flag, public key, DSN) to audit what is currently allowed to report errors into it. These are report-only credentials, not read/write API access.")]
    public async Task<CallToolResult> ListErrorTrackingClientKeysAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum keys to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ErrorTrackingClientKeySummary> collected = [];
        var truncated = false;

        await foreach (var key in errorTracking.ListClientKeysAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(key));
        }

        return GitLabContent.Wrap(new ErrorTrackingClientKeyListResult(collected, truncated),
            "projects/:id/error_tracking/client_keys");
    }

    [McpServerTool(Name = "gitlab_download_attestation", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads one attestation's raw provenance bundle (base64-encoded) by its project-scoped id, for offline signature/provenance verification. Refuses bundles over 4 MB rather than returning a truncated, unverifiable partial bundle.")]
    public async Task<CallToolResult> DownloadAttestationAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The attestation's project-scoped internal id (iid), not a global id.")]
        long attestationIid,
        CancellationToken cancellationToken = default)
    {
        await using var file = await attestations.DownloadAsync((ProjectId)project, attestationIid, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxAttestationBytes)
            throw new McpException(
                $"Attestation bundle is {declaredLength} bytes, over the {MaxAttestationBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var buffer = new byte[MaxAttestationBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxAttestationBytes)
            throw new McpException(
                $"Attestation bundle exceeds the {MaxAttestationBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var payload = new AttestationBundle(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/attestations/:attestation_iid");
    }

    [McpServerTool(Name = "gitlab_get_terraform_state", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads a named Terraform state's current document, or — when serial is given — one specific historical version. Returns the raw document base64-encoded. Refuses documents over 8 MB rather than returning a truncated, unusable partial state.")]
    public async Task<CallToolResult> GetTerraformStateAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The Terraform state's name (as used with terraform init -backend-config, or shown in GitLab's Terraform states list).")]
        string stateName,
        [Description("Optional historical version's serial number. Omit to read the current state.")]
        long? serial = null,
        [Description(
            "Optional lock id this read is made under, when the caller already holds the lock. Ignored when serial is given.")]
        string? lockId = null,
        CancellationToken cancellationToken = default)
    {
        await using var file = serial is { } version
            ? await terraformStates.DownloadVersionAsync((ProjectId)project, stateName, version, cancellationToken)
            : await terraformStates.DownloadAsync((ProjectId)project, stateName, lockId, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxTerraformStateBytes)
            throw new McpException(
                $"Terraform state document is {declaredLength} bytes, over the {MaxTerraformStateBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var buffer = new byte[MaxTerraformStateBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxTerraformStateBytes)
            throw new McpException(
                $"Terraform state document exceeds the {MaxTerraformStateBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var payload = new TerraformStateDocument(
            stateName,
            serial,
            file.ContentType,
            file.ContentLength,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/terraform_state/:name");
    }

    [McpServerTool(Name = "gitlab_list_cluster_agents", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the GitLab agents for Kubernetes registered on a project.")]
    public async Task<CallToolResult> ListClusterAgentsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum agents to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ClusterAgentSummary> collected = [];
        var truncated = false;

        await foreach (var agent in clusterAgents.ListAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(agent));
        }

        return GitLabContent.Wrap(new ClusterAgentListResult(collected, truncated), "projects/:id/cluster_agents");
    }

    [McpServerTool(Name = "gitlab_get_cluster_agent", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one GitLab agent for Kubernetes' registration details, including which config project defines it.")]
    public async Task<CallToolResult> GetClusterAgentAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id (from gitlab_list_cluster_agents).")]
        long agentId,
        CancellationToken cancellationToken = default)
    {
        var agent = await clusterAgents.GetAsync((ProjectId)project, agentId, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(agent), "projects/:id/cluster_agents/:agent_id");
    }

    [McpServerTool(Name = "gitlab_get_cluster_agent_url_configuration", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads the receptive-mode URL configuration (address and TLS material) GitLab uses to connect out to a cluster agent, if one is set (GitLab 17.4+). Most agents are push-mode and connect in instead, so a null Configuration is the common, healthy answer.")]
    public async Task<CallToolResult> GetClusterAgentUrlConfigurationAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id (from gitlab_list_cluster_agents).")]
        long agentId,
        CancellationToken cancellationToken = default)
    {
        await foreach (var configuration in clusterAgents.ListUrlConfigurationsAsync((ProjectId)project, agentId,
                           cancellationToken))
            return GitLabContent.Wrap(
                new ClusterAgentUrlConfigurationResult(InfraMapper.ToSummary(configuration)),
                "projects/:id/cluster_agents/:agent_id/url_configurations");

        return GitLabContent.Wrap(
            new ClusterAgentUrlConfigurationResult(null),
            "projects/:id/cluster_agents/:agent_id/url_configurations");
    }

    [McpServerTool(Name = "gitlab_get_dependency_export_status", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Checks whether a dependency-list or SBOM export requested via gitlab_export_dependencies has finished generating.")]
    public async Task<CallToolResult> GetDependencyExportStatusAsync(
        [Description("The export's numeric id, returned by gitlab_export_dependencies.")]
        long exportId,
        CancellationToken cancellationToken = default)
    {
        var export = await dependencies.GetExportAsync(exportId, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(export), "dependency_list_exports/:export_id");
    }

    [McpServerTool(Name = "gitlab_download_dependency_export", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads a finished dependency-list or SBOM export's raw file (base64-encoded), started earlier by gitlab_export_dependencies — check gitlab_get_dependency_export_status first. Refuses files over 8 MB rather than returning a truncated, unusable partial export.")]
    public async Task<CallToolResult> DownloadDependencyExportAsync(
        [Description("The export's numeric id, returned by gitlab_export_dependencies.")]
        long exportId,
        CancellationToken cancellationToken = default)
    {
        await using var file = await dependencies.DownloadExportAsync(exportId, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxDependencyExportBytes)
            throw new McpException(
                $"Dependency export is {declaredLength} bytes, over the {MaxDependencyExportBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var buffer = new byte[MaxDependencyExportBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxDependencyExportBytes)
            throw new McpException(
                $"Dependency export exceeds the {MaxDependencyExportBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var payload = new DependencyExportFile(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "dependency_list_exports/:export_id/download");
    }

    [McpServerTool(Name = "gitlab_list_dependency_occurrence_vulnerabilities", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the vulnerabilities associated with one specific dependency occurrence, for drilling down from an entry returned by gitlab_list_dependencies.")]
    public async Task<CallToolResult> ListDependencyOccurrenceVulnerabilitiesAsync(
        [Description(
            "The dependency occurrence's id (from the dependency entry this drills down from — not the dependency's name or version).")]
        string occurrenceId,
        [Description("Maximum vulnerabilities to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<DependencyVulnerabilitySummary> collected = [];
        var truncated = false;

        await foreach (var vulnerability in dependencies.ListOccurrenceVulnerabilitiesAsync(occurrenceId,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(vulnerability));
        }

        return GitLabContent.Wrap(new DependencyVulnerabilityListResult(collected, truncated),
            "dependencies/:occurrence_id/vulnerabilities");
    }

    [McpServerTool(Name = "gitlab_list_dependency_attestations", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the provenance attestations recorded for one build artifact's digest. GitLab feature-flags this endpoint and calls it not yet ready for production use.")]
    public async Task<CallToolResult> ListDependencyAttestationsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The artifact's subject digest to look up attestations for, e.g. \"sha256:...\".")]
        string subjectDigest,
        [Description("Maximum attestations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<AttestationSummary> collected = [];
        var truncated = false;

        await foreach (var attestation in dependencies.ListAttestationsAsync((ProjectId)project, subjectDigest,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(attestation));
        }

        return GitLabContent.Wrap(new AttestationListResult(collected, truncated), "projects/:id/attestations");
    }

    [McpServerTool(Name = "gitlab_scan_file_for_vulnerabilities", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Runs a real-time SAST scan on one file's content without a full pipeline, for a quick pre-commit check. GitLab marks this endpoint experimental and defines no fixed response schema, so the result comes back as raw JSON text rather than a typed shape.")]
    public async Task<CallToolResult> ScanFileForVulnerabilitiesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Which SAST scanner endpoint to target, as configured on the instance (e.g. \"semgrep\"). Check the project's SAST configuration if unsure.")]
        string sastEndpoint,
        [Description("The file's path, used to select the right scan rules for its type, e.g. \"src/app.py\".")]
        string filePath,
        [Description("The file's full source content to scan.")]
        string content,
        CancellationToken cancellationToken = default)
    {
        var request = new SastFileScanRequest { FilePath = filePath, Content = content };
        var result = await dependencies.ScanFileAsync((ProjectId)project, sastEndpoint, request, cancellationToken);

        var raw = result.GetRawText();
        var text = raw.Length > MaxScanResponseChars
            ? raw[..MaxScanResponseChars] + "\n... (truncated)"
            : raw;

        return GitLabContent.WrapText(text, "projects/:id/security_scans/sast/:sast_endpoint");
    }

    [McpServerTool(Name = "gitlab_get_sbom_scan", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Looks up a CI job's SBOM scan by the digest of the uploaded document. GitLab answers 202 with no body while the scan is still running, which may surface here as mostly-empty fields — retry after a short delay if so.")]
    public async Task<CallToolResult> GetSbomScanAsync(
        [Description("The CI job's numeric id the SBOM was uploaded under.")]
        long jobId,
        [Description("The uploaded document's digest, as given to gitlab_upload_sbom_scan or gitlab_reuse_sbom_scan.")]
        string sbomDigest,
        CancellationToken cancellationToken = default)
    {
        var scan = await dependencies.GetSbomScanAsync(jobId, sbomDigest, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(scan), "jobs/:job_id/sbom_scans/:sbom_digest");
    }

    [McpServerTool(Name = "gitlab_get_error_tracking_settings", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Checks whether error tracking is enabled for a project, and whether it is integrated (GitLab-hosted) or pointed at an external Sentry instance.")]
    public async Task<CallToolResult> GetErrorTrackingSettingsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var settings = await errorTracking.GetSettingsAsync((ProjectId)project, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(settings), "projects/:id/error_tracking/settings");
    }

    [McpServerTool(Name = "gitlab_list_alert_metric_images", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the metric screenshots/graphs attached to an alert, to review the evidence behind an incident.")]
    public async Task<CallToolResult> ListAlertMetricImagesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The alert's project-scoped internal id (iid).")]
        long alertIid,
        [Description("Maximum images to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<AlertMetricImageSummary> collected = [];
        var truncated = false;

        await foreach (var image in alertManagement.ListMetricImagesAsync((ProjectId)project, alertIid,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(InfraMapper.ToSummary(image));
        }

        return GitLabContent.Wrap(new AlertMetricImageListResult(collected, truncated),
            "projects/:id/alert_management_alerts/:alert_iid/metric_images");
    }

    // ---------------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_terraform_state_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Restricts who may write a named Terraform state, by requiring a minimum role and, optionally, that the write come from CI (optionally CI running on a protected branch).")]
    public async Task<CallToolResult> CreateTerraformStateProtectionRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The Terraform state's name (as used with terraform init/-backend-config, or shown in GitLab's Terraform states list).")]
        string stateName,
        [Description(
            "Minimum role allowed to write this state: \"developer\", \"maintainer\", \"owner\", or \"admin\".")]
        string minimumAccessLevelForWrite,
        [Description(
            "Restricts writes further: \"anywhere\" (any session with the required role), \"ci_only\" (CI/CD jobs only), or \"ci_on_protected_branch_only\" (CI/CD jobs running on a protected branch only). Omit for GitLab's default.")]
        string? allowedFrom = null,
        CancellationToken cancellationToken = default)
    {
        var accessLevel = minimumAccessLevelForWrite.ToLowerInvariant() switch
        {
            "developer" => GitLabTerraformStateWriteAccessLevel.Developer,
            "maintainer" => GitLabTerraformStateWriteAccessLevel.Maintainer,
            "owner" => GitLabTerraformStateWriteAccessLevel.Owner,
            "admin" => GitLabTerraformStateWriteAccessLevel.Admin,
            _ => throw new McpException(
                "minimumAccessLevelForWrite must be one of: developer, maintainer, owner, admin.")
        };

        GitLabTerraformStateAllowedFrom? allowedFromValue = allowedFrom?.ToLowerInvariant() switch
        {
            "anywhere" => GitLabTerraformStateAllowedFrom.Anywhere,
            "ci_only" => GitLabTerraformStateAllowedFrom.CiOnly,
            "ci_on_protected_branch_only" => GitLabTerraformStateAllowedFrom.CiOnProtectedBranchOnly,
            null or "" => null,
            _ => throw new McpException(
                "allowedFrom must be one of: anywhere, ci_only, ci_on_protected_branch_only, or omitted.")
        };

        var request = new CreateTerraformStateProtectionRuleRequest
        {
            StateName = stateName,
            MinimumAccessLevelForWrite = accessLevel,
            AllowedFrom = allowedFromValue
        };

        var rule = await terraformStates.CreateProtectionRuleAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(rule),
            "projects/:id/terraform_state/protection_rules (create)");
    }

    [McpServerTool(Name = "gitlab_create_cluster_agent_token", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Issues a new authentication token for a GitLab agent for Kubernetes (an agent can hold at most 2 active tokens at once). The plaintext secret is returned exactly once, in this response — GitLab cannot show it again, so persist it immediately for deploying to the agent.")]
    public async Task<CallToolResult> CreateClusterAgentTokenAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id to issue a token for.")]
        long agentId,
        [Description("A human-readable name for the token.")]
        string name,
        [Description("Optional description for the token.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateClusterAgentTokenRequest { Name = name, Description = description };
        var token = await clusterAgents.CreateTokenAsync((ProjectId)project, agentId, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToCreated(token),
            "projects/:id/cluster_agents/:agent_id/tokens (create)");
    }

    [McpServerTool(Name = "gitlab_create_error_tracking_client_key", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Issues a new client key (with an auto-generated public key and DSN) that an application can use to report errors into a project's integrated error tracking.")]
    public async Task<CallToolResult> CreateErrorTrackingClientKeyAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var key = await errorTracking.CreateClientKeyAsync((ProjectId)project, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(key), "projects/:id/error_tracking/client_keys (create)");
    }

    [McpServerTool(Name = "gitlab_upload_terraform_state", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Publishes a new Terraform state document as a project's current state for the given name — creates it if it does not exist, or appends a new version otherwise. This is the write half of Terraform's HTTP backend. Provide the document's raw bytes base64-encoded; documents whose decoded size exceeds 8 MB are refused rather than silently truncated.")]
    public async Task<TerraformStateUploadResult> UploadTerraformStateAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The Terraform state's name (as used with terraform init -backend-config, or shown in GitLab's Terraform states list).")]
        string stateName,
        [Description("The state document's raw bytes (the Terraform state JSON), base64-encoded.")]
        string contentBase64,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64.");
        }

        if (bytes.Length > MaxTerraformStateBytes)
            throw new McpException(
                $"Decoded state document is {bytes.Length} bytes, over the {MaxTerraformStateBytes}-byte limit this tool can upload. Upload it directly to GitLab instead.");

        await using var stream = new MemoryStream(bytes);
        var upload = new GitLabFileUpload
        {
            Content = stream,
            FileName = stateName,
            ContentType = "application/json",
            FieldName = "file"
        };

        await terraformStates.UploadAsync((ProjectId)project, stateName, upload, cancellationToken);
        return new TerraformStateUploadResult(stateName, true);
    }

    [McpServerTool(Name = "gitlab_delete_terraform_state", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes a named Terraform state and every historical version of it. This cannot be undone.")]
    public async Task<TerraformStateDeleteResult> DeleteTerraformStateAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The Terraform state's name to delete.")]
        string stateName,
        CancellationToken cancellationToken = default)
    {
        await terraformStates.DeleteAsync((ProjectId)project, stateName, cancellationToken);
        return new TerraformStateDeleteResult(stateName, true);
    }

    [McpServerTool(Name = "gitlab_delete_terraform_state_version", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Deletes one historical version (by serial number) of a Terraform state, leaving the current state and its other versions in place.")]
    public async Task<TerraformStateVersionDeleteResult> DeleteTerraformStateVersionAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The Terraform state's name.")]
        string stateName,
        [Description("The historical version's serial number to delete (from the state's version history).")]
        long serial,
        CancellationToken cancellationToken = default)
    {
        await terraformStates.DeleteVersionAsync((ProjectId)project, stateName, serial, cancellationToken);
        return new TerraformStateVersionDeleteResult(stateName, serial, true);
    }

    [McpServerTool(Name = "gitlab_lock_terraform_state", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Places a lock on a Terraform state so no other run may write it until it is unlocked, mirroring Terraform's own LOCK protocol. Fails with a conflict if another run already holds the lock.")]
    public async Task<TerraformStateLockResult> LockTerraformStateAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The Terraform state's name.")]
        string stateName,
        [Description(
            "A unique identifier for this lock (e.g. a UUID) — the caller must supply this same value to gitlab_unlock_terraform_state to release it normally.")]
        string lockId,
        [Description(
            "Optional: the Terraform operation holding the lock, e.g. \"OperationTypeApply\". Omit if unknown.")]
        string? operation = null,
        [Description("Optional free-text note about why the lock was taken.")]
        string? info = null,
        [Description("Optional identity of who/what holds the lock, e.g. \"alice@host\". Omit if unknown.")]
        string? who = null,
        [Description("Optional Terraform version string of the run holding the lock.")]
        string? version = null,
        [Description("Optional ISO-8601 timestamp the lock was created. Omit to leave unset.")]
        string? created = null,
        CancellationToken cancellationToken = default)
    {
        var request = new LockTerraformStateRequest
        {
            Id = lockId,
            Operation = operation ?? string.Empty,
            Info = info ?? string.Empty,
            Who = who ?? string.Empty,
            Version = version ?? string.Empty,
            Created = created ?? string.Empty,
            Path = stateName
        };

        await terraformStates.LockAsync((ProjectId)project, stateName, request, cancellationToken);
        return new TerraformStateLockResult(stateName, lockId, true);
    }

    [McpServerTool(Name = "gitlab_unlock_terraform_state", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Releases a Terraform state lock. Omit lockId to force the lock open, which is what \"terraform force-unlock\" does — use with care, since it can release a lock still legitimately held by another run.")]
    public async Task<TerraformStateUnlockResult> UnlockTerraformStateAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The Terraform state's name.")]
        string stateName,
        [Description("The lock id returned when the lock was taken. Omit to force-unlock.")]
        string? lockId = null,
        CancellationToken cancellationToken = default)
    {
        await terraformStates.UnlockAsync((ProjectId)project, stateName, lockId, cancellationToken);
        return new TerraformStateUnlockResult(stateName, lockId is null, true);
    }

    [McpServerTool(Name = "gitlab_update_terraform_state_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes an existing Terraform state protection rule's minimum write access level and/or allowed-from scope.")]
    public async Task<CallToolResult> UpdateTerraformStateProtectionRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The protection rule's numeric id (from gitlab_list_terraform_state_protection_rules).")]
        long ruleId,
        [Description(
            "New minimum role allowed to write the protected state: \"developer\", \"maintainer\", \"owner\", or \"admin\". Omit to leave unchanged.")]
        string? minimumAccessLevelForWrite = null,
        [Description(
            "New write-from restriction: \"anywhere\", \"ci_only\", or \"ci_on_protected_branch_only\". Omit to leave unchanged.")]
        string? allowedFrom = null,
        CancellationToken cancellationToken = default)
    {
        if (minimumAccessLevelForWrite is null && allowedFrom is null)
            throw new McpException("Provide at least one of minimumAccessLevelForWrite or allowedFrom to change.");

        GitLabTerraformStateWriteAccessLevel? accessLevel = minimumAccessLevelForWrite?.ToLowerInvariant() switch
        {
            null => null,
            "developer" => GitLabTerraformStateWriteAccessLevel.Developer,
            "maintainer" => GitLabTerraformStateWriteAccessLevel.Maintainer,
            "owner" => GitLabTerraformStateWriteAccessLevel.Owner,
            "admin" => GitLabTerraformStateWriteAccessLevel.Admin,
            _ => throw new McpException(
                "minimumAccessLevelForWrite must be one of: developer, maintainer, owner, admin.")
        };

        GitLabTerraformStateAllowedFrom? allowedFromValue = allowedFrom?.ToLowerInvariant() switch
        {
            null => null,
            "anywhere" => GitLabTerraformStateAllowedFrom.Anywhere,
            "ci_only" => GitLabTerraformStateAllowedFrom.CiOnly,
            "ci_on_protected_branch_only" => GitLabTerraformStateAllowedFrom.CiOnProtectedBranchOnly,
            _ => throw new McpException("allowedFrom must be one of: anywhere, ci_only, ci_on_protected_branch_only.")
        };

        var request = new UpdateTerraformStateProtectionRuleRequest
        {
            MinimumAccessLevelForWrite = accessLevel,
            AllowedFrom = allowedFromValue
        };

        var rule = await terraformStates.UpdateProtectionRuleAsync((ProjectId)project, ruleId, request,
            cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(rule),
            "projects/:id/terraform_state/protection_rules/:rule_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_terraform_state_protection_rule", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Removes a Terraform state protection rule, reverting that state to the project's default write access.")]
    public async Task<TerraformStateProtectionRuleDeleteResult> DeleteTerraformStateProtectionRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The protection rule's numeric id (from gitlab_list_terraform_state_protection_rules).")]
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        await terraformStates.DeleteProtectionRuleAsync((ProjectId)project, ruleId, cancellationToken);
        return new TerraformStateProtectionRuleDeleteResult(ruleId, true);
    }

    [McpServerTool(Name = "gitlab_register_cluster_agent", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Registers a new GitLab agent for Kubernetes on a project by name. The name must match a directory under .gitlab/agents in the project's default branch containing the agent's config, or the agent process will not be able to authenticate.")]
    public async Task<CallToolResult> RegisterClusterAgentAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The agent's name — must match an existing .gitlab/agents/<name> config directory.")]
        string name,
        CancellationToken cancellationToken = default)
    {
        var agent = await clusterAgents.CreateAsync((ProjectId)project, new CreateClusterAgentRequest { Name = name },
            cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(agent), "projects/:id/cluster_agents (create)");
    }

    [McpServerTool(Name = "gitlab_delete_cluster_agent", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deregisters a cluster agent, disconnecting any Kubernetes agent process currently running with its identity.")]
    public async Task<ClusterAgentDeleteResult> DeleteClusterAgentAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id to deregister.")]
        long agentId,
        CancellationToken cancellationToken = default)
    {
        await clusterAgents.DeleteAsync((ProjectId)project, agentId, cancellationToken);
        return new ClusterAgentDeleteResult(agentId, true);
    }

    [McpServerTool(Name = "gitlab_revoke_cluster_agent_token", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Revokes a cluster agent token immediately, disconnecting any running agent process still using it.")]
    public async Task<ClusterAgentTokenRevokeResult> RevokeClusterAgentTokenAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id.")]
        long agentId,
        [Description("The token's numeric id to revoke (from gitlab_list_cluster_agent_tokens).")]
        long tokenId,
        CancellationToken cancellationToken = default)
    {
        await clusterAgents.RevokeTokenAsync((ProjectId)project, agentId, tokenId, cancellationToken);
        return new ClusterAgentTokenRevokeResult(agentId, tokenId, true);
    }

    [McpServerTool(Name = "gitlab_create_cluster_agent_url_configuration", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Configures a receptive agent's outbound connection address and TLS material so GitLab can connect to it directly (GitLab 17.4+), instead of waiting for the agent to connect in.")]
    public async Task<CallToolResult> CreateClusterAgentUrlConfigurationAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id.")]
        long agentId,
        [Description("The address GitLab should connect to, e.g. \"grpc://agent.example.com:443\".")]
        string url,
        [Description("Optional PEM-encoded client certificate GitLab should present when connecting.")]
        string? clientCert = null,
        [Description(
            "Optional PEM-encoded private key matching clientCert. Sent to GitLab as connection credentials; GitLab does not return it back in any later read.")]
        string? clientKey = null,
        [Description("Optional PEM-encoded CA certificate GitLab should trust for this agent's TLS.")]
        string? caCert = null,
        [Description(
            "Optional TLS server-name override, when the agent's certificate does not match its connection host.")]
        string? tlsHost = null,
        CancellationToken cancellationToken = default)
    {
        Uri agentUrl;
        try
        {
            agentUrl = new Uri(url, UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            throw new McpException("url is not a valid absolute URL.");
        }

        var request = new CreateClusterAgentUrlConfigurationRequest
        {
            Url = agentUrl,
            ClientCert = clientCert,
            ClientKey = clientKey,
            CaCert = caCert,
            TlsHost = tlsHost
        };

        var configuration =
            await clusterAgents.CreateUrlConfigurationAsync((ProjectId)project, agentId, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(configuration),
            "projects/:id/cluster_agents/:agent_id/url_configurations (create)");
    }

    [McpServerTool(Name = "gitlab_delete_cluster_agent_url_configuration", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Removes a cluster agent's receptive-mode URL configuration. The agent reverts to connecting in (push mode).")]
    public async Task<ClusterAgentUrlConfigurationDeleteResult> DeleteClusterAgentUrlConfigurationAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The cluster agent's numeric id.")]
        long agentId,
        [Description("The URL configuration's numeric id (from gitlab_get_cluster_agent_url_configuration).")]
        long urlConfigurationId,
        CancellationToken cancellationToken = default)
    {
        await clusterAgents.DeleteUrlConfigurationAsync((ProjectId)project, agentId, urlConfigurationId,
            cancellationToken);
        return new ClusterAgentUrlConfigurationDeleteResult(agentId, urlConfigurationId, true);
    }

    [McpServerTool(Name = "gitlab_export_dependencies", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Starts an asynchronous dependency-list or SBOM export scoped to a project, a whole group, or a single pipeline. Poll gitlab_get_dependency_export_status with the returned export id until it reports finished. GitLab Ultimate feature; expect 403/404 on lower plans.")]
    public async Task<CallToolResult> ExportDependenciesAsync(
        [Description(
            "Export scope: \"project\" (one project's dependencies), \"group\" (every project under a group), or \"pipeline\" (the merged SBOM one pipeline detected).")]
        string scope,
        [Description(
            "Required when scope is \"project\": numeric project id or URL-encoded path. Required when scope is \"group\": numeric group id or URL-encoded path. Ignored when scope is \"pipeline\".")]
        string? projectOrGroup = null,
        [Description("Required when scope is \"pipeline\": the pipeline's numeric id. Ignored otherwise.")]
        long? pipelineId = null,
        [Description(
            "Export format. For scope \"project\": \"dependency_list\" (default), \"csv\", or \"cyclonedx_1_6_json\". For scope \"group\": \"json_array\" (default) or \"csv\". Ignored for scope \"pipeline\" (always an SBOM).")]
        string? format = null,
        [Description("Whether GitLab should email the caller when the export finishes. Default false.")]
        bool sendEmail = false,
        CancellationToken cancellationToken = default)
    {
        GitLabDependencyListExport export;

        switch (scope.ToLowerInvariant())
        {
            case "project":
            {
                if (string.IsNullOrWhiteSpace(projectOrGroup))
                    throw new McpException("projectOrGroup is required when scope is \"project\".");

                GitLabProjectDependencyListExportType? exportType = format?.ToLowerInvariant() switch
                {
                    null => null,
                    "dependency_list" => GitLabProjectDependencyListExportType.DependencyList,
                    "csv" => GitLabProjectDependencyListExportType.Csv,
                    "cyclonedx_1_6_json" => GitLabProjectDependencyListExportType.CycloneDx16Json,
                    _ => throw new McpException(
                        "format for scope \"project\" must be one of: dependency_list, csv, cyclonedx_1_6_json.")
                };

                export = await dependencies.CreateProjectExportAsync(
                    (ProjectId)projectOrGroup,
                    new CreateProjectDependencyListExportRequest { SendEmail = sendEmail, ExportType = exportType },
                    cancellationToken);
                break;
            }

            case "group":
            {
                if (string.IsNullOrWhiteSpace(projectOrGroup))
                    throw new McpException("projectOrGroup is required when scope is \"group\".");

                GitLabGroupDependencyListExportType? exportType = format?.ToLowerInvariant() switch
                {
                    null => null,
                    "json_array" => GitLabGroupDependencyListExportType.JsonArray,
                    "csv" => GitLabGroupDependencyListExportType.Csv,
                    _ => throw new McpException("format for scope \"group\" must be one of: json_array, csv.")
                };

                export = await dependencies.CreateGroupExportAsync(
                    (GroupId)projectOrGroup,
                    new CreateGroupDependencyListExportRequest { SendEmail = sendEmail, ExportType = exportType },
                    cancellationToken);
                break;
            }

            case "pipeline":
            {
                if (pipelineId is null) throw new McpException("pipelineId is required when scope is \"pipeline\".");

                export = await dependencies.CreatePipelineExportAsync(
                    pipelineId.Value,
                    new CreatePipelineDependencyListExportRequest
                        { SendEmail = sendEmail, ExportType = GitLabPipelineDependencyListExportType.Sbom },
                    cancellationToken);
                break;
            }

            default:
                throw new McpException("scope must be one of: project, group, pipeline.");
        }

        return GitLabContent.Wrap(InfraMapper.ToSummary(export), "dependency_list_exports (create)");
    }

    [McpServerTool(Name = "gitlab_upload_sbom_scan", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Submits a CI job's software bill of materials (SBOM) for vulnerability scanning, after checking it against the instance's upload size limit. Provide the document's raw bytes base64-encoded; documents whose decoded size exceeds 8 MB are refused rather than sent.")]
    public async Task<CallToolResult> UploadSbomScanAsync(
        [Description("The CI job's numeric id the SBOM belongs to.")]
        long jobId,
        [Description(
            "The uploaded document's digest (e.g. a hex SHA-256 digest of the SBOM file) — record it to look the scan up later via gitlab_get_sbom_scan.")]
        string sbomDigest,
        [Description("The SBOM document's raw bytes (CycloneDX JSON), base64-encoded.")]
        string contentBase64,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64.");
        }

        if (bytes.Length > MaxSbomScanBytes)
            throw new McpException(
                $"Decoded SBOM document is {bytes.Length} bytes, over the {MaxSbomScanBytes}-byte limit this tool can upload. Upload it directly to GitLab instead.");

        await dependencies.AuthorizeSbomScanUploadAsync(
            jobId,
            new AuthorizeSbomScanUploadRequest { FileSize = bytes.Length },
            cancellationToken);

        await using var stream = new MemoryStream(bytes);
        var upload = new GitLabFileUpload
        {
            Content = stream,
            FileName = "sbom.json",
            ContentType = "application/json",
            FieldName = "file"
        };

        var scan = await dependencies.UploadSbomScanAsync(jobId, upload, sbomDigest, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(scan), "jobs/:job_id/sbom_scans (upload)");
    }

    [McpServerTool(Name = "gitlab_reuse_sbom_scan", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Attaches an existing scan's results to a new job instead of re-uploading and re-scanning an identical SBOM digest.")]
    public async Task<CallToolResult> ReuseSbomScanAsync(
        [Description("The new CI job's numeric id to attach the existing scan results to.")]
        long jobId,
        [Description("The SBOM document's digest whose existing scan results should be reused.")]
        string sbomDigest,
        [Description(
            "Package-URL (purl) types the reused scan should cover, e.g. [\"npm\", \"pypi\"]. Required by GitLab's reuse endpoint — list every type the new job's SBOM actually contains.")]
        string[] purlTypes,
        CancellationToken cancellationToken = default)
    {
        if (purlTypes.Length == 0) throw new McpException("purlTypes must contain at least one package-url type.");

        var request = new ReuseSbomScanRequest { PurlTypes = purlTypes };
        var scan = await dependencies.ReuseSbomScanAsync(jobId, sbomDigest, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(scan), "jobs/:job_id/sbom_scans/:sbom_digest (reuse)");
    }

    [McpServerTool(Name = "gitlab_delete_error_tracking_client_key", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Revokes an error-tracking client key so applications using it can no longer report errors into this project. Returns the key as it looked just before deletion so the caller can confirm which one was removed.")]
    public async Task<CallToolResult> DeleteErrorTrackingClientKeyAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The client key's numeric id to revoke (from gitlab_list_error_tracking_client_keys).")]
        long keyId,
        CancellationToken cancellationToken = default)
    {
        var key = await errorTracking.DeleteClientKeyAsync((ProjectId)project, keyId, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(key),
            "projects/:id/error_tracking/client_keys/:key_id (delete)");
    }

    [McpServerTool(Name = "gitlab_create_error_tracking_settings", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Turns on error tracking for a project for the first time, choosing integrated (GitLab-hosted) or external Sentry mode. Requires the Maintainer or Owner role.")]
    public async Task<CallToolResult> CreateErrorTrackingSettingsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Whether error tracking should be active.")]
        bool active,
        [Description(
            "True for GitLab's own integrated error tracking; false to send errors to an external Sentry instance instead.")]
        bool integrated,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateErrorTrackingSettingsRequest { Active = active, Integrated = integrated };
        var settings = await errorTracking.CreateSettingsAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(settings), "projects/:id/error_tracking/settings (create)");
    }

    [McpServerTool(Name = "gitlab_update_error_tracking_settings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Toggles error tracking on or off, and optionally switches between integrated and external Sentry mode, on an already-configured project. Requires the Maintainer or Owner role.")]
    public async Task<CallToolResult> UpdateErrorTrackingSettingsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Whether error tracking should be active.")]
        bool active,
        [Description(
            "True for GitLab's own integrated error tracking; false for external Sentry. Omit to leave the current mode unchanged.")]
        bool? integrated = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateErrorTrackingSettingsRequest { Active = active, Integrated = integrated };
        var settings = await errorTracking.UpdateSettingsAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(settings), "projects/:id/error_tracking/settings (update)");
    }

    [McpServerTool(Name = "gitlab_upload_alert_metric_image", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Attaches a metric screenshot/graph to an alert, optionally linking to more detail elsewhere (e.g. an external dashboard) with a caption. Provide the image's raw bytes base64-encoded; images over 4 MB are refused rather than sent.")]
    public async Task<CallToolResult> UploadAlertMetricImageAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The alert's project-scoped internal id (iid) to attach the image to.")]
        long alertIid,
        [Description("The image file's raw bytes, base64-encoded.")]
        string contentBase64,
        [Description("Filename for the image, e.g. \"cpu-spike.png\".")]
        string fileName,
        [Description("Optional link to more metric detail elsewhere, e.g. an external dashboard URL. Omit for none.")]
        string? url = null,
        [Description("Optional caption for the image, or for url when given. Omit for none.")]
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64.");
        }

        if (bytes.Length > MaxMetricImageBytes)
            throw new McpException(
                $"Decoded image is {bytes.Length} bytes, over the {MaxMetricImageBytes}-byte limit this tool can upload. Upload it directly to GitLab instead.");

        Uri? linkUrl = null;
        if (!string.IsNullOrWhiteSpace(url))
            try
            {
                linkUrl = new Uri(url, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                throw new McpException("url is not a valid absolute URL.");
            }

        await using var stream = new MemoryStream(bytes);
        var upload = new GitLabFileUpload
        {
            Content = stream,
            FileName = fileName
            // FieldName left at its default: GitLabFileUpload's own doc names "an issue metric image"
            // as one of the endpoints that already matches DefaultFieldName ("file") — only a handful
            // of endpoints (avatar, for one) need to override it.
        };

        var image = await alertManagement.UploadMetricImageAsync(
            (ProjectId)project, alertIid, upload, linkUrl, caption, cancellationToken);

        return GitLabContent.Wrap(InfraMapper.ToSummary(image),
            "projects/:id/alert_management_alerts/:alert_iid/metric_images (upload)");
    }

    [McpServerTool(Name = "gitlab_update_alert_metric_image", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a metric image's caption and/or linked-detail URL without re-uploading the image file itself — there is no endpoint to replace the file.")]
    public async Task<CallToolResult> UpdateAlertMetricImageAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The alert's project-scoped internal id (iid).")]
        long alertIid,
        [Description("The metric image's numeric id (from gitlab_list_alert_metric_images).")]
        long metricImageId,
        [Description("New link to more metric detail elsewhere. Omit to leave unchanged.")]
        string? url = null,
        [Description("New caption for the image, or for url. Omit to leave unchanged.")]
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        if (url is null && caption is null) throw new McpException("Provide at least one of url or caption to change.");

        Uri? linkUrl = null;
        if (url is not null)
            try
            {
                linkUrl = new Uri(url, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                throw new McpException("url is not a valid absolute URL.");
            }

        var request = new UpdateMetricImageRequest { Url = linkUrl, Caption = caption };
        var image = await alertManagement.UpdateMetricImageAsync((ProjectId)project, alertIid, metricImageId, request,
            cancellationToken);
        return GitLabContent.Wrap(InfraMapper.ToSummary(image),
            "projects/:id/alert_management_alerts/:alert_iid/metric_images/:metric_image_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_alert_metric_image", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Removes a metric image from an alert.")]
    public async Task<AlertMetricImageDeleteResult> DeleteAlertMetricImageAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The alert's project-scoped internal id (iid).")]
        long alertIid,
        [Description("The metric image's numeric id (from gitlab_list_alert_metric_images).")]
        long metricImageId,
        CancellationToken cancellationToken = default)
    {
        await alertManagement.DeleteMetricImageAsync((ProjectId)project, alertIid, metricImageId, cancellationToken);
        return new AlertMetricImageDeleteResult(alertIid, metricImageId, true);
    }
}