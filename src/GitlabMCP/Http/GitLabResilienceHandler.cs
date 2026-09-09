using System.Net;

namespace GitlabMCP.Http;

/// <summary>
///     Hand-rolled retry/circuit-breaker for both the REST (
///     <c>AddGitLabClient(...).AddHttpMessageHandler&lt;GitLabResilienceHandler&gt;()</c>)
///     and GraphQL (
///     <c>AddHttpClient&lt;IGitLabGraphQlClient,...&gt;(...).AddHttpMessageHandler&lt;GitLabResilienceHandler&gt;()</c>)
///     clients — same retry/backoff shape for both transports, chained via
///     <see cref="Microsoft.Extensions.DependencyInjection.IHttpClientBuilder" /> either way. Zero
///     third-party resilience dependency (DEC-021).
/// </summary>
public sealed partial class GitLabResilienceHandler(
    ILogger<GitLabResilienceHandler> logger,
    GitLabCircuitBreaker circuitBreaker) : DelegatingHandler
{
    private const int MaxRetries = 3;

    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (circuitBreaker.IsOpen)
        {
            // This is GitlabMCP's own synthetic response, not something GitLab sent — never wrap it as
            // GitLab-authored content if it ever surfaces through a tool.
            LogCircuitOpen(logger, request.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = "GitLab circuit open",
                RequestMessage = request
            };
        }

        for (var attempt = 0;; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(AttemptTimeout);
                response = await base.SendAsync(request, attemptCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                circuitBreaker.RecordFailure();
                if (attempt >= MaxRetries) throw;
                LogAttemptTimedOut(logger, attempt);
                await Task.Delay(Backoff[attempt], cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                circuitBreaker.RecordFailure();
                LogTransportFailure(logger, attempt, ex);
                await Task.Delay(Backoff[attempt], cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!ShouldRetry(request.Method, response.StatusCode) || attempt >= MaxRetries)
            {
                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                    circuitBreaker.RecordFailure();
                else
                    circuitBreaker.RecordSuccess();
                return response;
            }

            var delay = GetRetryDelay(response, Backoff[attempt]);
            LogRetryingAfterStatus(logger, attempt, (int)response.StatusCode, delay);
            response.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    // Only GET/HEAD (REST reads) and POST-to-the-GraphQL-endpoint's read-only queries are safe to
    // retry blindly; GitLab writes (REST POST/PUT/PATCH/DELETE, and any GraphQL mutation) are not
    // generally idempotent. Since this handler cannot distinguish a GraphQL query from a mutation (both
    // are POST /api/graphql), retry-on-status here is scoped to GET/HEAD only, and GraphQL callers rely
    // on the resilience of the underlying transport failure/timeout arms above instead.
    private static bool ShouldRetry(HttpMethod method, HttpStatusCode statusCode)
    {
        return (method == HttpMethod.Get || method == HttpMethod.Head) &&
               statusCode is HttpStatusCode.TooManyRequests
                   or HttpStatusCode.RequestTimeout
                   or HttpStatusCode.BadGateway
                   or HttpStatusCode.ServiceUnavailable
                   or HttpStatusCode.GatewayTimeout;
    }

    /// <summary>Honours GitLab's Retry-After (seconds, or an HTTP-date) over the fixed backoff.</summary>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, TimeSpan fallback)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return fallback;
        if (retryAfter.Delta is { } delta) return delta;
        if (retryAfter.Date is not { } date) return fallback;
        var wait = date - DateTimeOffset.UtcNow;
        return wait > TimeSpan.Zero
            ? wait
            : fallback;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitLab request attempt {Attempt} timed out; retrying.")]
    private static partial void LogAttemptTimedOut(ILogger logger, int attempt);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GitLab request attempt {Attempt} failed at the transport level; retrying.")]
    private static partial void LogTransportFailure(ILogger logger, int attempt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GitLab request attempt {Attempt} returned {StatusCode}; retrying after {Delay}.")]
    private static partial void LogRetryingAfterStatus(ILogger logger, int attempt, int statusCode, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitLab circuit open; short-circuiting call to {Path}.")]
    private static partial void LogCircuitOpen(ILogger logger, string? path);
}