using GitlabMCP.GraphQL;
using Xunit;

namespace GitlabMCP.Tests.GraphQL;

/// <summary>
///     DEC-020/021: deriving the GraphQL endpoint from the configured REST <c>BaseAddress</c> is exactly the
///     mechanism that makes self-hosted / custom-domain GitLab instances work — this is the one place that
///     logic lives, so it is worth pinning down against gitlab.com, a bare self-hosted domain, and a
///     path-prefixed self-hosted instance (all three documented as verified in the type's own doc comment).
/// </summary>
public class GitLabGraphQlEndpointTests
{
    [Fact]
    public void Resolve_GitLabDotCom_ReturnsRootGraphQlEndpoint()
    {
        var resolved = GitLabGraphQlEndpoint.Resolve(new Uri("https://gitlab.com/api/v4/"));

        Assert.Equal(new Uri("https://gitlab.com/api/graphql"), resolved);
    }

    [Fact]
    public void Resolve_SelfHostedCustomDomain_ReturnsRootGraphQlEndpoint()
    {
        var resolved = GitLabGraphQlEndpoint.Resolve(new Uri("https://gitlab.example.internal/api/v4/"));

        Assert.Equal(new Uri("https://gitlab.example.internal/api/graphql"), resolved);
    }

    [Fact]
    public void Resolve_SelfHostedWithPathPrefix_PreservesThePrefix()
    {
        var resolved = GitLabGraphQlEndpoint.Resolve(new Uri("https://example.com/gitlab/api/v4/"));

        Assert.Equal(new Uri("https://example.com/gitlab/api/graphql"), resolved);
    }

    [Fact]
    public void Resolve_NonStandardPort_IsPreserved()
    {
        var resolved = GitLabGraphQlEndpoint.Resolve(new Uri("https://gitlab.internal:8443/api/v4/"));

        Assert.Equal(new Uri("https://gitlab.internal:8443/api/graphql"), resolved);
    }

    [Theory]
    [InlineData("https://gitlab.com/api/v3/")]
    [InlineData("https://gitlab.com/api/v5/")]
    [InlineData("https://gitlab.com/")]
    public void Resolve_BaseAddressNotEndingInApiV4_Throws(string baseAddress)
    {
        Assert.Throws<InvalidOperationException>(() => GitLabGraphQlEndpoint.Resolve(new Uri(baseAddress)));
    }
}
