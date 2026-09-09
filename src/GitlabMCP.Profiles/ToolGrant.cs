using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

/// <summary>
///     One catalog row for a tool: which profile(s) grant it, and its own honest read/write
///     classification — kept alongside <see cref="Grant" /> so <c>AssertCatalogIsComplete</c> can verify it
///     agrees with the tool's registered <c>ReadOnlyHint</c> (DEC-006).
/// </summary>
public readonly record struct ToolGrant(Grant Grant, bool ReadOnly);

/// <summary>One catalog row for a prompt or a resource (DEC-023) — no read/write axis applies to either.</summary>
public readonly record struct PrimitiveGrant(Grant Grant);