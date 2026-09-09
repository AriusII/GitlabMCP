namespace GitlabMCP.Contracts.Infra;

/// <summary>
///     Projection records for the "infra" domain (Terraform state protection, cluster-agent tokens,
///     dependency scanning, error tracking, attestations). Every string here can originate from GitLab
///     (state names, agent/token names, package/license identifiers, …) so every tool returning these
///     wraps via <see cref="GitLabContent" />.
/// </summary>

// --- Dependencies (IDependenciesClient) ---
public sealed record DependencyVulnerabilitySummary(
    long? Id,
    string? Name,
    string? Severity,
    string? Url);

public sealed record DependencyLicenseSummary(
    string? SpdxIdentifier,
    string? Name,
    string? Url);

public sealed record DependencySummary(
    string? Name,
    string? Version,
    string? PackageManager,
    string? DependencyFilePath,
    bool? Malware,
    IReadOnlyList<DependencyVulnerabilitySummary> Vulnerabilities,
    IReadOnlyList<DependencyLicenseSummary> Licenses);

public sealed record DependencyListResult(IReadOnlyList<DependencySummary> Dependencies, bool Truncated);

// --- Cluster agent tokens (IClusterAgentsClient) ---

/// <summary>
///     Token metadata only — never the secret. See <see cref="ClusterAgentTokenCreated" /> for the one call that
///     carries it.
/// </summary>
public sealed record ClusterAgentTokenSummary(
    long Id,
    string? Name,
    string? Description,
    long? AgentId,
    string? Status,
    DateTimeOffset? CreatedAt,
    long? CreatedByUserId,
    DateTimeOffset? LastUsedAt);

public sealed record ClusterAgentTokenListResult(IReadOnlyList<ClusterAgentTokenSummary> Tokens, bool Truncated);

/// <summary>
///     The one deliberate exception to "never project a secret-bearing field" (CLAUDE.md rule 5): this is the
///     single response that carries the freshly minted agent token, exactly once, per
///     <c>IClusterAgentsClient.CreateTokenAsync</c>'s own contract. Never reuse this shape for a list or get.
/// </summary>
public sealed record ClusterAgentTokenCreated(
    long Id,
    string? Name,
    string? Description,
    long? AgentId,
    string? Status,
    DateTimeOffset? CreatedAt,
    long? CreatedByUserId,
    DateTimeOffset? LastUsedAt,
    string Token);

// --- Terraform state protection rules (ITerraformStatesClient) ---

public sealed record TerraformStateProtectionRuleSummary(
    long Id,
    long? ProjectId,
    string? StateName,
    string? MinimumAccessLevelForWrite,
    string? AllowedFrom);

public sealed record TerraformStateProtectionRuleListResult(
    IReadOnlyList<TerraformStateProtectionRuleSummary> Rules,
    bool Truncated);

// --- Error tracking (IErrorTrackingClient) ---

/// <summary>
///     <see cref="PublicKey" /> and <see cref="SentryDsn" /> are, by design, the public half of the
///     project/client-key pair error-reporting clients embed to know where to send errors — not a
///     credential in the closed-list sense (CLAUDE.md rule 5): they authorize submitting error events, not
///     reading or acting on anything.
/// </summary>
public sealed record ErrorTrackingClientKeySummary(
    long Id,
    bool? Active,
    string? PublicKey,
    string? SentryDsn);

public sealed record ErrorTrackingClientKeyListResult(
    IReadOnlyList<ErrorTrackingClientKeySummary> Keys,
    bool Truncated);

// --- Attestations (IAttestationsClient) ---

