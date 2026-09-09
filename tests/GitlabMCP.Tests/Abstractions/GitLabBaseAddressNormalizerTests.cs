using GitlabMCP.Abstractions;
using Xunit;

namespace GitlabMCP.Tests.Abstractions;

/// <summary>
///     DEC-034: an operator should be able to set <c>GitLab:BaseAddress</c> to just the GitLab instance's
///     own URL — gitlab.com, a bare self-hosted domain, or a path-prefixed self-hosted install — and never
///     have to know or type the trailing <c>api/v4/</c>.
/// </summary>
public class GitLabBaseAddressNormalizerTests
{
    [Theory]
    [InlineData("https://gitlab.com", "https://gitlab.com/api/v4/")]
    [InlineData("https://gitlab.com/", "https://gitlab.com/api/v4/")]
    [InlineData("https://gitlab.example.internal", "https://gitlab.example.internal/api/v4/")]
    [InlineData("https://example.com/gitlab", "https://example.com/gitlab/api/v4/")]
    [InlineData("https://example.com/gitlab/", "https://example.com/gitlab/api/v4/")]
    [InlineData("https://gitlab.internal:8443", "https://gitlab.internal:8443/api/v4/")]
    public void Normalize_BareUrlWithNoApiSegment_AppendsApiV4(string input, string expected)
    {
        var result = GitLabBaseAddressNormalizer.Normalize(new Uri(input));

        Assert.Equal(new Uri(expected), result);
    }

    [Theory]
    [InlineData("https://gitlab.com/api/v4")]
    [InlineData("https://gitlab.com/api/v4/")]
    [InlineData("https://example.com/gitlab/api/v4")]
    [InlineData("https://example.com/gitlab/api/v4/")]
    [InlineData("https://gitlab.internal:8443/api/v4/")]
    public void Normalize_AlreadyApiV4_IsIdempotent(string input)
    {
        var result = GitLabBaseAddressNormalizer.Normalize(new Uri(input));

        var expected = input.EndsWith('/') ? input : input + "/";
        Assert.Equal(new Uri(expected), result);
    }

    [Theory]
    [InlineData("https://gitlab.com/api/v3/")]
    [InlineData("https://gitlab.com/api/v5/")]
    [InlineData("https://gitlab.com/some/api/path/")]
    public void Normalize_PathAlreadyContainsAnApiSegmentButNotApiV4_IsLeftAlone(string input)
    {
        var uri = new Uri(input);

        var result = GitLabBaseAddressNormalizer.Normalize(uri);

        Assert.Equal(uri, result); // unchanged — ValidateGitLabClientOptions rejects this with a clear message
    }

    [Fact]
    public void Normalize_RelativeUri_IsLeftAlone()
    {
        var relative = new Uri("api/v4/", UriKind.Relative);

        var result = GitLabBaseAddressNormalizer.Normalize(relative);

        Assert.Same(relative, result);
    }
}
