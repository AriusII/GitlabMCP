using System.Text.Json;
using ModelContextProtocol;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     The server's single <see cref="JsonSerializerOptions" /> instance (DEC-009). Every
///     <c>WithTools&lt;T&gt;()</c>/<c>WithPrompts&lt;T&gt;()</c> registration and every
///     <see cref="GitlabMCP.Contracts.GitLabContent.Wrap{T}" /> call serializes through this — never a second
///     instance, and never <c>SomeContext.Default.&lt;T&gt;</c> directly (that carries no naming policy and
///     silently emits PascalCase where the MCP wire is camelCase).
/// </summary>
public static class GitLabJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        // Insert at the head: REST/MCP payload types first, GraphQL request/response types second.
        // GitLabGraphQlJsonContext.Default is inserted by the host once GitlabMCP.GraphQL is wired in
        // (GitlabMCP.Contracts cannot reference GitlabMCP.GraphQL — that would invert DEC-018's
        // dependency direction) — see src/GitlabMCP/Program.cs.
        options.TypeInfoResolverChain.Insert(0, GitlabMcpJsonContext.Default);
        return options;
    }

    /// <summary>
    ///     Called once by the host after inserting every other domain's
    ///     <see cref="System.Text.Json.Serialization.JsonSerializerContext" />
    ///     (GraphQL, future contexts) into <see cref="Options" />'s <c>TypeInfoResolverChain</c>. Locks the
    ///     instance so no later call can silently add a resolver order nobody reviewed.
    /// </summary>
    public static void Seal()
    {
        Options.MakeReadOnly();
    }
}