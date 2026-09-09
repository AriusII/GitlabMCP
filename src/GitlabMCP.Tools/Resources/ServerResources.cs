using System.ComponentModel;
using System.Text.Json;
using GitLab.Client.DependencyInjection;
using GitlabMCP.Abstractions;
using GitlabMCP.Contracts.Common;
using GitlabMCP.Contracts.Serialization;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools.Resources;

/// <summary>
///     Server-authored structural resources (DEC-029) — never GitLab content, never wrapped via
///     <c>GitLabContent</c>. <c>gitlab-mcp://server/*</c> is this server's own metadata, not GitLab's, so
///     the untrusted-content envelope does not apply (<see cref="Contracts.GitLabContent" />'s own
///     docstring names this class as the reason that rule exists).
/// </summary>
[McpServerResourceType]
public sealed class ServerResources(IOptions<GitLabMcpOptions> mcpOptions, IOptions<GitLabClientOptions> clientOptions)
{
    [McpServerResource(Name = "server_info", UriTemplate = "gitlab-mcp://server/info", MimeType = "application/json")]
    [Description(
        "This server's own runtime metadata: the active profile and the GitLab instance it targets. Never GitLab content.")]
    public ReadResourceResult GetServerInfo()
    {
        var info = new ServerInfoResource(
            mcpOptions.Value.ResolveProfile().ToString(),
            mcpOptions.Value.ResolveVersion(),
            clientOptions.Value.BaseAddress.AbsoluteUri,
            DateTimeOffset.UtcNow);

        return ToResult("gitlab-mcp://server/info",
            JsonSerializer.Serialize(info, GitLabJson.Options.GetTypeInfo<ServerInfoResource>()));
    }

    [McpServerResource(Name = "server_profiles", UriTemplate = "gitlab-mcp://server/profiles",
        MimeType = "application/json")]
    [Description(
        "Describes the four selectable server profiles and their intended persona/scope, mirroring CLAUDE.md's persona table.")]
    public static ReadResourceResult GetServerProfiles()
    {
        var profiles = new ServerProfilesResource(
        [
            new ProfileDescription("Maintainer", "Product Owner / Product Manager",
                "Planning: ideas, roadmap, milestones, epics, issues, tasks. Read-mostly on code."),
            new ProfileDescription("Developer", "Developer",
                "MR/PR lifecycle, epics, issues, tasks, and the code, group and project management that supports them."),
            new ProfileDescription("DevOps", "Platform / SRE",
                "Administration plus settings, CI/CD, runners, Terraform, and the surrounding infrastructure."),
            new ProfileDescription("FullPermission", "-",
                "Every tool from every profile, plus instance administration. Superset, not a separate surface.")
        ]);

        return ToResult("gitlab-mcp://server/profiles",
            JsonSerializer.Serialize(profiles, GitLabJson.Options.GetTypeInfo<ServerProfilesResource>()));
    }

    private static ReadResourceResult ToResult(string uri, string json)
    {
        return new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "application/json", Text = json }]
        };
    }
}