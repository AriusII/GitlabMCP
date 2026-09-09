using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

public static class MilestoneMapper
{
    public static MilestoneSummary ToSummary(GitLabMilestone milestone)
    {
        return new MilestoneSummary(
            milestone.Id,
            milestone.Iid,
            milestone.Title,
            milestone.Description,
            milestone.State,
            milestone.StartDate,
            milestone.DueDate,
            milestone.WebUrl?.ToString());
    }

    public static BurndownEventSummary ToSummary(GitLabBurndownEvent burndownEvent)
    {
        return new BurndownEventSummary(
            burndownEvent.Action,
            burndownEvent.CreatedAt,
            burndownEvent.Weight);
    }
}