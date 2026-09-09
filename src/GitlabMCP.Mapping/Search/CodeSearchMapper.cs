using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabZoektIndexedNamespace" /> into the owned <see cref="ZoektIndexedNamespaceSummary" />
///     .
/// </summary>
public static class CodeSearchMapper
{
    public static ZoektIndexedNamespaceSummary ToSummary(GitLabZoektIndexedNamespace ns)
    {
        return new ZoektIndexedNamespaceSummary(
            ns.Id,
            ns.ZoektShardId,
            ns.ZoektNodeId,
            ns.NamespaceId,
            ns.NumberOfReplicasOverride);
    }

    public static ZoektNodeSummary ToSummary(GitLabZoektNode node)
    {
        return new ZoektNodeSummary(
            node.Id,
            node.IndexBaseUrl?.ToString(),
            node.SearchBaseUrl?.ToString());
    }

    public static ZoektProjectIndexResult ToIndexResult(GitLabZoektProjectIndexResult result)
    {
        return new ZoektProjectIndexResult(
            result.JobId);
    }
}