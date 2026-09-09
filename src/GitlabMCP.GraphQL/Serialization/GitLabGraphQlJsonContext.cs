using System.Text.Json.Serialization;

namespace GitlabMCP.GraphQL.Serialization;

/// <summary>
///     Source-generated JSON metadata for every GraphQL request/response type. Split across one partial
///     file per operation domain (<c>GitLabGraphQlJsonContext.WorkItems.cs</c>, …) — additive and reviewable
///     in isolation, same mechanism as <c>GitlabMcpJsonContext</c> (DEC-024). Deliberately carries no
///     <c>PropertyNamingPolicy</c> here: naming policy is resolved from the ambient
///     <see cref="System.Text.Json.JsonSerializerOptions" /> instance a type's
///     <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo" /> is
///     fetched through (<c>GitLabJson.Options</c>, camelCase already) — only <c>Context.Default</c> would
///     need one, and nothing routes through <c>Context.Default</c> directly.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
public sealed partial class GitLabGraphQlJsonContext : JsonSerializerContext;