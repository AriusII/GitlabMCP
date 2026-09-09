using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Pipelines/jobs/variables/triggers (CLAUDE.md's "cicd" domain). Developer + DevOps share the
    // read-mostly pipeline/job surface (Grant.Delivery); provisioning CI/CD variables, triggers,
    // schedules and the job-token allowlist, plus the wholesale artifact wipe, are DevOps-only.
    private static IReadOnlyDictionary<string, ToolGrant> CicdRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_my_pipelines"] = new(Grant.Delivery, true),
            ["gitlab_list_jobs"] = new(Grant.Delivery, true),
            ["gitlab_list_pipeline_schedule_pipelines"] = new(Grant.DevOps, true),
            ["gitlab_list_ci_variables"] = new(Grant.DevOps, true),
            ["gitlab_list_job_artifacts"] = new(Grant.Delivery, true),
            ["gitlab_list_triggers"] = new(Grant.DevOps, true),
            ["gitlab_list_job_token_allowlist"] = new(Grant.DevOps, true),
            ["gitlab_validate_ci_config"] = new(Grant.Delivery, true),
            ["gitlab_list_pipeline_bridge_jobs"] = new(Grant.Delivery, true),
            ["gitlab_list_pipeline_jobs"] = new(Grant.Delivery, true),
            ["gitlab_create_pipeline_schedule"] = new(Grant.DevOps, false),
            ["gitlab_create_ci_variable"] = new(Grant.DevOps, false),
            ["gitlab_create_pipeline"] = new(Grant.Delivery, false),
            ["gitlab_cancel_job"] = new(Grant.Delivery, false),
            ["gitlab_create_trigger"] = new(Grant.DevOps, false),
            ["gitlab_delete_all_project_artifacts"] = new(Grant.DevOps, false),

            // Backlog wave, part 1: pipeline/job lifecycle reads and writes (Delivery), plus the
            // pipeline-schedule CRUD tail and the destructive job/pipeline operations (DevOps-only).
            ["gitlab_list_pipelines"] = new(Grant.Delivery, true),
            ["gitlab_get_pipeline"] = new(Grant.Delivery, true),
            ["gitlab_cancel_pipeline"] = new(Grant.Delivery, false),
            ["gitlab_retry_pipeline"] = new(Grant.Delivery, false),
            ["gitlab_delete_pipeline"] = new(Grant.DevOps, false),
            ["gitlab_get_latest_pipeline"] = new(Grant.Delivery, true),
            ["gitlab_rename_pipeline"] = new(Grant.DevOps, false),
            ["gitlab_get_pipeline_test_report"] = new(Grant.Delivery, true),
            ["gitlab_list_pipeline_variables"] = new(Grant.Delivery, true),
            ["gitlab_get_job"] = new(Grant.Delivery, true),
            ["gitlab_retry_job"] = new(Grant.Delivery, false),
            ["gitlab_play_job"] = new(Grant.Delivery, false),
            ["gitlab_erase_job"] = new(Grant.DevOps, false),
            ["gitlab_get_job_log"] = new(Grant.Delivery, true),
            ["gitlab_list_pipeline_schedules"] = new(Grant.DevOps, true),
            ["gitlab_get_pipeline_schedule"] = new(Grant.DevOps, true),
            ["gitlab_update_pipeline_schedule"] = new(Grant.DevOps, false),
            ["gitlab_delete_pipeline_schedule"] = new(Grant.DevOps, false),

            // Backlog wave, part 2: pipeline-schedule run/ownership/variable CRUD, trigger CRUD plus
            // firing a pipeline via a trigger/job token (Developer + DevOps, since developers routinely
            // fire triggers as part of their own workflow), and project/group CI variable CRUD
            // (DevOps-only) alongside its instance-wide, administrator-only counterpart.
            ["gitlab_run_pipeline_schedule"] = new(Grant.DevOps, false),
            ["gitlab_take_pipeline_schedule_ownership"] = new(Grant.DevOps, false),
            ["gitlab_get_pipeline_schedule_variable"] = new(Grant.DevOps, true),
            ["gitlab_create_pipeline_schedule_variable"] = new(Grant.DevOps, false),
            ["gitlab_update_pipeline_schedule_variable"] = new(Grant.DevOps, false),
            ["gitlab_delete_pipeline_schedule_variable"] = new(Grant.DevOps, false),
            ["gitlab_get_trigger"] = new(Grant.DevOps, true),
            ["gitlab_update_trigger"] = new(Grant.DevOps, false),
            ["gitlab_delete_trigger"] = new(Grant.DevOps, false),
            ["gitlab_trigger_pipeline"] = new(Grant.Delivery, false),
            ["gitlab_get_ci_variable"] = new(Grant.DevOps, true),
            ["gitlab_update_ci_variable"] = new(Grant.DevOps, false),
            ["gitlab_delete_ci_variable"] = new(Grant.DevOps, false),

            // Instance-wide CI/CD variables -- "Administrator token required" per each tool's own
            // [Description], so bare Grant.AdminOnly (DEC-027): never combined with a persona bit, since
            // Grant.IsVisibleIn checks bits independently and a persona bit alone would make the row
            // reachable under that persona without administrator access.
            ["gitlab_list_instance_variables"] = new(Grant.AdminOnly, true),
            ["gitlab_get_instance_variable"] = new(Grant.AdminOnly, true),
            ["gitlab_create_instance_variable"] = new(Grant.AdminOnly, false),
            ["gitlab_update_instance_variable"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_instance_variable"] = new(Grant.AdminOnly, false),

            // Backlog wave, part 3: job-artifact download/retention/deletion (Developer + DevOps for the
            // reads and the non-destructive "keep" toggle, since developers routinely pull their own
            // build's artifacts; DevOps-only for the wholesale per-job delete), CI/CD Catalog component
            // publishing, and the job-token scope toggle plus its allowlist CRUD (all DevOps-only, same
            // as the rest of the job-token-scope surface above).
            ["gitlab_download_job_artifacts"] = new(Grant.Delivery, true),
            ["gitlab_get_job_artifact_file"] = new(Grant.Delivery, true),
            ["gitlab_keep_job_artifacts"] = new(Grant.Delivery, false),
            ["gitlab_delete_job_artifacts"] = new(Grant.DevOps, false),
            ["gitlab_publish_ci_catalog_version"] = new(Grant.DevOps, false),
            ["gitlab_get_job_token_scope"] = new(Grant.DevOps, true),
            ["gitlab_set_job_token_scope"] = new(Grant.DevOps, false),
            ["gitlab_add_job_token_allowlist_entry"] = new(Grant.DevOps, false),
            ["gitlab_remove_job_token_allowlist_entry"] = new(Grant.DevOps, false)
        };
    }
}