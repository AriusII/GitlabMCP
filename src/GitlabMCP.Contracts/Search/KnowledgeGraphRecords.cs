namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabKnowledgeGraphNamespace</c> — an id, a root-namespace id
///     and a timestamp only. No GitLab-authored string reaches this record, so the list tool declares the
///     bare record rather than <see cref="GitLabContent" /> (DEC-007's all-scalar exception).
/// </summary>
public sealed record KnowledgeGraphNamespaceSummary(
    long? Id,
    long? RootNamespaceId,
    DateTimeOffset? CreatedAt);

public sealed record KnowledgeGraphNamespaceListResult(
    IReadOnlyList<KnowledgeGraphNamespaceSummary> Namespaces,
    bool Truncated);

/// <summary>
///     Confirms a disable call. GitLab answers this endpoint with an empty 204 body, so every field here is
///     either the caller's own input (<see cref="NamespaceId" />, echoed back) or a server-computed flag —
///     nothing GitLab-authored — so this is the bare-record case, not <see cref="GitLabContent" />.
/// </summary>
public sealed record KnowledgeGraphDisableResult(string NamespaceId, bool Disabled);