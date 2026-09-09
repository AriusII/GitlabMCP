using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabActiveContextCollectionDetail" /> into the owned
///     <see cref="ActiveContextCollectionResult" /> — <c>Options</c> is surfaced as its raw JSON text rather than
///     re-typed, since its shape is not part of the library's contract.
/// </summary>
public static class ActiveContextMapper
{
    /// <summary>
    ///     Cap on <see cref="ActiveContextCollectionResult.OptionsJson" />, matching
    ///     <c>SearchTools.MaxRawTextLength</c> — the same 20,000-character convention
    ///     <c>gitlab_list_search_migrations</c>, the only other raw-JSON-text tool in this domain, uses. GitLab's
    ///     own schema for this field is just queue-sharding counters, so this is a defensive bound rather than
    ///     one expected to trigger in practice.
    /// </summary>
    private const int MaxOptionsJsonChars = 20_000;

    public static ActiveContextCollectionResult ToResult(GitLabActiveContextCollectionDetail detail)
    {
        var (optionsJson, truncated) = Truncate(detail.Options?.GetRawText());
        return new ActiveContextCollectionResult(
            detail.Id,
            detail.Name,
            detail.ConnectionId,
            optionsJson,
            truncated,
            detail.CreatedAt,
            detail.UpdatedAt);
    }

    private static (string? Json, bool Truncated) Truncate(string? json)
    {
        if (json is null) return (null, false);

        return json.Length > MaxOptionsJsonChars
            ? (json[..MaxOptionsJsonChars], true)
            : (json, false);
    }

    public static ActiveContextConnectionSummary ToConnectionSummary(GitLabActiveContextConnection connection)
    {
        return new ActiveContextConnectionSummary(
            connection.Id,
            connection.Name,
            connection.AdapterClass,
            connection.Prefix,
            connection.Active,
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    public static ActiveContextNamespaceStateResult ToNamespaceStateResult(GitLabActiveContextCodeEnabledNamespace ns)
    {
        return new ActiveContextNamespaceStateResult(
            ns.Id,
            ns.NamespaceId,
            ns.ConnectionId,
            ns.State,
            ns.CreatedAt,
            ns.UpdatedAt);
    }
}