using GitLab.Client.Models;
using GitlabMCP.Contracts.MergeRequests;

namespace GitlabMCP.Mapping.MergeRequests;

public static class ExternalStatusCheckMapper
{
    /// <summary>Project's configured check (from <c>ListAsync</c>) — no live status, since none has run yet in this view.</summary>
    public static ExternalStatusCheckSummary ToSummary(GitLabExternalStatusCheck check)
    {
        return new ExternalStatusCheckSummary(
            check.Id,
            check.Name,
            check.ExternalUrl?.ToString(),
            null,
            check.ProtectedBranches?.Select(static b => b.Name).Where(static n => n is not null).Select(static n => n!)
                .ToList() ?? []);
    }

    /// <summary>
    ///     One merge request's live check result (from <c>ListForMergeRequestAsync</c>) — no protected-branch scope in
    ///     this view.
    /// </summary>
    public static ExternalStatusCheckSummary ToSummary(GitLabMergeRequestStatusCheck check)
    {
        return new ExternalStatusCheckSummary(
            check.Id,
            check.Name,
            check.ExternalUrl?.ToString(),
            check.Status,
            []);
    }
}