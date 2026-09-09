using System.Collections.Frozen;

namespace GitlabMCP.Profiles;

/// <summary>
///     The single, data-driven map from a primitive's wire name to the profile(s) that grant it
///     (mcp-profile-gating Critical Rule 2; DEC-024's partitioning of it). No <c>if (profile == …)</c>
///     belongs anywhere inside a <c>Tools/</c> class — the compiler helps enforce this, since
///     <c>GitlabMCP.Tools</c> has no reference to this project beyond its public <see cref="ProfileGate" />
///     surface.
///     Partitioned across one file per domain (<c>ProfileCatalog.Planning.cs</c>, …), each contributing an
///     ordinary <c>private static IReadOnlyDictionary&lt;string, ToolGrant&gt; &lt;Domain&gt;Rows()</c>
///     method — DEC-024. This file is the only place those methods are called and merged; a tool class
///     never reads a domain's rows directly. <c>FrozenDictionary</c> is deliberate: the lookup runs on
///     every request for every primitive (DEC-004's per-request removal pass), and the table never changes
///     after startup.
/// </summary>
public static partial class ProfileCatalog
{
    private static readonly Lazy<FrozenDictionary<string, ToolGrant>> _toolGrants = new(BuildToolGrants);
    private static readonly Lazy<FrozenDictionary<string, PrimitiveGrant>> _promptGrants = new(BuildPromptGrants);
    private static readonly Lazy<FrozenDictionary<string, PrimitiveGrant>> _resourceGrants = new(BuildResourceGrants);

    public static FrozenDictionary<string, ToolGrant> ToolGrants => _toolGrants.Value;
    public static FrozenDictionary<string, PrimitiveGrant> PromptGrants => _promptGrants.Value;

    /// <summary>DEC-023: exists from the first resource ever added — no grace period of implicit universal grant.</summary>
    public static FrozenDictionary<string, PrimitiveGrant> ResourceGrants => _resourceGrants.Value;

    private static FrozenDictionary<string, ToolGrant> BuildToolGrants()
    {
        IReadOnlyDictionary<string, ToolGrant>[] domains =
        [
            CommonRows(),
            PlanningRows(),
            DiscussionRows(),
            MergeRequestsRows(),
            CodeRows(),
            PeopleRows(),
            ProjectsRows(),
            CicdRows(),
            PackagesRows(),
            SearchRows(),
            InfraRows(),
            DeployRows(),
            AdminRows(),
            LifecycleRows(),
            GraphQlRows()
        ];

        var merged = new Dictionary<string, ToolGrant>(StringComparer.Ordinal);
        foreach (var rows in domains)
        foreach (var (name, grant) in rows)
            merged.Add(name, grant); // Add(), not indexer: a duplicate name across domains must
        // throw here, never silently overwrite.
        return merged.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, PrimitiveGrant> BuildPromptGrants()
    {
        return PromptRows().ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, PrimitiveGrant> BuildResourceGrants()
    {
        return ResourceRows().ToFrozenDictionary(StringComparer.Ordinal);
    }
}