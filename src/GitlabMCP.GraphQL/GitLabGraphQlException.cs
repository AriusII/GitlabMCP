namespace GitlabMCP.GraphQL;

/// <summary>Parallel to <c>GitLabApiException</c> (REST) — feeds the same call-tool error-mapping filter (DEC-021).</summary>
public abstract class GitLabGraphQlException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class GitLabGraphQlAuthenticationException(string message) : GitLabGraphQlException(message);

public sealed class GitLabGraphQlForbiddenException(string message) : GitLabGraphQlException(message);

public sealed class GitLabGraphQlNotFoundException(string message) : GitLabGraphQlException(message);

public sealed class GitLabGraphQlRateLimitedException(TimeSpan? retryAfter, string message)
    : GitLabGraphQlException(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>
///     One or more GraphQL-envelope errors (HTTP 200, non-empty <c>errors</c>). Unlike
///     <c>GitLabValidationException.Errors</c> on the REST side, GraphQL has no separate "safe field name"
///     key — <see cref="GraphQLError.Message" /> is always free text (DEC-021) and must never be concatenated
///     into a plain error string; only <see cref="GraphQLError.PathString" /> is safe to relay unconditionally.
/// </summary>
public sealed class GitLabGraphQlValidationException(IReadOnlyList<GraphQLError> errors, string message)
    : GitLabGraphQlException(message)
{
    public IReadOnlyList<GraphQLError> Errors { get; } = errors;
}

public sealed class GitLabGraphQlServerException(string message) : GitLabGraphQlException(message);

public sealed class GitLabGraphQlTransportException(string message, Exception inner)
    : GitLabGraphQlException(message, inner);

/// <summary>
///     A field that should be non-null on success came back null with no matching <c>errors</c> entry —
///     GitLab's documented behaviour for a Premium/Ultimate-gated field (epics) accessed on a Free instance:
///     it does not 403, it resolves to null. DEC-020/021 requires this be surfaced as a tier problem, never a
///     null payload the model has to guess about.
/// </summary>
public sealed class GitLabGraphQlUnavailableFeatureException(string featureName, string message)
    : GitLabGraphQlException(message)
{
    public string FeatureName { get; } = featureName;
}