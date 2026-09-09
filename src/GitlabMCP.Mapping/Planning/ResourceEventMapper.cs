using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

/// <summary>
///     Projects each of the five <c>IResourceEventsClient</c> event DTOs into the one shared
///     <see cref="ResourceEventSummary" /> shape, leaving the fields the family doesn't carry null.
/// </summary>
public static class ResourceEventMapper
{
    public static ResourceEventSummary FromLabelEvent(GitLabResourceLabelEvent e)
    {
        return new ResourceEventSummary(
            e.Id,
            e.Action,
            e.ResourceType,
            e.ResourceId,
            e.CreatedAt,
            e.User?.Username,
            e.Label?.Name,
            null,
            null,
            null,
            null);
    }

    public static ResourceEventSummary FromStateEvent(GitLabResourceStateEvent e)
    {
        return new ResourceEventSummary(
            e.Id,
            null,
            e.ResourceType,
            e.ResourceId,
            e.CreatedAt,
            e.User?.Username,
            null,
            null,
            null,
            null,
            e.State);
    }

    public static ResourceEventSummary FromMilestoneEvent(GitLabResourceMilestoneEvent e)
    {
        return new ResourceEventSummary(
            e.Id,
            e.Action,
            e.ResourceType,
            e.ResourceId,
            e.CreatedAt,
            e.User?.Username,
            null,
            e.Milestone?.Title,
            null,
            null,
            e.State);
    }

    public static ResourceEventSummary FromIterationEvent(GitLabResourceIterationEvent e)
    {
        return new ResourceEventSummary(
            e.Id,
            e.Action,
            e.ResourceType,
            e.ResourceId,
            e.CreatedAt,
            e.User?.Username,
            null,
            null,
            e.Iteration?.Title,
            null,
            null);
    }

    public static ResourceEventSummary FromWeightEvent(GitLabResourceWeightEvent e)
    {
        return new ResourceEventSummary(
            e.Id,
            null,
            null,
            e.IssueId,
            e.CreatedAt,
            e.User?.Username,
            null,
            null,
            null,
            e.Weight,
            null);
    }
}