namespace GitlabMCP.Contracts.Lifecycle;

/// <summary>
///     Import/export, ML experiment tracking and Duo Chat projection records ("lifecycle" domain).
///     GitLab Duo Chat's own answer is wrapped as raw text via <see cref="GitLabContent.WrapText" /> instead
///     of a record here (its shape is an untyped <c>JsonElement</c> envelope on the library side — see
///     <c>LifecycleTools.DuoChatAsync</c>), so no record exists for it.
/// </summary>

// --- gitlab_list_project_templates ---
public sealed record ProjectTemplateSummary(string Key, string? Name);

public sealed record ProjectTemplateListResult(IReadOnlyList<ProjectTemplateSummary> Templates, bool Truncated);

// --- gitlab_list_bulk_import_entities / gitlab_create_bulk_import ---

public sealed record BulkImportEntitySummary(
    long Id,
    long? BulkImportId,
    string? EntityType,
    string? Status,
    string? SourceFullPath,
    string? DestinationFullPath,
    string? DestinationNamespace,
    string? DestinationSlug,
    int FailureCount,
    bool? HasFailures,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BulkImportEntityListResult(IReadOnlyList<BulkImportEntitySummary> Entities, bool Truncated);

public sealed record BulkImportSummary(
    long Id,
    string? Status,
    string? SourceType,
    string? SourceUrl,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool? HasFailures);

// --- gitlab_list_ml_experiments / gitlab_create_ml_experiment ---

public sealed record MlExperimentTag(string? Key, string? Value);

public sealed record MlExperimentSummary(
    string? ExperimentId,
    string? Name,
    string? LifecycleStage,
    string? ArtifactLocation,
    IReadOnlyList<MlExperimentTag> Tags);

public sealed record MlExperimentListResult(IReadOnlyList<MlExperimentSummary> Experiments, bool Truncated);

public sealed record MlExperimentCreateResult(string? ExperimentId);

// --- gitlab_get_group_export_download_info ---

/// <summary>
///     Headers-only summary of a group export archive. Deliberately excludes the archive body: the tool
///     disposes the underlying <c>GitLabFileResponse</c> without reading its <c>Content</c> stream.
/// </summary>
public sealed record GroupExportDownloadInfo(
    string? FileName,
    string? ContentType,
    long? ContentLengthBytes,
    int StatusCode);

// --- gitlab_export_project ---

/// <summary>
///     All-scalar acknowledgement of a scheduled export — no GitLab-authored string, so the tool returns this bare
///     rather than through <see cref="GitLabContent" />.
/// </summary>
public sealed record ProjectExportScheduleResult(bool Scheduled);

// --- gitlab_get_project_export_status ---

public sealed record ProjectExportStatusSummary(
    long Id,
    string? Name,
    string? PathWithNamespace,
    DateTimeOffset? CreatedAt,
    string? ExportStatus,
    string? ApiUrl,
    string? WebUrl);

// --- gitlab_import_project_from_git / gitlab_get_project_import_status ---

public sealed record ProjectImportStatusSummary(
    long Id,
    string? Name,
    string? PathWithNamespace,
    DateTimeOffset? CreatedAt,
    string? ImportStatus,
    string? ImportType,
    string? ImportError,
    int FailedRelationsCount);

// --- gitlab_import_project_from_github ---

public sealed record ImportedProjectSummary(
    long Id,
    string? Name,
    string? FullPath,
    string? FullName,
    bool? Forked);

// --- gitlab_get_project_template ---

public sealed record ProjectTemplateDetail(
    string? Key,
    string? Name,
    string? Nickname,
    string? Description,
    string? Content,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations,
    bool Truncated);

// --- gitlab_export_group ---

/// <summary>
///     All-scalar acknowledgement of a scheduled group export — no GitLab-authored string, so the tool returns this
///     bare rather than through <see cref="GitLabContent" />.
/// </summary>
public sealed record GroupExportScheduleResult(bool Scheduled);

// --- gitlab_get_group_relations_export_status ---

public sealed record GroupRelationsExportStatusSummary(
    string? Relation,
    string? Status,
    string? Error,
    DateTimeOffset? UpdatedAt,
    bool? Batched,
    int? BatchesCount,
    int? TotalObjectsCount);

public sealed record GroupRelationsExportStatusListResult(
    IReadOnlyList<GroupRelationsExportStatusSummary> Relations,
    bool Truncated);

// --- gitlab_list_bulk_imports ---

public sealed record BulkImportListResult(IReadOnlyList<BulkImportSummary> Imports, bool Truncated);

// --- gitlab_list_bulk_import_entity_failures ---

public sealed record BulkImportEntityFailureSummary(
    string? Relation,
    string? ExceptionMessage,
    string? ExceptionClass,
    string? SourceUrl,
    string? SourceTitle);

public sealed record BulkImportEntityFailureListResult(
    IReadOnlyList<BulkImportEntityFailureSummary> Failures,
    bool Truncated);

// --- gitlab_import_github_gists ---

/// <summary>
///     All-scalar acknowledgement of a scheduled gist import — no GitLab-authored string, so the tool returns this
///     bare rather than through <see cref="GitLabContent" />.
/// </summary>
public sealed record GitHubGistsImportScheduleResult(bool Scheduled);

// --- gitlab_create_ml_run / gitlab_get_ml_run / gitlab_search_ml_runs / gitlab_update_ml_run ---

public sealed record MlRunMetric(string? Key, double? Value, long? TimestampUnixMs, long? Step);

public sealed record MlRunSummary(
    string? RunId,
    string? RunName,
    string? ExperimentId,
    string? Status,
    string? LifecycleStage,
    string? ArtifactUri,
    long? StartTimeUnixMs,
    long? EndTimeUnixMs,
    IReadOnlyList<MlRunMetric> Metrics,
    IReadOnlyList<MlExperimentTag> Params,
    IReadOnlyList<MlExperimentTag> Tags);

/// <summary>
///     The underlying <c>SearchRunsAsync</c> library method types its response as a single run rather than a
///     page of them (see its own XML remarks), so at most one matching run is ever surfaced here even when
///     more exist — <c>Run</c> is null when nothing matched.
/// </summary>
public sealed record MlRunSearchResult(MlRunSummary? Run);

// --- gitlab_log_ml_run_data ---

/// <summary>
///     All-scalar acknowledgement of a logged batch — no GitLab-authored string, so the tool returns this bare rather
///     than through <see cref="GitLabContent" />.
/// </summary>
public sealed record MlRunLogBatchResult(int MetricsLogged, int ParametersLogged);

// --- gitlab_list_ml_models / gitlab_create_ml_model / gitlab_delete_ml_model ---

public sealed record MlModelSummary(
    string? Name,
    string? Description,
    long? CreatedAtUnixMs,
    long? UpdatedAtUnixMs,
    IReadOnlyList<MlExperimentTag> Tags);

public sealed record MlModelListResult(IReadOnlyList<MlModelSummary> Models, bool Truncated);

// --- gitlab_get_ml_model_version ---

/// <summary>
///     Shared shape for the two library types this resolves to (<c>GitLabMlflowModelVersion</c> and
///     <c>GitLabMlModelVersion</c>) — identical properties, two separate CLR types depending on which of
///     the three underlying endpoints answered. See <c>LifecycleMapper</c>'s two <c>ToSummary</c> overloads.
/// </summary>
public sealed record MlModelVersionSummary(
    string? Name,
    string? Version,
    long? CreatedAtUnixMs,
    long? UpdatedAtUnixMs,
    string? UserId,
    string? CurrentStage,
    string? Description,
    string? Source,
    string? RunId,
    string? Status,
    string? StatusMessage,
    IReadOnlyList<MlExperimentTag> Tags,
    string? RunLink,
    IReadOnlyList<string> Aliases);

// --- gitlab_duo_execute_glql_query ---

public sealed record GlqlFieldSummary(string? Key, string? Label, string? Name, string? Field, string? Type);

/// <summary>
///     <see cref="NodesJson" /> carries the query's matching rows as raw JSON text: the library types this
///     field as an untyped <c>JsonElement</c> because GLQL's row shape depends on the query's own
///     <c>display:</c> clause, so there is no fixed schema for this server to project against. The tool
///     truncates it to <c>MaxDuoResponseChars</c> before wrapping.
/// </summary>
public sealed record GlqlQueryResult(
    bool? Success,
    string? Error,
    int? Count,
    string? NodesJson,
    bool? HasNextPage,
    string? EndCursor,
    IReadOnlyList<GlqlFieldSummary> Fields);

// --- gitlab_duo_check_code_suggestions_enabled ---

/// <summary>
///     All-scalar acknowledgement — reaching this point without an exception already means Code Suggestions is
///     enabled, so the tool returns this bare rather than through <see cref="GitLabContent" />.
/// </summary>
public sealed record CodeSuggestionsEnabledResult(bool Enabled);

// --- gitlab_register_ai_agent_identity ---

public sealed record AgentIdentitySummary(
    long Id,
    string? AgentType,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? CreatedAt);

// --- gitlab_list_ai_agent_sessions / gitlab_create_ai_agent_session / gitlab_complete_ai_agent_session ---

public sealed record AgentSessionSummary(
    long Id,
    string? AgentType,
    long? AgentIdentityId,
    long? UserId,
    string? SyncType,
    string? Status,
    string? Goal,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record AgentSessionListResult(IReadOnlyList<AgentSessionSummary> Sessions, bool Truncated);

// --- gitlab_list_experiments ---

public sealed record ExperimentSummary(
    string? Key,
    IReadOnlyList<string> Context,
    string? DefinitionType,
    string? DefinitionGroup,
    bool? DefaultEnabled,
    string? Milestone,
    string? CurrentState);

public sealed record ExperimentListResult(IReadOnlyList<ExperimentSummary> Experiments, bool Truncated);

// --- gitlab_get_experiment_assignment / gitlab_force_experiment_assignment ---

public sealed record ExperimentAssignmentSummary(string? Experiment, string? Variant, string? ContextKey, bool? Cached);

// --- gitlab_list_admin_model_records ---

public sealed record AdminModelChecksumInfo(
    string? Checksum,
    string? LastChecksum,
    string? ChecksumState,
    string? ChecksumRetryCount,
    string? ChecksumRetryAt,
    string? ChecksumFailure);

public sealed record AdminModelRecordSummary(
    string? ModelClass,
    string? RecordIdentifierJson,
    DateTimeOffset? CreatedAt,
    long? FileSizeBytes,
    AdminModelChecksumInfo? ChecksumInformation);

public sealed record AdminModelRecordListResult(IReadOnlyList<AdminModelRecordSummary> Records, bool Truncated);

// --- gitlab_get_database_dictionary_table ---

public sealed record DictionaryTableSummary(
    string? TableName,
    IReadOnlyList<string> FeatureCategories,
    string? TableSize);

public sealed record DictionaryTableListResult(IReadOnlyList<DictionaryTableSummary> Tables, bool Truncated);

// --- gitlab_ingest_rollout_event ---

public sealed record RolloutSummary(
    long? Id,
    long? Iid,
    string? State,
    string? WorkflowRef,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

// --- gitlab_list_jira_forge_subscriptions / gitlab_delete_jira_forge_subscription ---

public sealed record JiraForgeSubscriptionSummary(
    DateTimeOffset? CreatedAt,
    string? UnlinkPath,
    string? GroupName,
    string? GroupFullName,
    string? GroupDescription,
    string? GroupAvatarUrl);

public sealed record JiraForgeSubscriptionListResult(
    IReadOnlyList<JiraForgeSubscriptionSummary> Subscriptions,
    bool Truncated);

/// <summary>
///     The underlying <c>GitLabJiraConnectResult.Success</c> is typed as an untyped <c>JsonElement</c> rather
///     than a bool, so this preserves it as raw JSON text rather than guessing a fixed shape.
/// </summary>
public sealed record JiraForgeMutationResult(string? SuccessJson);

// --- gitlab_register_mobile_push_subscription ---

/// <summary>
///     All-scalar — Id and CreatedAt are the library's entire response shape for a registration, no GitLab-authored
///     string, so the tool returns this bare rather than through <see cref="GitLabContent" />.
/// </summary>
public sealed record MobilePushSubscriptionSummary(long Id, DateTimeOffset? CreatedAt);

// --- gitlab_unregister_mobile_push_subscription ---

/// <summary>
///     All-scalar acknowledgement — the endpoint answers with no body, so the tool returns this bare rather than
///     through <see cref="GitLabContent" />.
/// </summary>
public sealed record MobilePushSubscriptionUnregisterResult(bool Unregistered);