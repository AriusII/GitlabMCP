namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     Pipeline trigger token projections (domain "cicd"). <see cref="TriggerSummary" /> (the list shape)
///     never carries <c>GitLab.Client.Models.GitLabTrigger.Token</c> — CLAUDE.md rule 5 names
///     <c>Trigger.Token</c> explicitly on the closed secret-field list. <see cref="TriggerCreateResult" /> is
///     the one deliberate exception rule 5 allows: the catalog's own purpose text for
///     <c>gitlab_create_trigger</c> says "its value is only ever shown in full at creation", so the create
///     tool alone carries it.
/// </summary>
public sealed record TriggerSummary(
    long Id,
    string? Description,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastUsed,
    DateTimeOffset? ExpiresAt,
    string? OwnerUsername);

public sealed record TriggerListResult(IReadOnlyList<TriggerSummary> Triggers, bool Truncated);

/// <summary>Result of <c>gitlab_create_trigger</c> — the only tool in this domain allowed to carry the trigger token.</summary>
public sealed record TriggerCreateResult(
    long Id,
    string? Token,
    string? Description,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    string? OwnerUsername);

/// <summary>
///     Result of <c>gitlab_delete_trigger</c>. Carries no GitLab-authored text at all (just a confirmation
///     flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4 — same shape as
///     <see cref="PipelineDeleteResult" /> in <c>PipelineRecords.cs</c>.
/// </summary>
public sealed record TriggerDeleteResult(bool Deleted);