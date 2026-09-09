using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Common;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for the "resources" domain (DEC-026/DEC-029) — the server-authored
///     structural resources under <c>gitlab-mcp://server/*</c>. Its own context, separate from every other
///     per-domain context — splitting <c>[JsonSerializable]</c> attributes for the same partial class
///     across two files throws CS8785 on this SDK (see <c>GitlabMcpJsonContext.cs</c>'s own docstring). The
///     host inserts <see cref="ResourcesJsonContext.Default" /> into
///     <c>GitLabJson.Options.TypeInfoResolverChain</c> alongside the other per-domain contexts.
///     GitLab-content-bearing resources (issue/merge_request/epic, under <c>GitlabMCP.Tools.Resources</c>)
///     reuse existing tool-domain record types and need no entries here.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ServerInfoResource))]
[JsonSerializable(typeof(ProfileDescription))]
[JsonSerializable(typeof(ServerProfilesResource))]
public sealed partial class ResourcesJsonContext : JsonSerializerContext;