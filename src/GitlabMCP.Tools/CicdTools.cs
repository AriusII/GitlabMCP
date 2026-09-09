using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using GitLab.Client.Abstractions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Cicd;
using GitlabMCP.Mapping.Cicd;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Pipelines, jobs, CI/CD variables and pipeline triggers (domain "cicd"). Every tool here returns
///     GitLab-authored or GitLab-controlled text (branch/ref names, statuses, job/pipeline names, usernames,
///     YAML content, job logs, ...) except <see cref="DeleteAllProjectArtifactsAsync" />,
///     <see cref="DeletePipelineAsync" />, <see cref="DeletePipelineScheduleAsync" />,
///     <see cref="RunPipelineScheduleAsync" />, <see cref="DeletePipelineScheduleVariableAsync" />,
///     <see cref="DeleteTriggerAsync" />, <see cref="DeleteCiVariableAsync" />,
///     <see cref="DeleteInstanceCiVariableAsync" />, <see cref="DeleteJobArtifactsAsync" />,
///     <see cref="GetJobTokenScopeAsync" />, <see cref="SetJobTokenScopeAsync" /> and
///     <see cref="RemoveJobTokenAllowlistEntryAsync" />, which each carry no
///     GitLab string at all (just a confirmation flag or a pair of settings booleans) — so every tool but
///     those wraps via <see cref="GitLabContent" /> (mcp-untrusted-content Step 1); <see cref="GetJobLogAsync" />
///     is raw text rather than a projection, so it goes through <see cref="GitLabContent.WrapText" /> instead
///     of <see cref="GitLabContent.Wrap{T}" />. <see cref="GitLabVariable.Value" /> and
///     <see cref="GitLabTrigger.Token" /> are never projected except the one deliberate exception on
///     <see cref="CreateTriggerAsync" /> — see CLAUDE.md rule 5 and the docstrings on
///     <see cref="Contracts.Cicd.VariableSummary" />/<see cref="Contracts.Cicd.TriggerSummary" />/
///     <see cref="Contracts.Cicd.TriggerCreateResult" />.
/// </summary>
[McpServerToolType]
public sealed class CicdTools(
    IPipelinesClient pipelines,
    IJobsClient jobs,
    IPipelineSchedulesClient pipelineSchedules,
    IVariablesClient variables,
    IJobArtifactsClient jobArtifacts,
    ITriggersClient triggers,
    IJobTokenScopeClient jobTokenScope,
    ICiLintClient ciLint,
    ICiCatalogClient ciCatalog)
{
    private const int MaxLimit = 100;
    private const int DefaultJobLogChars = 20_000;
    private const int MaxJobLogChars = 200_000;

    /// <summary>
    ///     Refuse-rather-than-truncate cap for <c>gitlab_download_job_artifacts</c> — mirrors
    ///     <c>InfraTools.MaxTerraformStateBytes</c>/<c>MaxDependencyExportBytes</c> for a whole binary archive.
    /// </summary>
    private const int MaxArtifactArchiveBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     Refuse-rather-than-truncate cap for <c>gitlab_get_job_artifact_file</c> — mirrors
    ///     <c>InfraTools.MaxAttestationBytes</c> for a single file rather than a whole archive.
    /// </summary>
    private const int MaxArtifactFileBytes = 4 * 1024 * 1024;

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_my_pipelines", ReadOnly = true, OpenWorld = false)]
    [Description("Lists pipelines the authenticated token's user triggered, across every project they can see.")]
    public async Task<CallToolResult> ListMyPipelinesAsync(
        [Description(
            "Only pipelines from this trigger source, e.g. \"push\", \"web\", \"schedule\", \"api\", \"trigger\", \"merge_request_event\". Omit for all.")]
        string? source = null,
        [Description("Only pipelines created at or after this ISO 8601 date-time. Omit for no lower bound.")]
        string? createdAfter = null,
        [Description("Only pipelines created at or before this ISO 8601 date-time. Omit for no upper bound.")]
        string? createdBefore = null,
        [Description("Maximum pipelines to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new UserPipelineListOptions
        {
            Source = source,
            CreatedAfter = ParseOptionalDate(createdAfter, nameof(createdAfter)),
            CreatedBefore = ParseOptionalDate(createdBefore, nameof(createdBefore)),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PipelineSummary> collected = [];
        var truncated = false;

        await foreach (var pipeline in pipelines.ListForCurrentUserAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PipelineMapper.ToSummary(pipeline));
        }

        return GitLabContent.Wrap(new PipelineListResult(collected, truncated), "pipelines?scope=me");
    }

    [McpServerTool(Name = "gitlab_list_jobs", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's jobs across all its pipelines, filterable by status/scope and ref.")]
    public async Task<CallToolResult> ListJobsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Only jobs in these states: \"created\", \"pending\", \"running\", \"failed\", \"success\", \"canceled\", \"canceling\", \"skipped\", \"manual\", \"scheduled\", \"waiting_for_resource\". Omit for all.")]
        string[]? scope = null,
        [Description("Only jobs from pipelines on this branch or tag. Omit for all refs.")]
        string? refName = null,
        [Description("Maximum jobs to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new JobListOptions { Scope = scope, Ref = refName, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<JobSummary> collected = [];
        var truncated = false;

        await foreach (var job in jobs.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(JobMapper.ToSummary(job));
        }

        return GitLabContent.Wrap(new JobListResult(collected, truncated), "projects/:id/jobs");
    }

    [McpServerTool(Name = "gitlab_list_pipeline_schedule_pipelines", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the pipelines a given schedule has triggered, to confirm it is actually firing.")]
    public async Task<CallToolResult> ListPipelineSchedulePipelinesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline schedule's numeric id (from gitlab_create_pipeline_schedule or the GitLab UI).")]
        long pipelineScheduleId,
        [Description("Only runs of this pipeline scope, e.g. \"finished\", \"branches\", \"tags\". Omit for all.")]
        string? scope = null,
        [Description("Only runs with this pipeline status, e.g. \"success\", \"failed\", \"canceled\". Omit for all.")]
        string? status = null,
        [Description("Maximum pipelines to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new PipelineScheduleRunListOptions
            { Scope = scope, Status = status, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<PipelineSummary> collected = [];
        var truncated = false;

        await foreach (var pipeline in pipelineSchedules.ListPipelinesAsync(project, pipelineScheduleId, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PipelineMapper.ToSummary(pipeline));
        }

        return GitLabContent.Wrap(new PipelineListResult(collected, truncated),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id/pipelines");
    }

    [McpServerTool(Name = "gitlab_list_ci_variables", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the CI/CD variables defined on a project or a group. Variable values are never returned by this tool, masked or not.")]
    public async Task<CallToolResult> ListCiVariablesAsync(
        [Description("The project or group to list variables for: numeric id, or URL-encoded \"namespace/path\".")]
        string id,
        [Description("Which kind of resource id was passed: \"project\" or \"group\". Default \"project\".")]
        string scope = "project",
        [Description("Maximum variables to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new VariableListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<VariableSummary> collected = [];
        var truncated = false;
        string source;

        if (IsGroupScope(scope, nameof(scope)))
        {
            source = "groups/:id/variables";
            await foreach (var variable in variables.ListGroupVariablesAsync(id, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(VariableMapper.ToSummary(variable));
            }
        }
        else
        {
            source = "projects/:id/variables";
            await foreach (var variable in variables.ListProjectVariablesAsync(id, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(VariableMapper.ToSummary(variable));
            }
        }

        return GitLabContent.Wrap(new VariableListResult(collected, truncated), source);
    }

    [McpServerTool(Name = "gitlab_list_job_artifacts", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the files inside one job's artifacts archive, to find the exact path to fetch.")]
    public async Task<CallToolResult> ListJobArtifactsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        [Description("Only list entries under this path inside the archive. Omit for the archive root.")]
        string? path = null,
        [Description(
            "If true, lists every file at every depth under path instead of just the immediate entries. Default false.")]
        bool recursive = false,
        [Description("Maximum entries to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new JobArtifactTreeListOptions
            { Path = path, Recursive = recursive, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<JobArtifactEntrySummary> collected = [];
        var truncated = false;

        await foreach (var entry in jobArtifacts.ListAsync(project, jobId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(JobMapper.ToSummary(entry));
        }

        return GitLabContent.Wrap(new JobArtifactEntryListResult(collected, truncated),
            "projects/:id/jobs/:job_id/artifacts (tree)");
    }

    [McpServerTool(Name = "gitlab_list_triggers", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the pipeline trigger tokens configured on a project. Token values are never returned by this tool -- use gitlab_create_trigger's result to capture a token, since GitLab only ever shows it once.")]
    public async Task<CallToolResult> ListTriggersAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum triggers to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<TriggerSummary> collected = [];
        var truncated = false;

        await foreach (var trigger in triggers.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(TriggerMapper.ToSummary(trigger));
        }

        return GitLabContent.Wrap(new TriggerListResult(collected, truncated), "projects/:id/triggers");
    }

    [McpServerTool(Name = "gitlab_list_job_token_allowlist", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the projects or groups allowed to authenticate against this project using its CI/CD job token.")]
    public async Task<CallToolResult> ListJobTokenAllowlistAsync(
        [Description("Project whose job-token allowlist to read: numeric id or URL-encoded \"namespace/path\".")]
        string project,
        [Description(
            "Which allowlist to read: \"project\" (other projects allowed in) or \"group\" (groups allowed in). Default \"project\".")]
        string kind = "project",
        [Description("Maximum entries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new JobTokenScopeAllowlistListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<AllowlistEntrySummary> collected = [];
        var truncated = false;
        string normalizedKind;

        if (IsGroupScope(kind, nameof(kind)))
        {
            normalizedKind = "group";
            await foreach (var group in jobTokenScope.ListGroupsAllowlistAsync(project, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(JobTokenScopeMapper.ToSummary(group));
            }
        }
        else
        {
            normalizedKind = "project";
            await foreach (var allowedProject in jobTokenScope.ListAllowlistAsync(project, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(JobTokenScopeMapper.ToSummary(allowedProject));
            }
        }

        return GitLabContent.Wrap(
            new JobTokenAllowlistResult(normalizedKind, collected, truncated),
            normalizedKind == "group"
                ? "projects/:id/job_token_scope/groups_allowlist"
                : "projects/:id/job_token_scope/allowlist");
    }

    [McpServerTool(Name = "gitlab_validate_ci_config", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lints a .gitlab-ci.yml -- either content you supply, or (when content is omitted) what's already committed to the project -- returning validity, errors, warnings and the merged/expanded YAML. This never throws for invalid YAML: check the valid field in the result, not the absence of an error.")]
    public async Task<CallToolResult> ValidateCiConfigAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Raw .gitlab-ci.yml content to validate. Omit to validate the configuration already committed to the project instead.")]
        string? content = null,
        [Description(
            "Branch, tag or commit context to resolve relative includes/rules against. Omit for the project's default branch.")]
        string? refName = null,
        [Description(
            "If true, evaluates rules/only/except as GitLab would for an actual pipeline run on refName, instead of a static syntax check. Default false.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        GitLabCiLintResult result;
        string source;

        if (content is not null)
        {
            var request = new ValidateCiConfigurationRequest { Content = content, Ref = refName, DryRun = dryRun };
            result = await ciLint.ValidateAsync(project, request, cancellationToken);
            source = "projects/:id/ci/lint (content)";
        }
        else
        {
            var options = new CiLintOptions
                { ContentRef = refName, DryRun = dryRun, DryRunRef = dryRun ? refName : null };
            result = await ciLint.ValidateProjectConfigurationAsync(project, options, cancellationToken);
            source = "projects/:id/ci/lint (committed)";
        }

        return GitLabContent.Wrap(CiLintMapper.ToSummary(result), source);
    }

    [McpServerTool(Name = "gitlab_list_pipeline_bridge_jobs", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the bridge jobs that start downstream pipelines from this one, to trace multi-project pipeline chains.")]
    public async Task<CallToolResult> ListPipelineBridgeJobsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_my_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        [Description("Only bridge jobs in these states, e.g. \"success\", \"failed\", \"running\". Omit for all.")]
        string[]? scope = null,
        [Description("Maximum bridge jobs to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new TriggerJobListOptions { Scope = scope, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<JobSummary> collected = [];
        var truncated = false;

        await foreach (var job in pipelines.ListTriggerJobsAsync(project, pipelineId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(JobMapper.ToSummary(job));
        }

        return GitLabContent.Wrap(new JobListResult(collected, truncated),
            "projects/:id/pipelines/:pipeline_id/trigger_jobs");
    }

    [McpServerTool(Name = "gitlab_list_pipeline_jobs", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the jobs that belong to one pipeline, to see per-stage pass/fail status.")]
    public async Task<CallToolResult> ListPipelineJobsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_my_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        [Description("Maximum jobs to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<JobSummary> collected = [];
        var truncated = false;

        await foreach (var job in jobs.ListForPipelineAsync(project, pipelineId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(JobMapper.ToSummary(job));
        }

        return GitLabContent.Wrap(new JobListResult(collected, truncated), "projects/:id/pipelines/:pipeline_id/jobs");
    }

    [McpServerTool(Name = "gitlab_list_pipelines", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's pipelines, most recently created first, with optional status/ref/source filters, to see recent CI activity.")]
    public async Task<CallToolResult> ListPipelinesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Filter by pipeline status, e.g. \"success\", \"failed\", \"running\", \"canceled\", \"pending\", \"skipped\". Omit for all statuses.")]
        string? status = null,
        [Description("Filter to pipelines on this branch or tag name. Omit for all refs.")]
        string? refName = null,
        [Description(
            "Filter by trigger source, e.g. \"push\", \"web\", \"schedule\", \"api\", \"trigger\", \"merge_request_event\". Omit for all.")]
        string? source = null,
        [Description("Maximum pipelines to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new PipelineListOptions
        {
            Status = status,
            Ref = refName,
            Source = source,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PipelineSummary> collected = [];
        var truncated = false;

        await foreach (var pipeline in pipelines.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PipelineMapper.ToSummary(pipeline));
        }

        return GitLabContent.Wrap(new PipelineListResult(collected, truncated), "projects/:id/pipelines");
    }

    [McpServerTool(Name = "gitlab_get_pipeline", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one pipeline's full status, timing, coverage and metadata by id.")]
    public async Task<CallToolResult> GetPipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await pipelines.GetAsync(project, pipelineId, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/:pipeline_id");
    }

    [McpServerTool(Name = "gitlab_get_latest_pipeline", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets the most recent pipeline for a branch or tag, or for the project's default branch if ref is omitted -- the \"is main green\" check.")]
    public async Task<CallToolResult> GetLatestPipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Branch or tag to check. Omit for the project's default branch.")]
        string? refName = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await pipelines.GetLatestAsync(project, refName, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/latest");
    }

    [McpServerTool(Name = "gitlab_get_pipeline_test_report", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets a pipeline's test results rolled up per suite (pass/fail/skip/error counts and timing). Set includeFailedCases to true to also fetch the full per-test-case report and return a bounded list of the failed/errored cases, for debugging.")]
    public async Task<CallToolResult> GetPipelineTestReportAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        [Description(
            "If true, also fetches the full test report and returns a bounded list of failed/errored test cases. Each case's captured output and stack trace is truncated at 4000 characters (see systemOutputTruncated/stackTraceTruncated on the result). Default false.")]
        bool includeFailedCases = false,
        [Description(
            "Maximum failed/errored test cases to return when includeFailedCases is true (1-100). Default 20. Ignored when includeFailedCases is false.")]
        int maxFailedCases = 20,
        CancellationToken cancellationToken = default)
    {
        if (maxFailedCases is < 1 or > MaxLimit)
            throw new McpException($"maxFailedCases must be between 1 and {MaxLimit}.");

        var summary = await pipelines.GetTestReportSummaryAsync(project, pipelineId, cancellationToken);

        List<TestCaseSummary> failedCases = [];
        var truncated = false;

        if (includeFailedCases)
        {
            var fullReport = await pipelines.GetTestReportAsync(project, pipelineId, cancellationToken);

            foreach (var suite in fullReport.TestSuites ?? [])
            {
                foreach (var testCase in suite.TestCases ?? [])
                {
                    if (!IsFailureStatus(testCase.Status)) continue;

                    if (failedCases.Count == maxFailedCases)
                    {
                        truncated = true;
                        break;
                    }

                    failedCases.Add(TestReportMapper.ToSummary(testCase));
                }

                if (truncated) break;
            }
        }

        var result = new TestReportSummaryResult(
            TestReportMapper.ToSummary(summary.Total),
            summary.TestSuites?.Select(TestReportMapper.ToSummary).ToList() ?? [],
            failedCases,
            truncated);

        return GitLabContent.Wrap(result, "projects/:id/pipelines/:pipeline_id/test_report_summary");
    }

    [McpServerTool(Name = "gitlab_list_pipeline_variables", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the CI/CD variables a specific pipeline actually ran with, for debugging a build. Variable values are never returned by this tool, masked or not.")]
    public async Task<CallToolResult> ListPipelineVariablesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        [Description("Maximum variables to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<VariableSummary> collected = [];
        var truncated = false;

        await foreach (var variable in pipelines.ListVariablesAsync(project, pipelineId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(VariableMapper.ToSummary(variable));
        }

        return GitLabContent.Wrap(new VariableListResult(collected, truncated),
            "projects/:id/pipelines/:pipeline_id/variables");
    }

    [McpServerTool(Name = "gitlab_get_job", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one job's full status, timing and pipeline/commit linkage by id.")]
    public async Task<CallToolResult> GetJobAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id");
    }

    [McpServerTool(Name = "gitlab_get_job_log", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Fetches a job's console log (trace) to diagnose a CI failure without opening the GitLab UI. Returns at most the last maxChars characters of the log -- the tail, where failures usually appear -- and says so when it truncates.")]
    public async Task<CallToolResult> GetJobLogAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        [Description("Maximum characters to return, counted from the end of the log (1-200000). Default 20000.")]
        int maxChars = DefaultJobLogChars,
        CancellationToken cancellationToken = default)
    {
        if (maxChars is < 1 or > MaxJobLogChars)
            throw new McpException($"maxChars must be between 1 and {MaxJobLogChars}.");

        // GitLabFileResponse (not its Content stream alone) is what owns disposal of the body, the
        // underlying HTTP response and the per-operation cancellation source -- see its XML doc.
        await using var file = await jobs.GetTraceAsync(project, jobId, null, cancellationToken);
        using var reader = new StreamReader(file.Content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var truncated = text.Length > maxChars;
        var tail = truncated ? text[^maxChars..] : text;
        var body = truncated
            ? $"[log truncated -- showing the last {maxChars} of {text.Length} characters]{Environment.NewLine}{tail}"
            : tail;

        return GitLabContent.WrapText(body, "projects/:id/jobs/:job_id/trace");
    }

    [McpServerTool(Name = "gitlab_list_pipeline_schedules", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's cron-scheduled pipelines.")]
    public async Task<CallToolResult> ListPipelineSchedulesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum schedules to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new PipelineScheduleListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<PipelineScheduleSummary> collected = [];
        var truncated = false;

        await foreach (var schedule in pipelineSchedules.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PipelineMapper.ToSummary(schedule));
        }

        return GitLabContent.Wrap(new PipelineScheduleListResult(collected, truncated),
            "projects/:id/pipeline_schedules");
    }

    [McpServerTool(Name = "gitlab_get_pipeline_schedule", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one pipeline schedule's cron, ref, owner and last-triggered pipeline.")]
    public async Task<CallToolResult> GetPipelineScheduleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await pipelineSchedules.GetAsync(project, pipelineScheduleId, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(schedule),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id");
    }

    [McpServerTool(Name = "gitlab_get_trigger", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one pipeline trigger token's metadata by id. The token value itself is never returned by this tool -- capture it from gitlab_create_trigger's result, since GitLab only shows it once.")]
    public async Task<CallToolResult> GetTriggerAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The trigger's numeric id (from gitlab_list_triggers).")]
        long triggerId,
        CancellationToken cancellationToken = default)
    {
        var trigger = await triggers.GetAsync(project, triggerId, cancellationToken);
        return GitLabContent.Wrap(TriggerMapper.ToSummary(trigger), "projects/:id/triggers/:trigger_id");
    }

    [McpServerTool(Name = "gitlab_get_pipeline_schedule_variable", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads one CI/CD variable attached to a pipeline schedule, by key -- there is no list-all endpoint for schedule variables, so fetch each key you need individually. The variable's value is never returned by this tool, masked or not.")]
    public async Task<CallToolResult> GetPipelineScheduleVariableAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        CancellationToken cancellationToken = default)
    {
        var variable = await pipelineSchedules.GetVariableAsync(project, pipelineScheduleId, key, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(variable),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id/variables/:key");
    }

    [McpServerTool(Name = "gitlab_get_ci_variable", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads one project or group CI/CD variable by key. The variable's value is never returned by this tool, masked or not.")]
    public async Task<CallToolResult> GetCiVariableAsync(
        [Description("The project or group to read the variable from: numeric id, or URL-encoded \"namespace/path\".")]
        string id,
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        [Description("Which kind of resource id was passed: \"project\" or \"group\". Default \"project\".")]
        string scope = "project",
        [Description(
            "Project scope only: disambiguates a key that exists in several environment scopes. Not valid when scope is \"group\" -- omit for a group variable, or to let GitLab pick whichever project-scoped match comes first.")]
        string? environmentScope = null,
        CancellationToken cancellationToken = default)
    {
        GitLabVariable variable;
        string source;

        if (IsGroupScope(scope, nameof(scope)))
        {
            if (environmentScope is not null)
                throw new McpException("environmentScope is only valid when scope is \"project\".");

            variable = await variables.GetGroupVariableAsync(id, key, cancellationToken);
            source = "groups/:id/variables/:key";
        }
        else
        {
            variable = await variables.GetProjectVariableAsync(id, key, environmentScope, cancellationToken);
            source = "projects/:id/variables/:key";
        }

        return GitLabContent.Wrap(VariableMapper.ToSummary(variable), source);
    }

    [McpServerTool(Name = "gitlab_list_instance_variables", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every instance-wide CI/CD variable. Requires administrator access. Variable values are never returned by this tool, masked or not.")]
    public async Task<CallToolResult> ListInstanceCiVariablesAsync(
        [Description("Maximum variables to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var options = new VariableListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<VariableSummary> collected = [];
        var truncated = false;

        await foreach (var variable in variables.ListInstanceVariablesAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(VariableMapper.ToSummary(variable));
        }

        return GitLabContent.Wrap(new VariableListResult(collected, truncated), "variables");
    }

    [McpServerTool(Name = "gitlab_get_instance_variable", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads one instance-level CI/CD variable by key. Requires administrator access. Instance variables are always scoped to every environment, so the key alone identifies it. The variable's value is never returned by this tool, masked or not.")]
    public async Task<CallToolResult> GetInstanceCiVariableAsync(
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        CancellationToken cancellationToken = default)
    {
        var variable = await variables.GetInstanceVariableAsync(key, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(variable), "variables/:key");
    }

    [McpServerTool(Name = "gitlab_download_job_artifacts", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads a job's whole artifacts archive (base64-encoded zip), either by job id or -- when jobId is omitted -- the latest successful run of a named job on a branch/tag. Refuses archives over 8 MB rather than returning a truncated, unusable partial zip; use gitlab_get_job_artifact_file to fetch a single file out of a larger archive instead.")]
    public async Task<CallToolResult> DownloadJobArtifactsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs). Supply this, or both refName and jobName -- not both forms.")]
        long? jobId = null,
        [Description(
            "Branch or tag to find the latest successful run on. Only valid together with jobName, and only when jobId is omitted.")]
        string? refName = null,
        [Description(
            "The CI job's name, e.g. \"test\" or \"build:linux\". Only valid together with refName, and only when jobId is omitted.")]
        string? jobName = null,
        [Description(
            "Restricts the download to one report type instead of the whole archive: \"accessibility\", \"api_fuzzing\", \"archive\", \"cobertura\", \"jacoco\", \"codequality\", \"container_scanning\", \"dast\", \"dependency_scanning\", \"dotenv\", \"junit\", or \"license_scanning\". Only valid together with jobId. Omit for the whole archive.")]
        string? fileType = null,
        [Description(
            "When downloading by refName/jobName, also search recent successful pipelines instead of only the latest one for a run that has this job. Only valid together with refName/jobName. Default false.")]
        bool searchRecentSuccessfulPipelines = false,
        CancellationToken cancellationToken = default)
    {
        GitLabFileResponse file;
        string source;

        if (jobId is { } id)
        {
            if (refName is not null || jobName is not null)
                throw new McpException("Supply either jobId, or refName and jobName -- not both.");

            if (searchRecentSuccessfulPipelines)
                throw new McpException(
                    "searchRecentSuccessfulPipelines only applies when downloading by refName and jobName.");

            var options = fileType is null
                ? (JobArtifactDownloadOptions?)null
                : new JobArtifactDownloadOptions { FileType = ParseArtifactFileType(fileType) };

            file = await jobArtifacts.DownloadAsync(project, id, options, cancellationToken);
            source = "projects/:id/jobs/:job_id/artifacts";
        }
        else if (refName is not null && jobName is not null)
        {
            if (fileType is not null) throw new McpException("fileType only applies when downloading by jobId.");

            var options = new JobArtifactRefDownloadOptions
                { SearchRecentSuccessfulPipelines = searchRecentSuccessfulPipelines };
            file = await jobArtifacts.DownloadForRefAsync(project, refName, jobName, options, cancellationToken);
            source = "projects/:id/jobs/artifacts/:ref_name/download";
        }
        else
        {
            throw new McpException("Supply either jobId, or both refName and jobName.");
        }

        await using var _ = file;

        if (file.ContentLength is { } declaredLength && declaredLength > MaxArtifactArchiveBytes)
            throw new McpException(
                $"Artifacts archive is {declaredLength} bytes, over the {MaxArtifactArchiveBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var buffer = new byte[MaxArtifactArchiveBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxArtifactArchiveBytes)
            throw new McpException(
                $"Artifacts archive exceeds the {MaxArtifactArchiveBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var payload = new JobArtifactArchiveDownload(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, source);
    }

    [McpServerTool(Name = "gitlab_get_job_artifact_file", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads one file out of a job's artifacts archive (base64-encoded), without fetching the whole archive -- e.g. a single coverage report. Discover the exact path first with gitlab_list_job_artifacts. Either supply jobId, or -- when jobId is omitted -- refName and jobName to read from the latest successful run of that job. Refuses files over 4 MB.")]
    public async Task<CallToolResult> GetJobArtifactFileAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The path to the file inside the artifacts archive, e.g. \"coverage/index.html\" (from gitlab_list_job_artifacts).")]
        string artifactPath,
        [Description(
            "The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs). Supply this, or both refName and jobName -- not both forms.")]
        long? jobId = null,
        [Description(
            "Branch or tag to find the latest successful run on. Only valid together with jobName, and only when jobId is omitted.")]
        string? refName = null,
        [Description(
            "The CI job's name, e.g. \"test\" or \"build:linux\". Only valid together with refName, and only when jobId is omitted.")]
        string? jobName = null,
        [Description(
            "When downloading by refName/jobName, also search recent successful pipelines instead of only the latest one for a run that has this job. Only valid together with refName/jobName. Default false.")]
        bool searchRecentSuccessfulPipelines = false,
        CancellationToken cancellationToken = default)
    {
        GitLabFileResponse file;
        string source;

        if (jobId is { } id)
        {
            if (refName is not null || jobName is not null)
                throw new McpException("Supply either jobId, or refName and jobName -- not both.");

            if (searchRecentSuccessfulPipelines)
                throw new McpException(
                    "searchRecentSuccessfulPipelines only applies when downloading by refName and jobName.");

            file = await jobArtifacts.DownloadFileAsync(project, id, artifactPath,
                cancellationToken: cancellationToken);
            source = "projects/:id/jobs/:job_id/artifacts/:artifact_path";
        }
        else if (refName is not null && jobName is not null)
        {
            var options = new JobArtifactRefDownloadOptions
                { SearchRecentSuccessfulPipelines = searchRecentSuccessfulPipelines };
            file = await jobArtifacts.DownloadFileForRefAsync(project, refName, artifactPath, jobName, options,
                cancellationToken);
            source = "projects/:id/jobs/artifacts/:ref_name/raw/:artifact_path";
        }
        else
        {
            throw new McpException("Supply either jobId, or both refName and jobName.");
        }

        await using var _ = file;

        if (file.ContentLength is { } declaredLength && declaredLength > MaxArtifactFileBytes)
            throw new McpException(
                $"Artifact file is {declaredLength} bytes, over the {MaxArtifactFileBytes}-byte limit this tool can return. Download the whole archive with gitlab_download_job_artifacts instead, or fetch it directly from GitLab.");

        var buffer = new byte[MaxArtifactFileBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxArtifactFileBytes)
            throw new McpException(
                $"Artifact file exceeds the {MaxArtifactFileBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var payload = new JobArtifactFileDownload(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, source);
    }

    [McpServerTool(Name = "gitlab_get_job_token_scope", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Checks whether a project restricts which other projects or groups can authenticate against it using its own CI/CD job token, and whether it restricts what a job token minted here can reach in other projects.")]
    public async Task<JobTokenScopeSummary> GetJobTokenScopeAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var scope = await jobTokenScope.GetAsync(project, cancellationToken);
        return new JobTokenScopeSummary(scope.InboundEnabled, scope.OutboundEnabled);
    }

    // ---------------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_pipeline_schedule", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new cron-triggered pipeline schedule for a ref.")]
    public async Task<CallToolResult> CreatePipelineScheduleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("A short description shown in the GitLab UI's schedule list.")]
        string description,
        [Description("The branch or tag this schedule runs pipelines against.")]
        string refName,
        [Description("A standard 5-field cron expression, e.g. \"0 4 * * *\" for daily at 04:00.")]
        string cron,
        [Description(
            "IANA timezone name the cron expression is evaluated in, e.g. \"America/New_York\". Omit for UTC.")]
        string? cronTimezone = null,
        [Description("If false, the schedule is created disabled and will not fire until activated. Default true.")]
        bool active = true,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePipelineScheduleRequest
        {
            Description = description,
            Ref = refName,
            Cron = cron,
            CronTimezone = cronTimezone,
            Active = active
        };

        var schedule = await pipelineSchedules.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(schedule), "projects/:id/pipeline_schedules (create)");
    }

    [McpServerTool(Name = "gitlab_create_ci_variable", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new CI/CD variable on a project or a group. The variable's value is accepted here but never echoed back in the result.")]
    public async Task<CallToolResult> CreateCiVariableAsync(
        [Description("The project or group to create the variable on: numeric id, or URL-encoded \"namespace/path\".")]
        string id,
        [Description("The variable's name, e.g. \"DEPLOY_KEY\". Must be uppercase letters, digits and underscores.")]
        string key,
        [Description("The variable's value.")] string value,
        [Description("Which kind of resource id was passed: \"project\" or \"group\". Default \"project\".")]
        string scope = "project",
        [Description(
            "\"env_var\" (a plain environment variable, the default) or \"file\" (written to a temp file, with the variable holding its path).")]
        string variableType = "env_var",
        [Description("If true, only exposed to pipelines running on protected branches or tags. Default false.")]
        bool @protected = false,
        [Description(
            "If true, GitLab masks the value in job logs. The value must meet GitLab's masking character/format rules or the create call fails. Default false.")]
        bool masked = false,
        [Description(
            "If true, also hides the value from the CI/CD settings UI after creation (implies masked). Default false.")]
        bool maskedAndHidden = false,
        [Description(
            "If true, GitLab does not expand $VARIABLE/${VARIABLE} references inside this value. Default false.")]
        bool raw = false,
        [Description(
            "Limits which deployment environments can use this variable, e.g. \"production\" or a wildcard like \"review/*\". Default \"*\" (all environments).")]
        string? environmentScope = null,
        [Description("Optional free-text note about what this variable is for.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = variableType.ToLowerInvariant() switch
        {
            "env_var" => "env_var",
            "file" => "file",
            _ => throw new McpException($"variableType must be \"env_var\" or \"file\", got \"{variableType}\".")
        };

        var request = new CreateVariableRequest
        {
            Key = key,
            Value = value,
            VariableType = normalizedType,
            Protected = @protected,
            Masked = masked,
            MaskedAndHidden = maskedAndHidden,
            Raw = raw,
            EnvironmentScope = environmentScope,
            Description = description
        };

        GitLabVariable created;
        string source;

        if (IsGroupScope(scope, nameof(scope)))
        {
            created = await variables.CreateGroupVariableAsync(id, request, cancellationToken);
            source = "groups/:id/variables (create)";
        }
        else
        {
            created = await variables.CreateProjectVariableAsync(id, request, cancellationToken);
            source = "projects/:id/variables (create)";
        }

        return GitLabContent.Wrap(VariableMapper.ToSummary(created), source);
    }

    [McpServerTool(Name = "gitlab_create_pipeline", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Triggers a new pipeline run for a branch, tag or commit ref.")]
    public async Task<CallToolResult> CreatePipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The branch, tag or commit SHA to run the pipeline against.")]
        string refName,
        CancellationToken cancellationToken)
    {
        var request = new CreatePipelineRequest { Ref = refName };
        var pipeline = await pipelines.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipeline (create)");
    }

    [McpServerTool(Name = "gitlab_cancel_job", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Cancels a running or pending job.")]
    public async Task<CallToolResult> CancelJobAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.CancelAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id/cancel");
    }

    [McpServerTool(Name = "gitlab_create_trigger", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new pipeline trigger token. Its value is shown in full only in this call's result -- capture it now, GitLab will not display it again.")]
    public async Task<CallToolResult> CreateTriggerAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("A short description of what this trigger is used for.")]
        string description,
        [Description("Optional expiry, ISO 8601 date-time. Omit for a trigger that never expires.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateTriggerRequest
        {
            Description = description,
            ExpiresAt = ParseOptionalDate(expiresAt, nameof(expiresAt))
        };

        var trigger = await triggers.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(TriggerMapper.ToCreateResult(trigger), "projects/:id/triggers (create)");
    }

    [McpServerTool(Name = "gitlab_delete_all_project_artifacts", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Deletes the artifacts of every job in a project. Requires the Maintainer role. GitLab processes the deletion in the background, so artifacts may still exist briefly after this returns. This cannot be undone.")]
    public async Task<DeleteAllArtifactsResult> DeleteAllProjectArtifactsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken)
    {
        await jobArtifacts.DeleteAllAsync(project, cancellationToken);
        return new DeleteAllArtifactsResult(true);
    }

    [McpServerTool(Name = "gitlab_cancel_pipeline", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description("Cancels every running or pending job in a pipeline. Already-finished jobs are left as they are.")]
    public async Task<CallToolResult> CancelPipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await pipelines.CancelAsync(project, pipelineId, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/:pipeline_id/cancel");
    }

    [McpServerTool(Name = "gitlab_retry_pipeline", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Retries a pipeline's failed and canceled jobs without re-running the ones that already succeeded.")]
    public async Task<CallToolResult> RetryPipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines or gitlab_create_pipeline).")]
        long pipelineId,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await pipelines.RetryAsync(project, pipelineId, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/:pipeline_id/retry");
    }

    [McpServerTool(Name = "gitlab_delete_pipeline", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes a pipeline and all of its jobs, logs, artifacts and triggers. Requires the Owner role on the project. This cannot be undone.")]
    public async Task<PipelineDeleteResult> DeletePipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines).")]
        long pipelineId,
        CancellationToken cancellationToken)
    {
        await pipelines.DeleteAsync(project, pipelineId, cancellationToken);
        return new PipelineDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_rename_pipeline", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Sets a pipeline's display name, shown in the GitLab UI in place of its id.")]
    public async Task<CallToolResult> RenamePipelineAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline's numeric id (from gitlab_list_pipelines).")]
        long pipelineId,
        [Description("The new display name for the pipeline.")]
        string name,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePipelineMetadataRequest { Name = name };
        var pipeline = await pipelines.UpdateMetadataAsync(project, pipelineId, request, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), "projects/:id/pipelines/:pipeline_id/metadata");
    }

    [McpServerTool(Name = "gitlab_retry_job", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Re-runs a finished (failed or canceled) job as a new job.")]
    public async Task<CallToolResult> RetryJobAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.RetryAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id/retry");
    }

    [McpServerTool(Name = "gitlab_play_job", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Starts a manual job that is waiting to be triggered.")]
    public async Task<CallToolResult> PlayJobAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.PlayAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id/play");
    }

    [McpServerTool(Name = "gitlab_erase_job", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Irreversibly erases a job's log and artifacts -- e.g. to remove a secret that leaked into a build log. This cannot be undone.")]
    public async Task<CallToolResult> EraseJobAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.EraseAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id/erase");
    }

    [McpServerTool(Name = "gitlab_update_pipeline_schedule", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a pipeline schedule's description, ref, cron expression, timezone or active flag. Only the parameters you supply are changed -- omit the rest to leave them as they are.")]
    public async Task<CallToolResult> UpdatePipelineScheduleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        [Description("New description. Omit to leave unchanged.")]
        string? description = null,
        [Description("New branch or tag this schedule runs pipelines against. Omit to leave unchanged.")]
        string? refName = null,
        [Description("New standard 5-field cron expression, e.g. \"0 4 * * *\". Omit to leave unchanged.")]
        string? cron = null,
        [Description("New IANA timezone name, e.g. \"America/New_York\". Omit to leave unchanged.")]
        string? cronTimezone = null,
        [Description("Set true to activate or false to deactivate the schedule. Omit to leave unchanged.")]
        bool? active = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePipelineScheduleRequest
        {
            Description = description,
            Ref = refName,
            Cron = cron,
            CronTimezone = cronTimezone,
            Active = active
        };

        var schedule = await pipelineSchedules.UpdateAsync(project, pipelineScheduleId, request, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(schedule),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_pipeline_schedule", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a pipeline schedule. This cannot be undone.")]
    public async Task<PipelineScheduleDeleteResult> DeletePipelineScheduleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules).")]
        long pipelineScheduleId,
        CancellationToken cancellationToken)
    {
        await pipelineSchedules.DeleteAsync(project, pipelineScheduleId, cancellationToken);
        return new PipelineScheduleDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_run_pipeline_schedule", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Triggers a scheduled pipeline immediately, out of band of its cron. GitLab acknowledges with a bare message rather than the pipeline it created -- call gitlab_list_pipeline_schedule_pipelines afterwards to find the resulting run.")]
    public async Task<PipelineScheduleRunResult> RunPipelineScheduleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        CancellationToken cancellationToken)
    {
        await pipelineSchedules.PlayAsync(project, pipelineScheduleId, cancellationToken);
        return new PipelineScheduleRunResult(true);
    }

    [McpServerTool(Name = "gitlab_take_pipeline_schedule_ownership", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Makes the calling user the owner of a pipeline schedule, so future scheduled runs use that user's permissions instead of the previous owner's.")]
    public async Task<CallToolResult> TakePipelineScheduleOwnershipAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await pipelineSchedules.TakeOwnershipAsync(project, pipelineScheduleId, cancellationToken);
        return GitLabContent.Wrap(PipelineMapper.ToSummary(schedule),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id/take_ownership");
    }

    [McpServerTool(Name = "gitlab_create_pipeline_schedule_variable", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Adds a CI/CD variable visible only to pipelines this schedule triggers.")]
    public async Task<CallToolResult> CreatePipelineScheduleVariableAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        [Description("The variable's name, e.g. \"DEPLOY_KEY\". Must be uppercase letters, digits and underscores.")]
        string key,
        [Description("The variable's value.")] string value,
        [Description(
            "\"env_var\" (a plain environment variable, the default) or \"file\" (written to a temp file, with the variable holding its path).")]
        string variableType = "env_var",
        CancellationToken cancellationToken = default)
    {
        var normalizedType = variableType.ToLowerInvariant() switch
        {
            "env_var" => "env_var",
            "file" => "file",
            _ => throw new McpException($"variableType must be \"env_var\" or \"file\", got \"{variableType}\".")
        };

        var request = new CreatePipelineScheduleVariableRequest
            { Key = key, Value = value, VariableType = normalizedType };
        var created =
            await pipelineSchedules.CreateVariableAsync(project, pipelineScheduleId, request, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(created),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id/variables (create)");
    }

    [McpServerTool(Name = "gitlab_update_pipeline_schedule_variable", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes a pipeline schedule variable's value or type. The key itself is immutable -- to rename a variable, delete it and create a new one. Only the parameters you supply are changed -- omit the rest to leave them as they are.")]
    public async Task<CallToolResult> UpdatePipelineScheduleVariableAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        [Description("The variable's key to update.")]
        string key,
        [Description("New value. Omit to leave unchanged.")]
        string? value = null,
        [Description("New type: \"env_var\" or \"file\". Omit to leave unchanged.")]
        string? variableType = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePipelineScheduleVariableRequest
        {
            Value = value,
            VariableType = NormalizeOptionalVariableType(variableType)
        };

        var updated =
            await pipelineSchedules.UpdateVariableAsync(project, pipelineScheduleId, key, request, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(updated),
            "projects/:id/pipeline_schedules/:pipeline_schedule_id/variables/:key (update)");
    }

    [McpServerTool(Name = "gitlab_delete_pipeline_schedule_variable", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Removes a variable from a pipeline schedule. This cannot be undone.")]
    public async Task<VariableDeleteResult> DeletePipelineScheduleVariableAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline schedule's numeric id (from gitlab_list_pipeline_schedules or gitlab_create_pipeline_schedule).")]
        long pipelineScheduleId,
        [Description("The variable's key to remove.")]
        string key,
        CancellationToken cancellationToken)
    {
        await pipelineSchedules.DeleteVariableAsync(project, pipelineScheduleId, key, cancellationToken);
        return new VariableDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_update_trigger", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Renames a pipeline trigger token's description. The token value itself cannot be changed.")]
    public async Task<CallToolResult> UpdateTriggerAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The trigger's numeric id (from gitlab_list_triggers).")]
        long triggerId,
        [Description("The trigger's new description.")]
        string description,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateTriggerRequest { Description = description };
        var trigger = await triggers.UpdateAsync(project, triggerId, request, cancellationToken);
        return GitLabContent.Wrap(TriggerMapper.ToSummary(trigger), "projects/:id/triggers/:trigger_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_trigger", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Revokes a pipeline trigger token. Any CI/CD process still using it will stop being able to authenticate. This cannot be undone.")]
    public async Task<TriggerDeleteResult> DeleteTriggerAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The trigger's numeric id (from gitlab_list_triggers).")]
        long triggerId,
        CancellationToken cancellationToken)
    {
        await triggers.DeleteAsync(project, triggerId, cancellationToken);
        return new TriggerDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_trigger_pipeline", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Fires a new pipeline run using a pipeline trigger token or a CI job token, rather than the server's own configured credential. Provide refName to run on a specific branch or tag; omit it to run on the project's default branch.")]
    public async Task<CallToolResult> FirePipelineTriggerAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The pipeline trigger token or CI job token to authenticate this run with -- a caller-supplied credential, not one the server stores or has of its own.")]
        string token,
        [Description("Branch or tag to run the pipeline on. Omit to use the project's default branch.")]
        string? refName = null,
        [Description(
            "Extra CI/CD variables for this run, as \"KEY=value\" pairs separated by commas, e.g. \"ENV=staging,VERBOSE=true\". Omit for none.")]
        string? variableAssignments = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TriggerPipelineRequest { Token = token, Variables = ParseVariablePairs(variableAssignments) };

        GitLabPipeline pipeline;
        string source;

        if (refName is null)
        {
            pipeline = await triggers.TriggerPipelineAsync(project, request, cancellationToken);
            source = "projects/:id/trigger/pipeline";
        }
        else
        {
            pipeline = await triggers.TriggerPipelineForRefAsync(project, refName, request, cancellationToken);
            source = "projects/:id/ref/:ref/trigger/pipeline";
        }

        return GitLabContent.Wrap(PipelineMapper.ToSummary(pipeline), source);
    }

    [McpServerTool(Name = "gitlab_update_ci_variable", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes an existing project or group CI/CD variable's value or flags. Only the parameters you supply are changed -- omit the rest to leave them as they are. The variable's value is accepted here but never echoed back in the result.")]
    public async Task<CallToolResult> UpdateCiVariableAsync(
        [Description("The project or group the variable belongs to: numeric id, or URL-encoded \"namespace/path\".")]
        string id,
        [Description(
            "The variable's key, e.g. \"DEPLOY_KEY\". Immutable -- to rename a variable, delete it and create a new one.")]
        string key,
        [Description("Which kind of resource id was passed: \"project\" or \"group\". Default \"project\".")]
        string scope = "project",
        [Description("New value. Omit to leave unchanged.")]
        string? value = null,
        [Description("New type: \"env_var\" or \"file\". Omit to leave unchanged.")]
        string? variableType = null,
        [Description(
            "If true, only exposed to pipelines running on protected branches or tags. Omit to leave unchanged.")]
        bool? @protected = null,
        [Description(
            "If true, GitLab masks the value in job logs; it must meet GitLab's masking rules or the update fails. Omit to leave unchanged.")]
        bool? masked = null,
        [Description(
            "If true, GitLab does not expand $VARIABLE/${VARIABLE} references inside this value. Omit to leave unchanged.")]
        bool? raw = null,
        [Description(
            "New environment scope, e.g. \"production\" or a wildcard like \"review/*\". Omit to leave unchanged.")]
        string? environmentScope = null,
        [Description("New free-text note about what this variable is for. Omit to leave unchanged.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateVariableRequest
        {
            Value = value,
            VariableType = NormalizeOptionalVariableType(variableType),
            Protected = @protected,
            Masked = masked,
            Raw = raw,
            EnvironmentScope = environmentScope,
            Description = description
        };

        GitLabVariable updated;
        string source;

        if (IsGroupScope(scope, nameof(scope)))
        {
            updated = await variables.UpdateGroupVariableAsync(id, key, request, cancellationToken);
            source = "groups/:id/variables/:key (update)";
        }
        else
        {
            updated = await variables.UpdateProjectVariableAsync(id, key, request, cancellationToken);
            source = "projects/:id/variables/:key (update)";
        }

        return GitLabContent.Wrap(VariableMapper.ToSummary(updated), source);
    }

    [McpServerTool(Name = "gitlab_delete_ci_variable", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a project or group CI/CD variable. This cannot be undone.")]
    public async Task<VariableDeleteResult> DeleteCiVariableAsync(
        [Description("The project or group the variable belongs to: numeric id, or URL-encoded \"namespace/path\".")]
        string id,
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        [Description("Which kind of resource id was passed: \"project\" or \"group\". Default \"project\".")]
        string scope = "project",
        [Description(
            "Project scope only: targets a specific scope of a key that exists more than once. Not valid when scope is \"group\".")]
        string? environmentScope = null,
        CancellationToken cancellationToken = default)
    {
        if (IsGroupScope(scope, nameof(scope)))
        {
            if (environmentScope is not null)
                throw new McpException("environmentScope is only valid when scope is \"project\".");

            await variables.DeleteGroupVariableAsync(id, key, cancellationToken);
        }
        else
        {
            await variables.DeleteProjectVariableAsync(id, key, environmentScope, cancellationToken);
        }

        return new VariableDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_create_instance_variable", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates an instance-wide CI/CD variable available to every project's pipelines. Requires administrator access. GitLab ignores environment scoping and the \"masked and hidden\" option at instance scope -- use gitlab_create_ci_variable on a specific project or group for those. The variable's value is accepted here but never echoed back in the result.")]
    public async Task<CallToolResult> CreateInstanceCiVariableAsync(
        [Description("The variable's name, e.g. \"DEPLOY_KEY\". Must be uppercase letters, digits and underscores.")]
        string key,
        [Description("The variable's value.")] string value,
        [Description(
            "\"env_var\" (a plain environment variable, the default) or \"file\" (written to a temp file, with the variable holding its path).")]
        string variableType = "env_var",
        [Description("If true, only exposed to pipelines running on protected branches or tags. Default false.")]
        bool @protected = false,
        [Description(
            "If true, GitLab masks the value in job logs. The value must meet GitLab's masking character/format rules or the create call fails. Default false.")]
        bool masked = false,
        [Description(
            "If true, GitLab does not expand $VARIABLE/${VARIABLE} references inside this value. Default false.")]
        bool raw = false,
        [Description("Optional free-text note about what this variable is for.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = variableType.ToLowerInvariant() switch
        {
            "env_var" => "env_var",
            "file" => "file",
            _ => throw new McpException($"variableType must be \"env_var\" or \"file\", got \"{variableType}\".")
        };

        var request = new CreateVariableRequest
        {
            Key = key,
            Value = value,
            VariableType = normalizedType,
            Protected = @protected,
            Masked = masked,
            Raw = raw,
            Description = description
        };

        var created = await variables.CreateInstanceVariableAsync(request, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(created), "variables (create)");
    }

    [McpServerTool(Name = "gitlab_update_instance_variable", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes an instance-level CI/CD variable's value or flags. Requires administrator access. Only the parameters you supply are changed -- omit the rest to leave them as they are. GitLab ignores environment scoping at instance scope. The variable's value is accepted here but never echoed back in the result.")]
    public async Task<CallToolResult> UpdateInstanceCiVariableAsync(
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        [Description("New value. Omit to leave unchanged.")]
        string? value = null,
        [Description("New type: \"env_var\" or \"file\". Omit to leave unchanged.")]
        string? variableType = null,
        [Description(
            "If true, only exposed to pipelines running on protected branches or tags. Omit to leave unchanged.")]
        bool? @protected = null,
        [Description("If true, GitLab masks the value in job logs. Omit to leave unchanged.")]
        bool? masked = null,
        [Description(
            "If true, GitLab does not expand $VARIABLE/${VARIABLE} references inside this value. Omit to leave unchanged.")]
        bool? raw = null,
        [Description("New free-text note about what this variable is for. Omit to leave unchanged.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateVariableRequest
        {
            Value = value,
            VariableType = NormalizeOptionalVariableType(variableType),
            Protected = @protected,
            Masked = masked,
            Raw = raw,
            Description = description
        };

        var updated = await variables.UpdateInstanceVariableAsync(key, request, cancellationToken);
        return GitLabContent.Wrap(VariableMapper.ToSummary(updated), "variables/:key (update)");
    }

    [McpServerTool(Name = "gitlab_delete_instance_variable", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes an instance-level CI/CD variable. Requires administrator access. This cannot be undone.")]
    public async Task<VariableDeleteResult> DeleteInstanceCiVariableAsync(
        [Description("The variable's key, e.g. \"DEPLOY_KEY\".")]
        string key,
        CancellationToken cancellationToken)
    {
        await variables.DeleteInstanceVariableAsync(key, cancellationToken);
        return new VariableDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_keep_job_artifacts", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Exempts a job's artifacts from GitLab's automatic expiry, so they are kept indefinitely instead of being deleted once their expire_at date passes.")]
    public async Task<CallToolResult> KeepJobArtifactsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobArtifacts.KeepAsync(project, jobId, cancellationToken);
        return GitLabContent.Wrap(JobMapper.ToSummary(job), "projects/:id/jobs/:job_id/artifacts/keep");
    }

    [McpServerTool(Name = "gitlab_delete_job_artifacts", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes one job's artifacts immediately, without waiting for their expiry. This cannot be undone.")]
    public async Task<JobArtifactsDeleteResult> DeleteJobArtifactsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The job's numeric id (from gitlab_list_jobs or gitlab_list_pipeline_jobs).")]
        long jobId,
        CancellationToken cancellationToken)
    {
        await jobArtifacts.DeleteAsync(project, jobId, cancellationToken);
        return new JobArtifactsDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_publish_ci_catalog_version", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Publishes a new version of a CI/CD component project to the CI/CD Catalog. The project must already be marked as a Catalog resource in GitLab. Intended for CLI/CI tooling -- GitLab documents this endpoint as normally called from within the release pipeline itself, authenticated with that job's own CI job token, rather than interactively.")]
    public async Task<CallToolResult> PublishCiCatalogVersionAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Optional raw JSON object describing this release's metadata (inputs, spec version, and other component-schema fields). Must be a JSON object if supplied. Omit to publish with no explicit metadata.")]
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        JsonElement? metadata = null;

        if (metadataJson is not null)
            try
            {
                using var document = JsonDocument.Parse(metadataJson);
                metadata = document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new McpException($"metadataJson is not valid JSON: {ex.Message}");
            }

        var request = new CiCatalogPublishRequest { Metadata = metadata };
        var result = await ciCatalog.PublishAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(CiCatalogMapper.ToSummary(result), "projects/:id/catalog/publish");
    }

    [McpServerTool(Name = "gitlab_set_job_token_scope", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Turns a project's CI/CD job token access restriction on or off. Once enabled, only the project itself plus the projects and groups added via gitlab_add_job_token_allowlist_entry can authenticate against this project using a CI/CD job token minted elsewhere.")]
    public async Task<JobTokenScopeUpdateResult> SetJobTokenScopeAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "True to restrict which other projects/groups can authenticate against this one with a job token; false to allow any project's job token to authenticate against it.")]
        bool enabled,
        CancellationToken cancellationToken)
    {
        await jobTokenScope.UpdateAsync(project, new UpdateProjectJobTokenScopeRequest { Enabled = enabled },
            cancellationToken);
        return new JobTokenScopeUpdateResult(enabled);
    }

    [McpServerTool(Name = "gitlab_add_job_token_allowlist_entry", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Grants another project or group permission to authenticate against this project using its CI/CD job token, by adding it to the allowlist. Has no effect unless gitlab_set_job_token_scope has enabled the restriction.")]
    public async Task<CallToolResult> AddJobTokenAllowlistEntryAsync(
        [Description("Project whose job-token allowlist to add to: numeric id or URL-encoded \"namespace/path\".")]
        string project,
        [Description("The numeric id of the project or group to grant access to.")]
        long targetId,
        [Description(
            "Which allowlist to add to: \"project\" (other projects allowed in) or \"group\" (groups allowed in). Default \"project\".")]
        string kind = "project",
        CancellationToken cancellationToken = default)
    {
        AllowlistEntrySummary entry;
        string source;

        if (IsGroupScope(kind, nameof(kind)))
        {
            var group = await jobTokenScope.AddGroupToAllowlistAsync(project,
                new AddGroupToJobTokenAllowlistRequest { TargetGroupId = targetId }, cancellationToken);
            entry = JobTokenScopeMapper.ToSummary(group);
            source = "projects/:id/job_token_scope/groups_allowlist (add)";
        }
        else
        {
            var addedProject = await jobTokenScope.AddToAllowlistAsync(project,
                new AddProjectToJobTokenAllowlistRequest { TargetProjectId = targetId }, cancellationToken);
            entry = JobTokenScopeMapper.ToSummary(addedProject);
            source = "projects/:id/job_token_scope/allowlist (add)";
        }

        return GitLabContent.Wrap(entry, source);
    }

    [McpServerTool(Name = "gitlab_remove_job_token_allowlist_entry", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Revokes a project or group's permission to authenticate against this project using its CI/CD job token, by removing it from the allowlist. This cannot be undone (the entry can always be added again).")]
    public async Task<AllowlistEntryDeleteResult> RemoveJobTokenAllowlistEntryAsync(
        [Description("Project whose job-token allowlist to remove from: numeric id or URL-encoded \"namespace/path\".")]
        string project,
        [Description("The numeric id of the project or group to revoke access from.")]
        long targetId,
        [Description("Which allowlist to remove from: \"project\" (default) or \"group\".")]
        string kind = "project",
        CancellationToken cancellationToken = default)
    {
        if (IsGroupScope(kind, nameof(kind)))
            await jobTokenScope.RemoveGroupFromAllowlistAsync(project, targetId, cancellationToken);
        else
            await jobTokenScope.RemoveFromAllowlistAsync(project, targetId, cancellationToken);

        return new AllowlistEntryDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    ///     Validates a "fileType" string against <see cref="GitLabJobArtifactFileType" />'s wire (
    ///     <c>JsonStringEnumMemberName</c>) values.
    /// </summary>
    private static GitLabJobArtifactFileType ParseArtifactFileType(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            "accessibility" => GitLabJobArtifactFileType.Accessibility,
            "api_fuzzing" => GitLabJobArtifactFileType.ApiFuzzing,
            "archive" => GitLabJobArtifactFileType.Archive,
            "cobertura" => GitLabJobArtifactFileType.Cobertura,
            "jacoco" => GitLabJobArtifactFileType.Jacoco,
            "codequality" => GitLabJobArtifactFileType.CodeQuality,
            "container_scanning" => GitLabJobArtifactFileType.ContainerScanning,
            "dast" => GitLabJobArtifactFileType.Dast,
            "dependency_scanning" => GitLabJobArtifactFileType.DependencyScanning,
            "dotenv" => GitLabJobArtifactFileType.Dotenv,
            "junit" => GitLabJobArtifactFileType.JUnit,
            "license_scanning" => GitLabJobArtifactFileType.LicenseScanning,
            _ => throw new McpException(
                $"fileType must be one of: accessibility, api_fuzzing, archive, cobertura, jacoco, codequality, container_scanning, dast, dependency_scanning, dotenv, junit, license_scanning. Got \"{fileType}\".")
        };
    }

    private static bool IsFailureStatus(string? status)
    {
        return string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "error", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");
    }

    /// <summary>
    ///     True for "group", false for "project" (both case-insensitive); throws for anything else instead of silently
    ///     misrouting a typo like "groups" to "project".
    /// </summary>
    private static bool IsGroupScope(string scope, string paramName)
    {
        if (string.Equals(scope, "group", StringComparison.OrdinalIgnoreCase)) return true;

        if (string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase)) return false;

        throw new McpException($"{paramName} must be \"project\" or \"group\", got \"{scope}\".");
    }

    /// <summary>Validates an optional "env_var"/"file" variable type for an update call; null means "leave unchanged".</summary>
    private static string? NormalizeOptionalVariableType(string? variableType)
    {
        return variableType?.ToLowerInvariant() switch
        {
            "env_var" => "env_var",
            "file" => "file",
            null => null,
            _ => throw new McpException($"variableType must be \"env_var\" or \"file\", got \"{variableType}\".")
        };
    }

    /// <summary>
    ///     Parses "KEY=value,KEY2=value2" into a dictionary for a pipeline trigger's extra variables. Null/blank input
    ///     yields null (no extra variables).
    /// </summary>
    private static IReadOnlyDictionary<string, string>? ParseVariablePairs(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length != 2)
                throw new McpException($"variableAssignments entry \"{pair}\" must be \"KEY=value\".");

            result[parts[0]] = parts[1];
        }

        return result;
    }

    private static DateTimeOffset? ParseOptionalDate(string? value, string paramName)
    {
        if (value is null) return null;

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var parsed)) throw new McpException($"{paramName} must be a valid ISO 8601 date-time.");

        return parsed;
    }
}