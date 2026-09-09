namespace GitlabMCP.GraphQL;

/// <summary>
///     The wire shape GraphQL-over-HTTP expects in the POST body, per the informal "GraphQL over HTTP"
///     convention GitLab's implementation follows.
/// </summary>
/// <typeparam name="TVariables">
///     A per-operation record — never <c>object</c>, <c>Dictionary&lt;string, object&gt;</c> or
///     <c>JsonObject</c> for the common path (DEC-020/021): AOT-safe, source-generated, and the record's own
///     properties double as the MCP tool's parameter schema.
/// </typeparam>
public sealed record GraphQLRequest<TVariables>(string Query, TVariables Variables, string? OperationName = null);