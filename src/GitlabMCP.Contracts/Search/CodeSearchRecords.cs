namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabZoektIndexedNamespace</c> — every field is a numeric id
///     (shard/node/namespace ids, a replica-count override). No GitLab-authored *string* reaches this
///     record, so the tools returning it declare the bare record rather than
///     <see cref="GitLabContent" /> (DEC-007's all-scalar exception).
/// </summary>
public sealed record ZoektIndexedNamespaceSummary(
    long? Id,
    long? ZoektShardId,
    long? ZoektNodeId,
    long? NamespaceId,
    int? NumberOfReplicasOverride);

public sealed record ZoektIndexedNamespaceListResult(
    IReadOnlyList<ZoektIndexedNamespaceSummary> Namespaces,
    bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabZoektNode</c> — one exact-code-search (Zoekt) node's
///     index/search base URLs. Those URLs are GitLab-configured infrastructure addresses, so the tool wraps
///     via <see cref="GitLabContent" />.
/// </summary>
public sealed record ZoektNodeSummary(long? Id, string? IndexBaseUrl, string? SearchBaseUrl);

public sealed record ZoektNodeListResult(IReadOnlyList<ZoektNodeSummary> Nodes, bool Truncated);

/// <summary>
///     All-scalar confirmation of a namespace removal from a Zoekt index — no GitLab-authored string, so the tool
///     returns this bare (unwrapped).
/// </summary>
public sealed record ZoektIndexedNamespaceRemoveResult(long NodeId, long NamespaceId, bool Removed);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabZoektProjectIndexResult</c> — the background job id GitLab
///     enqueued for a reindex. GitLab-generated, so the tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ZoektProjectIndexResult(string? JobId);