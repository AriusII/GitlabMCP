using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>
///     Projects <see cref="GitLabTrigger" /> into the owned "cicd" records. <see cref="ToSummary" /> (used by
///     the list tool) never carries <see cref="GitLabTrigger.Token" />; <see cref="ToCreateResult" /> (used
///     only by the create tool) does, per the deliberate exception documented on
///     <see cref="TriggerCreateResult" />.
/// </summary>
public static class TriggerMapper
{
    public static TriggerSummary ToSummary(GitLabTrigger trigger)
    {
        return new TriggerSummary(
            trigger.Id,
            trigger.Description,
            trigger.CreatedAt,
            trigger.UpdatedAt,
            trigger.LastUsed,
            trigger.ExpiresAt,
            trigger.Owner?.Username);
    }

    public static TriggerCreateResult ToCreateResult(GitLabTrigger trigger)
    {
        return new TriggerCreateResult(
            trigger.Id,
            trigger.Token,
            trigger.Description,
            trigger.CreatedAt,
            trigger.UpdatedAt,
            trigger.ExpiresAt,
            trigger.Owner?.Username);
    }
}