using GitLab.Client.Abstractions.Exceptions;
using GitlabMCP.GraphQL;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace GitlabMCP.Errors;

/// <summary>
///     One cross-cutting filter pair mapping both REST (<see cref="GitLabApiException" />) and GraphQL
///     (<see cref="GitLabGraphQlException" />, DEC-021) failures onto the same <c>gitlab_*</c> vocabulary — no
///     tool or resource body ever writes its own <c>catch</c>. Tools get a soft <c>CallToolResult.IsError</c>
///     (DEC-021's original shape); resources have no such field on <c>ReadResourceResult</c> (DEC-029), so
///     the same classification is instead thrown as an <see cref="McpException" />, whose message — unlike
///     an arbitrary exception's — is relayed to the client instead of collapsed to a generic redacted
///     "An error occurred." (mcp-untrusted-content's <c>McpException</c> vs. SDK-redacted-failure rule).
/// </summary>
public static class GitLabErrorMappingExtensions
{
    public static IMcpServerBuilder WithGitLabErrorMapping(this IMcpServerBuilder builder)
    {
        return builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next =>
                async (context, cancellationToken) =>
                {
                    try
                    {
                        return await next(context, cancellationToken);
                    }
                    catch (GitLabApiException ex)
                    {
                        return Fail(Describe(ex));
                    }
                    catch (GitLabGraphQlException ex)
                    {
                        return Fail(Describe(ex));
                    }
                    catch (HttpRequestException)
                    {
                        return Fail(
                            "gitlab_transport: could not reach the GitLab instance. Retry once; if it persists the server is misconfigured.");
                    }
                });

            filters.AddReadResourceFilter(next =>
                async (context, cancellationToken) =>
                {
                    try
                    {
                        return await next(context, cancellationToken);
                    }
                    catch (GitLabApiException ex)
                    {
                        throw new McpException(Describe(ex));
                    }
                    catch (GitLabGraphQlException ex)
                    {
                        throw new McpException(Describe(ex));
                    }
                    catch (HttpRequestException)
                    {
                        throw new McpException(
                            "gitlab_transport: could not reach the GitLab instance. Retry once; if it persists the server is misconfigured.");
                    }
                });
        });
    }

    private static string Describe(GitLabApiException ex)
    {
        return ex switch
        {
            GitLabAuthenticationException =>
                "gitlab_unauthenticated: the configured GitLab token is missing, expired or revoked. Do not retry.",
            GitLabForbiddenException =>
                "gitlab_forbidden: the configured GitLab token lacks the rights for this operation. Do not retry; a different token or scope is required.",
            GitLabNotFoundException =>
                "gitlab_not_found: not found, or not visible to the configured token. Check the id, then consider that it may exist but be invisible.",
            GitLabConflictException =>
                "gitlab_conflict: the resource changed or is in a state that forbids this operation. Re-read it before retrying.",
            GitLabValidationException v =>
                "gitlab_validation: GitLab rejected the request. Invalid fields: " + string.Join(", ", v.Errors.Keys) +
                ". Correct them and retry.",
            GitLabRateLimitExceededException r =>
                $"gitlab_rate_limited: retry after {(int)(r.RetryAfter?.TotalSeconds ?? 60)} seconds. Do not retry immediately.",
            GitLabServerException =>
                "gitlab_server_error: GitLab returned a server error. This is transient; retry once after a short delay.",
            _ => $"gitlab_error: GitLab returned HTTP {(int)ex.StatusCode}."
        };
    }

    // DEC-021: GraphQLError.Message is always free text (can echo caller-supplied variable content
    // verbatim) and is NEVER concatenated into this string — unlike GitLabValidationException.Errors'
    // field-name KEYS above, GraphQL has no safe/unsafe split within one error.
    private static string Describe(GitLabGraphQlException ex)
    {
        return ex switch
        {
            GitLabGraphQlAuthenticationException =>
                "gitlab_unauthenticated: the configured GitLab token is missing, expired or revoked. Do not retry.",
            GitLabGraphQlForbiddenException =>
                "gitlab_forbidden: the configured GitLab token lacks the rights for this operation. Do not retry; a different token or scope is required.",
            GitLabGraphQlNotFoundException =>
                "gitlab_not_found: not found, or not visible to the configured token. Check the id, then consider that it may exist but be invisible.",
            GitLabGraphQlRateLimitedException r =>
                $"gitlab_rate_limited: retry after {(int)(r.RetryAfter?.TotalSeconds ?? 60)} seconds. Do not retry immediately.",
            GitLabGraphQlValidationException =>
                "gitlab_validation: GitLab GraphQL rejected the request. Re-check the operation's inputs against its schema and retry.",
            GitLabGraphQlServerException =>
                "gitlab_server_error: GitLab returned a server error. This is transient; retry once after a short delay.",
            GitLabGraphQlUnavailableFeatureException u =>
                $"gitlab_feature_unavailable: this GitLab instance/namespace does not expose {u.FeatureName} (commonly a Premium/Ultimate-tier feature gate). Do not retry; this will not succeed here.",
            GitLabGraphQlTransportException =>
                "gitlab_transport: could not reach the GitLab GraphQL endpoint. Retry once; if it persists the server is misconfigured.",
            _ => "gitlab_graphql_error: GitLab GraphQL returned an unclassified error."
        };
    }

    private static CallToolResult Fail(string message)
    {
        return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = message }] };
    }
}