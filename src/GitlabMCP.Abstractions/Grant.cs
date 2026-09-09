namespace GitlabMCP.Abstractions;

/// <summary>
///     Which profile(s) a tool, prompt, or resource is granted to (DEC-005/DEC-006). A single primitive can
///     carry more than one bit — e.g. a planning tool shared by <see cref="Maintainer" /> and
///     <see cref="Developer" />. <see cref="Full" /> is a union, not a distinct fifth surface: a
///     <see cref="McpProfile.FullPermission" /> operator sees every primitive whose <see cref="Grant" /> is not
///     <see cref="None" />.
/// </summary>
[Flags]
public enum Grant
{
    /// <summary>
    ///     Reachable in no profile at all. A catalog row with this value is a startup-fatal authoring
    ///     mistake — <c>ProfileGate.AssertCatalogIsComplete</c> rejects it (a fourth check beyond the three
    ///     DEC-014 already named, closing the gap <c>gitlab-mcp-reviewer</c> §B6 flagged).
    /// </summary>
    None = 0,

    /// <summary>Product Owner / Product Manager: planning — ideas, roadmap, milestones, epics, issues, tasks.</summary>
    Maintainer = 1 << 0,

    /// <summary>Developer: MR/PR lifecycle, epics, issues, tasks, and the code/group/project management that supports them.</summary>
    Developer = 1 << 1,

    /// <summary>Platform / SRE: administration, settings, CI/CD, runners, Terraform, infrastructure.</summary>
    DevOps = 1 << 2,

    /// <summary>
    ///     Instance administration that belongs to no persona — reachable only under
    ///     <see cref="McpProfile.FullPermission" />, never under a persona profile alone (DEC-005).
    /// </summary>
    AdminOnly = 1 << 3,

    /// <summary>The union every <see cref="McpProfile.FullPermission" /> operator sees (DEC-005).</summary>
    Full = Maintainer | Developer | DevOps | AdminOnly,

    // Convenience combinations — spell out a full name instead of re-deriving the same OR at every one
    // of the hundreds of catalog rows across the tool domains.
    /// <summary>Planning surface shared by both personas that touch backlog/roadmap work.</summary>
    Planning = Maintainer | Developer,

    /// <summary>Delivery surface shared by the two personas that touch shipping code.</summary>
    Delivery = Developer | DevOps,

    /// <summary>Every persona profile, excluding <see cref="AdminOnly" />.</summary>
    Everyone = Maintainer | Developer | DevOps
}

/// <summary>Maps an operator-selected <see cref="McpProfile" /> onto the <see cref="Grant" /> bit(s) it exposes.</summary>
public static class McpProfileExtensions
{
    /// <summary>
    ///     True iff a primitive carrying <paramref name="grant" /> is visible under <paramref name="profile" />.
    ///     <see cref="McpProfile.FullPermission" /> sees anything not <see cref="Grant.None" /> — it is defined
    ///     as the union (DEC-005), so it must never be listed as its own bit here.
    /// </summary>
    public static bool IsVisibleIn(this Grant grant, McpProfile profile)
    {
        return profile switch
        {
            McpProfile.Maintainer => grant.HasFlag(Grant.Maintainer),
            McpProfile.Developer => grant.HasFlag(Grant.Developer),
            McpProfile.DevOps => grant.HasFlag(Grant.DevOps),
            McpProfile.FullPermission => grant != Grant.None,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unhandled McpProfile.")
        };
    }
}