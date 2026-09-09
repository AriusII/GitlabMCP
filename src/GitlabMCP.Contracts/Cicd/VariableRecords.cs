namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     CI/CD variable projection (domain "cicd"). <c>Value</c> is deliberately never projected here, on
///     either the list or the create tool — <c>GitLab.Client.Models.GitLabVariable.Value</c> is on
///     CLAUDE.md rule 5's closed secret-field list unconditionally, with no "already redacted when masked"
///     carve-out; the create tool doesn't get an exception either, since its purpose text (unlike e.g. the
///     impersonation-token tool) never says the plaintext is meant to come back.
/// </summary>
public sealed record VariableSummary(
    string? Key,
    string? VariableType,
    bool? Protected,
    bool? Masked,
    bool? Hidden,
    bool? Raw,
    string? EnvironmentScope,
    string? Description);

public sealed record VariableListResult(IReadOnlyList<VariableSummary> Variables, bool Truncated);

/// <summary>
///     Result of deleting a CI/CD variable. Carries no GitLab-authored text at all (just a confirmation
///     flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4 — same shape as
///     <see cref="PipelineDeleteResult" /> in <c>PipelineRecords.cs</c>. Shared across
///     <c>gitlab_delete_ci_variable</c>, <c>gitlab_delete_pipeline_schedule_variable</c> and
///     <c>gitlab_delete_instance_variable</c> — all three delete "a CI/CD variable", differing only in scope.
/// </summary>
public sealed record VariableDeleteResult(bool Deleted);