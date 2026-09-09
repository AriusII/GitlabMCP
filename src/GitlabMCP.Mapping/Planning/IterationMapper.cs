using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

public static class IterationMapper
{
    public static IterationSummary ToSummary(GitLabIteration iteration)
    {
        return new IterationSummary(
            iteration.Id,
            iteration.Iid,
            iteration.Title,
            iteration.Description,
            DescribeState(iteration.State),
            iteration.StartDate,
            iteration.DueDate,
            iteration.WebUrl?.ToString());
    }

    /// <summary>
    ///     <see cref="GitLabIteration.State" /> comes back from GitLab as a raw integer (1 upcoming,
    ///     2 current, 3 closed) — unlike every other state field in the library. See the type's own XML doc.
    /// </summary>
    private static string DescribeState(int? state)
    {
        return state switch
        {
            1 => "upcoming",
            2 => "current",
            3 => "closed",
            _ => "unknown"
        };
    }
}