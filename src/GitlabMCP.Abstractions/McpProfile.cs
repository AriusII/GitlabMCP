namespace GitlabMCP.Abstractions;

/// <summary>
///     The four operator-selectable profiles (CLAUDE.md §Target architecture: profiles). Resolved exactly
///     once, at startup, from configuration (DEC-004 = A) — never per request, never from a header.
/// </summary>
public enum McpProfile
{
    /// <summary>
    ///     DEC-012's fallback when <c>GitLabMcp:Profile</c> is absent — the narrowest surface. Do not
    ///     reorder these members: <see cref="Maintainer" /> being ordinal 0 is load-bearing for that
    ///     fallback (<c>default(McpProfile)</c> must equal the documented "absent" behaviour).
    /// </summary>
    Maintainer = 0,
    Developer,
    DevOps,

    /// <summary>
    ///     The union of every persona plus <see cref="Grant.AdminOnly" /> (DEC-005) — an administrative and
    ///     debugging profile. Never recommend it as a default in a README, prompt, or ServerInstructions.
    /// </summary>
    FullPermission
}