using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabKnowledgeGraphNamespace" /> into the owned
///     <see cref="KnowledgeGraphNamespaceSummary" />.
/// </summary>
public static class KnowledgeGraphMapper
{
    public static KnowledgeGraphNamespaceSummary ToSummary(GitLabKnowledgeGraphNamespace ns)
    {
        return new KnowledgeGraphNamespaceSummary(
            ns.Id,
            ns.RootNamespaceId,
            ns.CreatedAt);
    }
}