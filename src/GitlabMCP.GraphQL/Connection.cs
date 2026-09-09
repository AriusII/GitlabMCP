namespace GitlabMCP.GraphQL;

/// <summary>
///     Relay-style connection shape GitLab's GraphQL API uses for every paginated field. One generic wrapper, not one
///     per node type.
/// </summary>
public sealed record Connection<TNode>(IReadOnlyList<Edge<TNode>> Edges, PageInfo PageInfo, int? Count)
{
    public IEnumerable<TNode> Nodes => Edges.Select(static e => e.Node);
}

public sealed record Edge<TNode>(TNode Node, string Cursor);

public sealed record PageInfo(bool HasNextPage, bool HasPreviousPage, string? StartCursor, string? EndCursor);