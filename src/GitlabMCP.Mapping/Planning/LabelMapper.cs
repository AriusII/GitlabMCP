using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

/// <summary>
///     Projects both the project-scoped <see cref="GitLabLabel" /> and the group-scoped
///     <see cref="GitLabGroupLabel" /> into the one shared <see cref="LabelSummary" /> shape.
/// </summary>
public static class LabelMapper
{
    public static LabelSummary ToSummary(GitLabLabel label)
    {
        return new LabelSummary(
            label.Id,
            label.Name,
            label.Color,
            label.TextColor,
            label.Description,
            label.Archived,
            label.Priority,
            label.OpenIssuesCount,
            label.ClosedIssuesCount,
            label.OpenMergeRequestsCount,
            label.Subscribed);
    }

    public static LabelSummary ToSummary(GitLabGroupLabel label)
    {
        return new LabelSummary(
            label.Id,
            label.Name,
            label.Color,
            label.TextColor,
            label.Description,
            label.Archived,
            null,
            label.OpenIssuesCount,
            label.ClosedIssuesCount,
            label.OpenMergeRequestsCount,
            label.Subscribed);
    }
}