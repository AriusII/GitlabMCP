using System.ComponentModel.DataAnnotations;

namespace GitlabMCP.Abstractions;

/// <summary>
///     Binds <c>GitLabMcp:Profile</c> (DEC-012). Absent is valid (DEC-004: resolves to
///     <see cref="McpProfile.Maintainer" />) — only a present-but-unrecognised value fails validation. Lives
///     in Abstractions (not the host) so both the host's DI wiring and any tool that needs to know the
///     active profile (e.g. the canary <c>gitlab_ping</c>) can resolve it via <c>IOptions&lt;GitLabMcpOptions&gt;</c>
///     without a reference to host-only code.
/// </summary>
public sealed class GitLabMcpOptions
{
    public const string SectionName = "GitLabMcp";

    [RegularExpression(
        "^(?i:Maintainer|Developer|DevOps|FullPermission)$",
        ErrorMessage = "GitLabMcp:Profile ('{0}') must be one of Maintainer, Developer, DevOps, FullPermission.")]
    public string? Profile { get; set; }

    /// <summary>
    ///     Baked in at image build time as <c>ENV GitlabMcp__Version</c> (docker-aot-image §6/CI release
    ///     workflow) from the pushed tag — never set by an operator. Absent outside a released container
    ///     image (e.g. <c>dotnet run</c> on a dev box), in which case <see cref="GitLabMcpOptionsExtensions.ResolveVersion" />
    ///     falls back to <c>"dev"</c>. Free-form on purpose: unlike <see cref="Profile" /> there is nothing
    ///     to validate against — it is a label, not a switch.
    /// </summary>
    public string? Version { get; set; }
}

public static class GitLabMcpOptionsExtensions
{
    /// <summary>
    ///     DEC-004's exact contract: absent -> Maintainer; present-but-invalid is already blocked at startup
    ///     by <c>ValidateOnStart</c> (see the host's <c>Program.cs</c>), so
    ///     <see cref="Enum.Parse{TEnum}(string, bool)" /> here is safe by construction.
    /// </summary>
    public static McpProfile ResolveProfile(this GitLabMcpOptions options)
    {
        return string.IsNullOrWhiteSpace(options.Profile)
            ? McpProfile.Maintainer
            : Enum.Parse<McpProfile>(options.Profile, true);
    }

    /// <summary>The image's own release version, or <c>"dev"</c> outside a released container (see <see cref="GitLabMcpOptions.Version" />).</summary>
    public static string ResolveVersion(this GitLabMcpOptions options)
    {
        return string.IsNullOrWhiteSpace(options.Version) ? "dev" : options.Version;
    }
}