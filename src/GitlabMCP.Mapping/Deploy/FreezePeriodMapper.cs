using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class FreezePeriodMapper
{
    public static FreezePeriodSummary ToSummary(GitLabFreezePeriod period)
    {
        return new FreezePeriodSummary(
            period.Id,
            period.FreezeStart,
            period.FreezeEnd,
            period.CronTimezone,
            period.CreatedAt,
            period.UpdatedAt);
    }
}