/// <summary>
///     The raw attestation bundle, base64-encoded (it is not JSON/text) — capped at
///     <c>InfraTools.MaxAttestationBytes</c>; a bundle over that limit is refused rather than truncated,
///     because a partial signature/provenance bundle verifies as neither valid nor cleanly invalid.
/// </summary>
public sealed record AttestationBundle(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

// --- Terraform state documents, locks and versions (ITerraformStatesClient) ---

/// <summary>
///     A Terraform state document, base64-encoded (it is JSON, but treated as opaque bytes rather than
///     re-parsed) — capped at <c>InfraTools.MaxTerraformStateBytes</c>; a document over that limit is
///     refused rather than truncated, because a partial state file cannot be safely inspected or re-applied.
/// </summary>
public sealed record TerraformStateDocument(
    string StateName,
    long? Serial,
    string? ContentType,
    long? ContentLengthBytes,
    string ContentBase64);

/// <summary><c>StateName</c> here is the caller's own input, echoed back — never GitLab-authored text.</summary>
public sealed record TerraformStateUploadResult(string StateName, bool Uploaded);

public sealed record TerraformStateDeleteResult(string StateName, bool Deleted);

public sealed record TerraformStateVersionDeleteResult(string StateName, long Serial, bool Deleted);

public sealed record TerraformStateLockResult(string StateName, string LockId, bool Locked);

public sealed record TerraformStateUnlockResult(string StateName, bool Forced, bool Unlocked);

public sealed record TerraformStateProtectionRuleDeleteResult(long RuleId, bool Deleted);

// --- Cluster agents (IClusterAgentsClient) ---

/// <summary>The config project's identity only — name and path, not the full project projection another domain owns.</summary>
public sealed record ClusterAgentConfigProjectSummary(
    long Id,
    string? Name,
    string? PathWithNamespace);

public sealed record ClusterAgentSummary(
    long Id,
    string? Name,
    ClusterAgentConfigProjectSummary? ConfigProject,
    DateTimeOffset? CreatedAt,
    long? CreatedByUserId,
    bool? IsReceptive);

public sealed record ClusterAgentListResult(IReadOnlyList<ClusterAgentSummary> Agents, bool Truncated);

public sealed record ClusterAgentDeleteResult(long AgentId, bool Deleted);

public sealed record ClusterAgentTokenRevokeResult(long AgentId, long TokenId, bool Revoked);

/// <summary>
///     The public half of a receptive agent's connection material (address, public cert, TLS host) —
///     never <c>ClientKey</c> (the private key): <c>GitLabClusterAgentUrlConfiguration</c>, the read model
///     GitLab returns, has no such property, so the private key supplied at creation is never readable back.
/// </summary>
public sealed record ClusterAgentUrlConfigurationSummary(
    long Id,
    long? AgentId,
    string? Url,
    string? PublicKey,
    string? ClientCert,
    string? CaCert,
    string? TlsHost);

/// <summary>Wraps a nullable configuration in a named field, since an agent has zero-or-one of these — never a list.</summary>
public sealed record ClusterAgentUrlConfigurationResult(ClusterAgentUrlConfigurationSummary? Configuration);

public sealed record ClusterAgentUrlConfigurationDeleteResult(long AgentId, long UrlConfigurationId, bool Deleted);

// --- Dependency exports (IDependenciesClient) ---

public sealed record DependencyExportSummary(
    long Id,
    bool? HasFinished,
    string? SelfUrl,
    string? DownloadUrl);

// --- Backlog part 2/2: dependency exports (download), occurrence vulnerabilities, attestations,
// SAST file scan, SBOM scans, error tracking settings, alert metric images ---

/// <summary>
///     A finished dependency-list/SBOM export's raw file, base64-encoded — capped at
///     <c>InfraTools.MaxDependencyExportBytes</c>; a file over that limit is refused rather than truncated,
///     same reasoning as <see cref="TerraformStateDocument" /> and <see cref="AttestationBundle" />.
/// </summary>
public sealed record DependencyExportFile(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

public sealed record DependencyVulnerabilityListResult(
    IReadOnlyList<DependencyVulnerabilitySummary> Vulnerabilities,
    bool Truncated);

public sealed record AttestationSummary(
    long Id,
    long? Iid,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpireAt,
    long? ProjectId,
    long? BuildId,
    string? Status,
    string? PredicateKind,
    string? PredicateType,
    string? SubjectDigest,
    string? DownloadUrl);

public sealed record AttestationListResult(IReadOnlyList<AttestationSummary> Attestations, bool Truncated);

// gitlab_scan_file_for_vulnerabilities has no record: GitLab defines no fixed schema for that
// experimental endpoint, so its result travels as raw JSON text via GitLabContent.WrapText instead.

public sealed record SbomScanResult(
    long Id,
    string? DownloadUrl,
    bool? Throttled,
    int? ProjectThrottlingResetsIn,
    string? AdvisoryDbState);

public sealed record ErrorTrackingSettingsSummary(
    bool? Active,
    string? ProjectName,
    string? SentryExternalUrl,
    string? ApiUrl,
    bool? Integrated);

public sealed record AlertMetricImageSummary(
    long Id,
    DateTimeOffset? CreatedAt,
    string? Filename,
    string? FilePath,
    string? Url,
    string? Caption);

public sealed record AlertMetricImageListResult(IReadOnlyList<AlertMetricImageSummary> Images, bool Truncated);

public sealed record AlertMetricImageDeleteResult(long AlertIid, long MetricImageId, bool Deleted);