namespace GitlabMCP.GraphQL;

/// <summary>
///     Derives the GraphQL endpoint from the REST client's own configured base address. DEC-020/021: walk one
///     segment up from <c>/api/v4/</c> — never append <c>graphql</c> onto it, which 404s in a way that reads
///     like a permissions problem. Verified to hold for <c>https://gitlab.com/api/v4/</c>, a self-hosted
///     <c>https://git.example.com/api/v4/</c>, and a path-prefixed <c>https://example.com/gitlab/api/v4/</c>.
/// </summary>
public static class GitLabGraphQlEndpoint
{
    public static Uri Resolve(Uri restApiV4BaseAddress)
    {
        var segments = restApiV4BaseAddress.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[^1] != "v4" || segments[^2] != "api")
            throw new InvalidOperationException(
                $"GitLabClientOptions.BaseAddress '{restApiV4BaseAddress}' does not end in '/api/v4/'; " +
                "cannot derive the GraphQL endpoint from it.");

        var prefix = string.Join('/', segments[..^2]); // everything before "api/v4" (empty for gitlab.com)
        var path = string.IsNullOrEmpty(prefix) ? "/api/graphql" : $"/{prefix}/api/graphql";

        return new UriBuilder(restApiV4BaseAddress) { Path = path, Query = "", Fragment = "" }.Uri;
    }
}