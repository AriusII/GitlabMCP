using System.ComponentModel;
using GitlabMCP.Abstractions;
using GitlabMCP.Contracts.Common;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     A canary tool with no GitLab dependency at all — proves the host, the profile gate, and the JSON
///     serialization chain end to end before any real GitLab-backed tool is added. Visible in every profile.
/// </summary>
[McpServerToolType]
public sealed class PingTools(IOptions<GitLabMcpOptions> mcpOptions)
{
    [McpServerTool(Name = "gitlab_ping", ReadOnly = true, OpenWorld = false)]
    [Description("Checks that the GitlabMCP server is running and reports the active profile. Takes no GitLab action.")]
    public PingResult Ping()
    {
        return new PingResult("ok", mcpOptions.Value.ResolveProfile().ToString(), DateTimeOffset.UtcNow);
    }
}