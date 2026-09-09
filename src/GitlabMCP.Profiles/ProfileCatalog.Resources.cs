using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // DEC-023/DEC-029/DEC-031: the resource surface. "server_info"/"server_profiles" (direct,
    // GitlabMCP.Tools.Resources.ServerResources) are server-authored metadata, visible everywhere —
    // Grant.Full mirrors the gitlab_ping canary's own grant. Every templated resource (
    // GitlabMCP.Tools.Resources.GitLabEntityResources) carries real GitLab content and is granted
    // exactly like its equivalent gitlab_get_* tool, so a client sees the resource iff it would already
    // be able to call the tool: gitlab_issue~gitlab_get_issue, gitlab_merge_request~gitlab_get_merge_request,
    // gitlab_epic~gitlab_get_epic, gitlab_file~gitlab_get_file_content, gitlab_wiki_page~gitlab_get_wiki_page,
    // gitlab_pipeline~gitlab_get_pipeline, gitlab_release~gitlab_get_release.
    private static IReadOnlyDictionary<string, PrimitiveGrant> ResourceRows()
    {
        return new Dictionary<string, PrimitiveGrant>(StringComparer.Ordinal)
        {
            ["server_info"] = new(Grant.Full),
            ["server_profiles"] = new(Grant.Full),
            ["gitlab_issue"] = new(Grant.Planning),
            ["gitlab_merge_request"] = new(Grant.Developer),
            ["gitlab_epic"] = new(Grant.Planning),
            ["gitlab_file"] = new(Grant.Planning),
            ["gitlab_wiki_page"] = new(Grant.Planning),
            ["gitlab_pipeline"] = new(Grant.Delivery),
            ["gitlab_release"] = new(Grant.Everyone)
        };
    }
}