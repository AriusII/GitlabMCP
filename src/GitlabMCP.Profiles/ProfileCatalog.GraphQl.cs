using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Epic/work-item tools (DEC-011=C/DEC-020/DEC-021) — Maintainer + Developer per CLAUDE.md's persona
    // tables ("epics" appears under both).
    private static IReadOnlyDictionary<string, ToolGrant> GraphQlRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_epics"] = new(Grant.Planning, true),
            ["gitlab_get_epic"] = new(Grant.Planning, true),
            ["gitlab_create_epic"] = new(Grant.Planning, false),
            ["gitlab_update_epic"] = new(Grant.Planning, false),
            ["gitlab_delete_epic"] = new(Grant.Maintainer, false),
            ["gitlab_add_epic_note"] = new(Grant.Planning, false),
            ["gitlab_list_epic_children"] = new(Grant.Planning, true)
        };
    }
}