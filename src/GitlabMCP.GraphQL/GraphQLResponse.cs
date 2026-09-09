using System.Globalization;
using System.Text.Json;

namespace GitlabMCP.GraphQL;

/// <summary>
///     The wire shape every GraphQL-over-HTTP response body has, win or lose. HTTP 200 is not success —
///     <see cref="Errors" /> can be populated alongside a partial or null <see cref="Data" /> (DEC-021).
/// </summary>
public sealed record GraphQLResponse<TData>(TData? Data, IReadOnlyList<GraphQLError>? Errors);

/// <summary>One entry of the GraphQL spec's <c>errors</c> array. <see cref="Message" /> is always free text — DEC-021.</summary>
public sealed record GraphQLError(
    string Message,
    IReadOnlyList<GraphQLErrorLocation>? Locations,
    /// <summary>
    /// The field path the error occurred at, as a mix of field-name strings and list-index integers.
    /// Kept as <see cref="JsonElement"/> (a built-in, non-reflective converter — no
    /// <c>[JsonSerializable]</c> entry needed for the element type itself) rather than <c>object</c>.
    /// </summary>
    IReadOnlyList<JsonElement>? Path,
    IReadOnlyDictionary<string, JsonElement>? Extensions)
{
    /// <summary>
    ///     Renders <see cref="Path" /> as <c>workItem.widgets.2.title</c> for logs and error text — safe to relay
    ///     unconditionally (DEC-021).
    /// </summary>
    public string? PathString()
    {
        return Path is null
            ? null
            : string.Join('.', Path.Select(static p => p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetInt32().ToString(CultureInfo.InvariantCulture),
                _ => "?"
            }));
    }
}

public sealed record GraphQLErrorLocation(int Line, int Column);