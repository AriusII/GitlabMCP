using GitLab.Client.Models;
using GitlabMCP.Contracts.Lifecycle;

namespace GitlabMCP.Mapping.Lifecycle;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> DTOs into the owned records in
///     <see cref="GitlabMCP.Contracts.Lifecycle" /> — never hands back the library type itself.
/// </summary>
public static class LifecycleMapper
{
    public static ProjectTemplateSummary ToSummary(GitLabProjectTemplate template)
    {
        return new ProjectTemplateSummary(
            template.Key,
            template.Name);
    }

    public static BulkImportEntitySummary ToSummary(GitLabBulkImportEntity entity)
    {
        return new BulkImportEntitySummary(
            entity.Id,
            entity.BulkImportId,
            entity.EntityType?.ToString(),
            entity.Status?.ToString(),
            entity.SourceFullPath,
            entity.DestinationFullPath,
            entity.DestinationNamespace,
            entity.DestinationSlug,
            entity.Failures?.Count ?? 0,
            entity.HasFailures,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public static BulkImportSummary ToSummary(GitLabBulkImport import)
    {
        return new BulkImportSummary(
            import.Id,
            import.Status?.ToString(),
            import.SourceType,
            import.SourceUrl?.ToString(),
            import.CreatedAt,
            import.UpdatedAt,
            import.HasFailures);
    }

    public static MlExperimentSummary ToSummary(GitLabMlflowExperiment experiment)
    {
        return new MlExperimentSummary(
            experiment.ExperimentId,
            experiment.Name,
            experiment.LifecycleStage,
            experiment.ArtifactLocation,
            experiment.Tags?.Select(static t => new MlExperimentTag(t.Key, t.Value)).ToList() ?? []);
    }

    public static MlExperimentCreateResult ToResult(GitLabMlflowNewExperiment experiment)
    {
        return new MlExperimentCreateResult(
            experiment.ExperimentId);
    }

    public static ProjectExportStatusSummary ToSummary(GitLabProjectExportStatus status)
    {
        return new ProjectExportStatusSummary(
            status.Id,
            status.Name,
            status.PathWithNamespace,
            status.CreatedAt,
            status.ExportStatus?.ToString(),
            status.Links?.ApiUrl?.ToString(),
            status.Links?.WebUrl?.ToString());
    }

    public static ProjectImportStatusSummary ToSummary(GitLabProjectImportStatus status)
    {
        return new ProjectImportStatusSummary(
            status.Id,
            status.Name,
            status.PathWithNamespace,
            status.CreatedAt,
            status.ImportStatus,
            status.ImportType,
            status.ImportError,
            status.FailedRelations?.Count ?? 0);
    }

    public static ImportedProjectSummary ToSummary(GitLabImportedProject project)
    {
        return new ImportedProjectSummary(
            project.Id,
            project.Name,
            project.FullPath,
            project.FullName,
            project.Forked);
    }

    public static ProjectTemplateDetail ToDetail(GitLabProjectTemplateDetail template)
    {
        return new ProjectTemplateDetail(
            template.Key,
            template.Name,
            template.Nickname,
            template.Description,
            template.Content,
            template.Conditions ?? [],
            template.Permissions ?? [],
            template.Limitations ?? [],
            false);
    }

    public static GroupRelationsExportStatusSummary ToSummary(GitLabGroupRelationsExportStatus status)
    {
        return new GroupRelationsExportStatusSummary(
            status.Relation,
            status.Status?.ToString(),
            status.Error,
            status.UpdatedAt,
            status.Batched,
            status.BatchesCount,
            status.TotalObjectsCount);
    }

    public static BulkImportEntityFailureSummary ToSummary(GitLabBulkImportEntityFailure failure)
    {
        return new BulkImportEntityFailureSummary(
            failure.Relation,
            failure.ExceptionMessage,
            failure.ExceptionClass,
            failure.SourceUrl?.ToString(),
            failure.SourceTitle);
    }

    /// <summary>
    ///     Full run (info + logged metrics/params/tags), e.g. from <c>CreateRunAsync</c>/<c>GetRunAsync</c>/
    ///     <c>SearchRunsAsync</c>.
    /// </summary>
    public static MlRunSummary ToSummary(GitLabMlflowRun run)
    {
        return ToSummary(run.Info, run.Data);
    }

    /// <summary>Info-only run, e.g. from <c>UpdateRunAsync</c>'s response, which carries no logged data.</summary>
    public static MlRunSummary ToSummary(GitLabMlflowRunInfo info)
    {
        return ToSummary(info, null);
    }

    private static MlRunSummary ToSummary(GitLabMlflowRunInfo? info, GitLabMlflowRunData? data)
    {
        return new MlRunSummary(
            info?.RunId,
            info?.RunName,
            info?.ExperimentId,
            info?.Status,
            info?.LifecycleStage,
            info?.ArtifactUri,
            info?.StartTime,
            info?.EndTime,
            data?.Metrics?.Select(static m => new MlRunMetric(m.Key, m.Value, m.Timestamp, m.Step)).ToList() ??
            [],
            data?.Params?.Select(static p => new MlExperimentTag(p.Key, p.Value)).ToList() ?? [],
            data?.Tags?.Select(static t => new MlExperimentTag(t.Key, t.Value)).ToList() ?? []);
    }

    public static MlModelSummary ToSummary(GitLabMlModel model)
    {
        return new MlModelSummary(
            model.Name,
            model.Description,
            model.CreationTimestamp,
            model.LastUpdatedTimestamp,
            model.Tags?.Select(static t => new MlExperimentTag(t.Key, t.Value)).ToList() ?? []);
    }

    /// <summary>From <c>IMlExperimentsClient.GetModelVersionAsync</c> — resolving by explicit version.</summary>
    public static MlModelVersionSummary ToSummary(GitLabMlflowModelVersion version)
    {
        return new MlModelVersionSummary(
            version.Name,
            version.Version,
            version.CreationTimestamp,
            version.LastUpdatedTimestamp,
            version.UserId,
            version.CurrentStage,
            version.Description,
            version.Source,
            version.RunId,
            version.Status,
            version.StatusMessage,
            version.Tags?.Select(static t => new MlExperimentTag(t.Key, t.Value)).ToList() ?? [],
            version.RunLink,
            version.Aliases ?? []);
    }

    /// <summary>
    ///     From <c>IMlModelsClient.GetLatestVersionAsync</c>/<c>GetVersionByAliasAsync</c> — an identically-shaped,
    ///     distinct CLR type.
    /// </summary>
    public static MlModelVersionSummary ToSummary(GitLabMlModelVersion version)
    {
        return new MlModelVersionSummary(
            version.Name,
            version.Version,
            version.CreationTimestamp,
            version.LastUpdatedTimestamp,
            version.UserId,
            version.CurrentStage,
            version.Description,
            version.Source,
            version.RunId,
            version.Status,
            version.StatusMessage,
            version.Tags?.Select(static t => new MlExperimentTag(t.Key, t.Value)).ToList() ?? [],
            version.RunLink,
            version.Aliases ?? []);
    }

    /// <summary>
    ///     Leaves <see cref="GlqlQueryResult.NodesJson" /> untruncated — the tool applies its own char cap before
    ///     wrapping.
    /// </summary>
    public static GlqlQueryResult ToResult(GitLabGlqlResult result)
    {
        return new GlqlQueryResult(
            result.Success,
            result.Error,
            result.Data?.Count,
            result.Data?.Nodes?.GetRawText(),
            result.Data?.PageInfo?.HasNextPage,
            result.Data?.PageInfo?.EndCursor,
            result.Fields?.Select(static f => new GlqlFieldSummary(f.Key, f.Label, f.Name, f.Field, f.Type))
                .ToList() ?? []);
    }

    public static AgentIdentitySummary ToSummary(GitLabAgentIdentity identity)
    {
        return new AgentIdentitySummary(
            identity.Id,
            identity.AgentType,
            identity.RevokedAt,
            identity.CreatedAt);
    }

    public static AgentSessionSummary ToSummary(GitLabAgentSession session)
    {
        return new AgentSessionSummary(
            session.Id,
            session.AgentType,
            session.AgentIdentityId,
            session.UserId,
            session.SyncType,
            session.Status,
            session.Goal,
            session.CreatedAt,
            session.UpdatedAt);
    }

    public static ExperimentSummary ToSummary(GitLabExperiment experiment)
    {
        return new ExperimentSummary(
            experiment.Key,
            experiment.Context ?? [],
            experiment.Definition?.Type,
            experiment.Definition?.Group,
            experiment.Definition?.DefaultEnabled,
            experiment.Definition?.Milestone,
            experiment.CurrentStatus?.State);
    }

    public static ExperimentAssignmentSummary ToSummary(GitLabExperimentAssignment assignment)
    {
        return new ExperimentAssignmentSummary(
            assignment.Experiment,
            assignment.Variant,
            assignment.ContextKey,
            assignment.Cached);
    }

    private static AdminModelChecksumInfo? ToSummary(GitLabAdminModelChecksumInfo? info)
    {
        return info is null
            ? null
            : new AdminModelChecksumInfo(
                info.Checksum,
                info.LastChecksum,
                info.ChecksumState,
                info.ChecksumRetryCount,
                info.ChecksumRetryAt,
                info.ChecksumFailure);
    }

    public static AdminModelRecordSummary ToSummary(GitLabAdminModelRecord record)
    {
        return new AdminModelRecordSummary(
            record.ModelClass,
            record.RecordIdentifier?.GetRawText(),
            record.CreatedAt,
            record.FileSize,
            ToSummary(record.ChecksumInformation));
    }

    public static DictionaryTableSummary ToSummary(GitLabDictionaryTable table)
    {
        return new DictionaryTableSummary(
            table.TableName,
            table.FeatureCategories ?? [],
            table.TableSize);
    }

    public static RolloutSummary ToSummary(GitLabCdRollout rollout)
    {
        return new RolloutSummary(
            rollout.Id,
            rollout.Iid,
            rollout.State,
            rollout.WorkflowRef,
            rollout.StartedAt,
            rollout.FinishedAt);
    }

    public static JiraForgeSubscriptionSummary ToSummary(GitLabJiraConnectSubscription subscription)
    {
        return new JiraForgeSubscriptionSummary(
            subscription.CreatedAt,
            subscription.UnlinkPath,
            subscription.Group?.Name,
            subscription.Group?.FullName,
            subscription.Group?.Description,
            subscription.Group?.AvatarUrl?.ToString());
    }

    public static JiraForgeMutationResult ToSummary(GitLabJiraConnectResult result)
    {
        return new JiraForgeMutationResult(
            result.Success?.GetRawText());
    }

    public static MobilePushSubscriptionSummary ToSummary(GitLabMobilePushSubscription subscription)
    {
        return new MobilePushSubscriptionSummary(
            subscription.Id,
            subscription.CreatedAt);
    }
}