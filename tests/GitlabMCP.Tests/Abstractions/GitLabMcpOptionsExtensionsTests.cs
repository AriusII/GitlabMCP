using GitlabMCP.Abstractions;
using Xunit;

namespace GitlabMCP.Tests.Abstractions;

/// <summary>DEC-004's fallback contract: absent -&gt; Maintainer, present-but-valid resolves case-insensitively.</summary>
public class GitLabMcpOptionsExtensionsTests
{
    [Fact]
    public void ResolveProfile_WhenProfileIsNull_DefaultsToMaintainer()
    {
        var options = new GitLabMcpOptions { Profile = null };

        Assert.Equal(McpProfile.Maintainer, options.ResolveProfile());
    }

    [Fact]
    public void ResolveProfile_WhenProfileIsWhitespace_DefaultsToMaintainer()
    {
        var options = new GitLabMcpOptions { Profile = "   " };

        Assert.Equal(McpProfile.Maintainer, options.ResolveProfile());
    }

    [Theory]
    [InlineData("Maintainer", McpProfile.Maintainer)]
    [InlineData("developer", McpProfile.Developer)]
    [InlineData("DEVOPS", McpProfile.DevOps)]
    [InlineData("FullPermission", McpProfile.FullPermission)]
    [InlineData("fullpermission", McpProfile.FullPermission)]
    public void ResolveProfile_IsCaseInsensitive(string configured, McpProfile expected)
    {
        var options = new GitLabMcpOptions { Profile = configured };

        Assert.Equal(expected, options.ResolveProfile());
    }
}
