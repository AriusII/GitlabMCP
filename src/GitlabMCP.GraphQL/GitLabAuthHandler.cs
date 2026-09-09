using System.Net.Http.Headers;
using GitLab.Client.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GitlabMCP.GraphQL;

/// <summary>
///     Reads the SAME <see cref="GitLabClientOptions" /> the REST client (<c>AddGitLabClient</c>) was
///     configured with, and stamps the identical auth header GitLab.Client sends for the REST API — so the
///     GraphQL and REST calls authenticate as the same token by construction, never a second credential
///     (DEC-015 single-tenant; DEC-020/021).
/// </summary>
public sealed class GitLabAuthHandler(IOptions<GitLabClientOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var o = options.Value;
        switch (o.AuthenticationMode)
        {
            case GitLabAuthenticationMode.PersonalAccessToken:
                request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", o.AccessToken);
                break;
            case GitLabAuthenticationMode.OAuthBearer:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
                break;
            case GitLabAuthenticationMode.JobToken:
                request.Headers.TryAddWithoutValidation("JOB-TOKEN", o.AccessToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unhandled {nameof(GitLabAuthenticationMode)}: {o.AuthenticationMode}.");
        }

        return base.SendAsync(request, cancellationToken);
    }
}