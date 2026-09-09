using System.Text.Json;
using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects <see cref="GitLabCodeReviewAnalyticsItem" /> into the owned
///     <see cref="CodeReviewAnalyticsItemSummary" />. <c>Author</c>/<c>Milestone</c>/<c>ApprovedBy</c> arrive
///     as raw <see cref="JsonElement" /> from the library (the spec declares no schema for them on this
///     endpoint) — this mapper picks the one field a caller needs out of each rather than passing the
///     element through untyped.
/// </summary>
public static class AnalyticsMapper
{
    public static CodeReviewAnalyticsItemSummary ToSummary(GitLabCodeReviewAnalyticsItem item)
    {
        return new CodeReviewAnalyticsItemSummary(
            item.Iid,
            item.Title,
            item.State,
            ExtractString(item.Author, "username"),
            ExtractString(item.Milestone, "title"),
            ExtractUsernames(item.ApprovedBy),
            item.NotesCount,
            item.ReviewTime,
            item.DiffStats,
            item.CreatedAt,
            item.UpdatedAt,
            item.WebUrl?.ToString());
    }

    private static string? ExtractString(JsonElement? element, string propertyName)
    {
        return element is { ValueKind: JsonValueKind.Object } obj &&
               obj.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static IReadOnlyList<string> ExtractUsernames(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array) return [];

        List<string> usernames = [];
        foreach (var entry in array.EnumerateArray())
            if (entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("username", out var username) &&
                username.ValueKind == JsonValueKind.String &&
                username.GetString() is { } name)
                usernames.Add(name);

        return usernames;
    }

    public static DeploymentFrequencyPointSummary ToSummary(GitLabDeploymentFrequency point)
    {
        return new DeploymentFrequencyPointSummary(
            point.Value,
            point.From,
            point.To);
    }

    /// <summary>
    ///     Parses the untyped JSON array GitLab's DORA metrics endpoints return into owned points, applying
    ///     the caller's <paramref name="limit" /> the same way a buffered (non-<c>IAsyncEnumerable</c>) list is
    ///     bounded elsewhere in this domain: take the first <paramref name="limit" /> and report whether more
    ///     existed. A non-array or malformed entry is treated as empty/null rather than thrown, since the
    ///     endpoint's response shape is not part of the library's typed contract.
    /// </summary>
    public static (IReadOnlyList<DoraMetricPointSummary> Points, bool Truncated) ParseDoraMetrics(JsonElement root,
        int limit)
    {
        if (root.ValueKind != JsonValueKind.Array) return ([], false);

        List<DoraMetricPointSummary> all = [];

        foreach (var entry in root.EnumerateArray())
        {
            var date = entry.ValueKind == JsonValueKind.Object &&
                       entry.TryGetProperty("date", out var dateElement) &&
                       dateElement.ValueKind == JsonValueKind.String
                ? dateElement.GetString()
                : null;

            double? value = entry.ValueKind == JsonValueKind.Object &&
                            entry.TryGetProperty("value", out var valueElement) &&
                            valueElement.ValueKind == JsonValueKind.Number
                ? valueElement.GetDouble()
                : null;

            all.Add(new DoraMetricPointSummary(date, value));
        }

        var truncated = all.Count > limit;
        return (truncated ? all.Take(limit).ToList() : all, truncated);
    }
}