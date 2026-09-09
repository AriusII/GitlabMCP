using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Common;
using GitlabMCP.Contracts.Epics;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every REST/MCP payload record crossing the MCP boundary.
///     ALL [JsonSerializable] attributes for this context live in THIS ONE FILE — verified empirically
///     this session that splitting them across multiple files of the same partial class (as Microsoft's own
///     docs describe as supported, and as GitlabMcpJsonContext.Common.cs + GitlabMcpJsonContext.Epics.cs
///     originally attempted) throws at build time on this SDK:
///     `CS8785: JsonSourceGenerator ... hintName "GitlabMcpJsonContext.Boolean.g.cs" ... must be unique`.
///     Reproduced identically on SDK 10.0.401 and 10.0.303 — not a version-specific regression, a real
///     generator limitation when 2+ files each carry [JsonSerializable] attributes for the same context and
///     a reachable primitive (bool) collides. A context may still be split across multiple FILES for other
///     content, but only ONE of them may carry [JsonSerializable] attributes. (GitLabGraphQlJsonContext
///     avoids this by construction: its base file carries zero attributes and
///     GitLabGraphQlJsonContext.WorkItems.cs carries all of them — one attribute-bearing file, not two.)
///     New domains append their [JsonSerializable] entries to the list below, grouped under a comment
///     naming the domain, rather than adding a new per-domain file.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PingResult))]
// --- Epics (DEC-011=C/DEC-020) ---
[JsonSerializable(typeof(EpicSummary))]
[JsonSerializable(typeof(EpicDetail))]
[JsonSerializable(typeof(EpicListResult))]
[JsonSerializable(typeof(EpicDeleteResult))]
[JsonSerializable(typeof(EpicNoteResult))]
internal sealed partial class GitlabMcpJsonContext : JsonSerializerContext;