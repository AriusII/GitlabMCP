using GitlabMCP.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace GitlabMCP.Profiles;

/// <summary>
///     Removes every primitive the active <see cref="McpProfile" /> does not grant, from
///     <see cref="McpServerOptions.ToolCollection" />/<c>PromptCollection</c>/<c>ResourceCollection</c> —
///     gating works by REMOVAL from the collection, never by filtering <c>tools/list</c> alone (a primitive
///     left in the collection is still callable via <c>tools/call</c> even when hidden from the listing).
///     DEC-004=A: the profile is a constant closed over at startup, but the removal pass itself still runs
///     on every HTTP request in Stateless mode — each request gets a fresh, pre-populated collection to
///     remove from.
///     Deliberately independent of ASP.NET Core / <c>HttpContext</c> / <c>HttpServerTransportOptions</c> —
///     those live only in the host (DEC-018: this project references only <c>ModelContextProtocol.Core</c>,
///     never <c>.AspNetCore</c>). The host's <c>Program.cs</c> wraps <see cref="Apply" /> inside its own
///     <c>ConfigureSessionOptions</c> delegate.
/// </summary>
public static class ProfileGate
{
    /// <summary>
    ///     Removes every ungranted primitive from <paramref name="options" /> and sets its per-profile
    ///     ServerInstructions.
    /// </summary>
    public static void Apply(McpServerOptions options, McpProfile profile)
    {
        if (options.ToolCollection is { } tools)
            foreach (var tool in tools.ToArray()) // ToArray() first: never mutate the collection being walked.
                if (!ProfileCatalog.ToolGrants.TryGetValue(tool.ProtocolTool.Name, out var grant) ||
                    !grant.Grant.IsVisibleIn(profile))
                    tools.Remove(tool);

        if (options.PromptCollection is { } prompts)
            foreach (var prompt in prompts.ToArray())
                if (!ProfileCatalog.PromptGrants.TryGetValue(prompt.ProtocolPrompt.Name, out var grant) ||
                    !grant.Grant.IsVisibleIn(profile))
                    prompts.Remove(prompt);

        // DEC-023/DEC-029. A direct resource carries ProtocolResource; a templated one carries
        // ProtocolResourceTemplate instead (never both) — both are gated on the same ResourceGrants
        // table, keyed on whichever protocol name is present.
        if (options.ResourceCollection is { } resources)
            foreach (var resource in resources.ToArray())
            {
                var name = resource.ProtocolResource?.Name ?? resource.ProtocolResourceTemplate?.Name;
                if (name is null ||
                    !ProfileCatalog.ResourceGrants.TryGetValue(name, out var grant) ||
                    !grant.Grant.IsVisibleIn(profile))
                    resources.Remove(resource);
            }

        options.ServerInstructions = ProfileInstructions.For(profile);
    }

    /// <summary>
    ///     Throws at startup on a dark profile, an uncatalogued primitive, a dead catalog row, or a tool
    ///     whose catalog <see cref="ToolGrant.ReadOnly" /> flag disagrees with its registered
    ///     <c>ReadOnlyHint</c> (DEC-006). Call once, before <c>app.Run()</c> — never per request.
    /// </summary>
    public static void AssertCatalogIsComplete(IServiceProvider services)
    {
        var errors = new List<string>();

        foreach (var p in Enum.GetValues<McpProfile>())
        {
            var anyVisible = ProfileCatalog.ToolGrants.Values.Any(g => g.Grant.IsVisibleIn(p)) ||
                             ProfileCatalog.PromptGrants.Values.Any(g => g.Grant.IsVisibleIn(p));
            if (!anyVisible)
                errors.Add($"McpProfile.{p} grants zero tools and zero prompts — it would serve an empty surface.");
        }

        var registeredToolInstances = services.GetServices<McpServerTool>().ToArray();
        var registeredTools = registeredToolInstances
            .Select(static t => t.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);
        var registeredPrompts = services.GetServices<McpServerPrompt>()
            .Select(static p => p.ProtocolPrompt.Name)
            .ToHashSet(StringComparer.Ordinal);
        var registeredResources = services.GetServices<McpServerResource>()
            .Select(static r => r.ProtocolResource?.Name ?? r.ProtocolResourceTemplate?.Name)
            .Where(static n => n is not null)
            .Select(static n => n!)
            .ToHashSet(StringComparer.Ordinal);

        AssertBidirectional(errors, "tool", registeredTools, ProfileCatalog.ToolGrants.Keys, "ToolGrants");
        AssertBidirectional(errors, "prompt", registeredPrompts, ProfileCatalog.PromptGrants.Keys, "PromptGrants");
        AssertBidirectional(errors, "resource", registeredResources, ProfileCatalog.ResourceGrants.Keys,
            "ResourceGrants");

        foreach (var (name, grant) in ProfileCatalog.ToolGrants)
            if (grant.Grant == Grant.None)
                errors.Add($"ToolGrants['{name}'] is Grant.None — reachable in no profile at all.");

        foreach (var (name, grant) in ProfileCatalog.PromptGrants)
            if (grant.Grant == Grant.None)
                errors.Add($"PromptGrants['{name}'] is Grant.None.");

        foreach (var (name, grant) in ProfileCatalog.ResourceGrants)
            if (grant.Grant == Grant.None)
                errors.Add($"ResourceGrants['{name}'] is Grant.None.");

        // DEC-006: the SDK omits an unset ReadOnlyHint from the wire entirely rather than sending
        // false, so "hint is exactly true" vs "hint is anything else" is the correct comparison.
        foreach (var tool in registeredToolInstances)
        {
            if (!ProfileCatalog.ToolGrants.TryGetValue(tool.ProtocolTool.Name,
                    out var grant)) continue; // already reported by AssertBidirectional above

            var hintIsReadOnly = tool.ProtocolTool.Annotations?.ReadOnlyHint == true;
            if (hintIsReadOnly != grant.ReadOnly)
                errors.Add(
                    $"Tool '{tool.ProtocolTool.Name}': catalog ReadOnly={grant.ReadOnly} disagrees with " +
                    $"its registered ReadOnlyHint={hintIsReadOnly}.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "ProfileCatalog is incomplete:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static void AssertBidirectional(
        List<string> errors, string kind, HashSet<string> registered, IEnumerable<string> catalogued,
        string catalogName)
    {
        var cataloguedSet = catalogued.ToHashSet(StringComparer.Ordinal);

        string[] orphans = [.. registered.Where(n => !cataloguedSet.Contains(n)).Order(StringComparer.Ordinal)];
        if (orphans.Length > 0)
            errors.Add(
                $"{catalogName} has no row for {kind}(s) [{string.Join(", ", orphans)}] — " +
                "an uncatalogued primitive is invisible in every profile.");

        string[] dead = [.. cataloguedSet.Where(n => !registered.Contains(n)).Order(StringComparer.Ordinal)];
        if (dead.Length > 0)
            errors.Add(
                $"{catalogName} rows are dead — {kind}(s) [{string.Join(", ", dead)}] are catalogued but " +
                "never registered. A dead row puts a name in a golden snapshot that no client can ever see.");
    }
}