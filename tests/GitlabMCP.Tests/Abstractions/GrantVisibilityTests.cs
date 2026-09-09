using GitlabMCP.Abstractions;
using Xunit;

namespace GitlabMCP.Tests.Abstractions;

/// <summary>
///     DEC-005/DEC-006/DEC-027: which persona(s) see a catalog row carrying a given <see cref="Grant" />.
///     Pure bitmask logic — no DI, no catalog, no network — so it is cheap to pin down exactly here rather
///     than only ever exercised indirectly through a live <c>tools/list</c> snapshot.
/// </summary>
public class GrantVisibilityTests
{
    [Theory]
    [InlineData(Grant.Maintainer, McpProfile.Maintainer, true)]
    [InlineData(Grant.Maintainer, McpProfile.Developer, false)]
    [InlineData(Grant.Maintainer, McpProfile.DevOps, false)]
    [InlineData(Grant.Planning, McpProfile.Maintainer, true)]
    [InlineData(Grant.Planning, McpProfile.Developer, true)]
    [InlineData(Grant.Planning, McpProfile.DevOps, false)]
    [InlineData(Grant.Delivery, McpProfile.Developer, true)]
    [InlineData(Grant.Delivery, McpProfile.DevOps, true)]
    [InlineData(Grant.Delivery, McpProfile.Maintainer, false)]
    [InlineData(Grant.Everyone, McpProfile.Maintainer, true)]
    [InlineData(Grant.Everyone, McpProfile.Developer, true)]
    [InlineData(Grant.Everyone, McpProfile.DevOps, true)]
    [InlineData(Grant.AdminOnly, McpProfile.Maintainer, false)]
    [InlineData(Grant.AdminOnly, McpProfile.Developer, false)]
    [InlineData(Grant.AdminOnly, McpProfile.DevOps, false)]
    public void IsVisibleIn_MatchesPersonaBits(Grant grant, McpProfile profile, bool expected)
    {
        Assert.Equal(expected, grant.IsVisibleIn(profile));
    }

    [Theory]
    [InlineData(Grant.Maintainer)]
    [InlineData(Grant.Developer)]
    [InlineData(Grant.DevOps)]
    [InlineData(Grant.AdminOnly)]
    [InlineData(Grant.Everyone)]
    [InlineData(Grant.Full)]
    public void IsVisibleIn_FullPermissionSeesEveryNonNoneGrant(Grant grant)
    {
        Assert.True(grant.IsVisibleIn(McpProfile.FullPermission));
    }

    [Fact]
    public void IsVisibleIn_None_IsVisibleInNoProfile()
    {
        Assert.False(Grant.None.IsVisibleIn(McpProfile.Maintainer));
        Assert.False(Grant.None.IsVisibleIn(McpProfile.Developer));
        Assert.False(Grant.None.IsVisibleIn(McpProfile.DevOps));
        Assert.False(Grant.None.IsVisibleIn(McpProfile.FullPermission));
    }

    [Fact]
    public void IsVisibleIn_UnknownProfile_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Grant.Full.IsVisibleIn((McpProfile)99));
    }
}
