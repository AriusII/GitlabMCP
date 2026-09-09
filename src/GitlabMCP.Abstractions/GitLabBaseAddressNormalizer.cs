namespace GitlabMCP.Abstractions;

/// <summary>
///     Lets an operator set <c>GitLab:BaseAddress</c> to just the GitLab instance's own URL — the bare
///     domain (self-hosted or otherwise), with or without a path prefix, with or without a port — and
///     never have to know or type the trailing <c>api/v4/</c> that <c>GitLab.Client</c> and this server's
///     own GraphQL endpoint derivation (<c>GitLabGraphQlEndpoint.Resolve</c>) both require.
///     DEC-034: normalization runs once, in a single place, before validation ever sees the value — so
///     the common case (a bare self-hosted URL) always passes, and only a URL that already contains an
///     <c>api</c> segment pointing somewhere OTHER than <c>api/v4</c> (almost certainly a deliberate,
///     if wrong, configuration rather than a forgotten suffix) is left alone for
///     <c>ValidateGitLabClientOptions</c> to reject with a clear message instead of being silently
///     mangled into a nonsense double path.
/// </summary>
public static class GitLabBaseAddressNormalizer
{
    /// <summary>
    ///     Returns <paramref name="baseAddress" /> unchanged if it is not an absolute URI (nothing sensible
    ///     to normalize; validation will report that clearly) or if its path already contains an
    ///     <c>api</c> segment anywhere (assume the operator meant exactly what they typed). Otherwise
    ///     appends <c>api/v4/</c> to whatever path prefix was given — the empty prefix for a bare domain,
    ///     or the existing prefix for a path-prefixed self-hosted install — and always normalizes the
    ///     result to end in exactly one trailing slash.
    /// </summary>
    public static Uri Normalize(Uri baseAddress)
    {
        if (!baseAddress.IsAbsoluteUri)
            return baseAddress;

        var segments = baseAddress.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2 && segments[^1] == "v4" && segments[^2] == "api")
            return WithPath(baseAddress, segments); // already .../api/v4 — just normalize the trailing slash

        if (Array.IndexOf(segments, "api") >= 0)
            return baseAddress; // an "api" segment pointing somewhere else — leave it for validation to reject

        return WithPath(baseAddress, [.. segments, "api", "v4"]);
    }

    private static Uri WithPath(Uri baseAddress, string[] segments)
    {
        var path = "/" + string.Join('/', segments) + "/";
        return new UriBuilder(baseAddress) { Path = path, Query = "", Fragment = "" }.Uri;
    }
}
