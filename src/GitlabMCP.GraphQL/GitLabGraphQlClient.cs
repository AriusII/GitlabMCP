using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GitlabMCP.GraphQL;

public interface IGitLabGraphQlClient
{
    Task<GraphQLResponse<TData>> ExecuteAsync<TVariables, TData>(
        GraphQLRequest<TVariables> request, CancellationToken cancellationToken);
}

/// <summary>
///     Hand-rolled GraphQL-over-HTTP client (DEC-020/021 — neither <c>GraphQL.Client</c> nor StrawberryShake
///     is AOT-clean). Takes the process's single shared <see cref="JsonSerializerOptions" /> as a constructor
///     dependency and resolves each <see cref="JsonTypeInfo{T}" /> from it via the warning-free, non-generic
///     <see cref="JsonSerializerOptions.GetTypeInfo(Type)" /> — the same call
///     <c>McpJsonUtilities.GetTypeInfo&lt;T&gt;</c> wraps for REST payloads.
/// </summary>
public sealed class GitLabGraphQlClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IGitLabGraphQlClient
{
    private const long MaxResponseBytes = 8 * 1024 * 1024; // mcp-untrusted-content §6: cap total response bytes.

    public async Task<GraphQLResponse<TData>> ExecuteAsync<TVariables, TData>(
        GraphQLRequest<TVariables> request,
        CancellationToken cancellationToken)
    {
        var requestTypeInfo =
            (JsonTypeInfo<GraphQLRequest<TVariables>>)jsonOptions.GetTypeInfo(typeof(GraphQLRequest<TVariables>));
        var responseTypeInfo =
            (JsonTypeInfo<GraphQLResponse<TData>>)jsonOptions.GetTypeInfo(typeof(GraphQLResponse<TData>));

        using var content = new ReadOnlyMemoryContent(JsonSerializer.SerializeToUtf8Bytes(request, requestTypeInfo));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "") { Content = content };

        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired, not the caller's token.
            throw new GitLabGraphQlTransportException("gitlab_graphql request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GitLabGraphQlTransportException("gitlab_graphql transport failure.", ex);
        }

        using (response)
        {
            ThrowForTransportLevelStatus(response);

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var bounded = new BoundedReadStream(body, MaxResponseBytes);

            GraphQLResponse<TData> parsed;
            try
            {
                parsed = await JsonSerializer.DeserializeAsync(bounded, responseTypeInfo, cancellationToken)
                             .ConfigureAwait(false)
                         ?? throw new GitLabGraphQlServerException("gitlab_graphql returned an empty response body.");
            }
            catch (JsonException)
            {
                throw new GitLabGraphQlServerException("gitlab_graphql returned a body that was not valid JSON.");
            }

            return parsed;
        }
    }

    private static void ThrowForTransportLevelStatus(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var retryAfter = response.Headers.RetryAfter?.Delta;
        throw (int)response.StatusCode switch
        {
            401 => new GitLabGraphQlAuthenticationException("gitlab_graphql request was unauthenticated."),
            403 => new GitLabGraphQlForbiddenException("gitlab_graphql request was forbidden."),
            404 => new GitLabGraphQlNotFoundException(
                "gitlab_graphql endpoint not found — check GitLabClientOptions.BaseAddress."),
            429 => new GitLabGraphQlRateLimitedException(retryAfter, "gitlab_graphql request was rate limited."),
            >= 500 and <= 599 => new GitLabGraphQlServerException("gitlab_graphql returned a server error."),
            _ => new GitLabGraphQlServerException(
                $"gitlab_graphql returned unexpected HTTP {(int)response.StatusCode}.")
        };
    }
}