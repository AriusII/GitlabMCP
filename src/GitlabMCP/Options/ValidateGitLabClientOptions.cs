using GitLab.Client.DependencyInjection;
using GitlabMCP.GraphQL;
using Microsoft.Extensions.Options;

namespace GitlabMCP.Options;

/// <summary>
///     <c>[OptionsValidator]</c> can't help here: the source generator reads attributes on the target
///     class's OWN properties, and this project can't retroactively attribute a type <c>GitLab.Client</c>
///     owns. Hand-written, still fully AOT-safe (plain field comparisons, zero reflection).
/// </summary>
internal sealed class ValidateGitLabClientOptions : IValidateOptions<GitLabClientOptions>
{
    public ValidateOptionsResult Validate(string? name, GitLabClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessToken))
            // Names only the missing KEY, never a value — safe to surface verbatim in the startup log.
            return ValidateOptionsResult.Fail(
                "GitLab:AccessToken (env GitLab__AccessToken, or /run/secrets/GitLab__AccessToken) " +
                "is required and was not supplied.");

        if (options.BaseAddress is not { IsAbsoluteUri: true } baseAddress ||
            !baseAddress.AbsoluteUri.EndsWith('/'))
            return ValidateOptionsResult.Fail(
                $"GitLab:BaseAddress ('{options.BaseAddress}') must be an absolute URL ending in " +
                "'/api/v4/' (e.g. https://gitlab.example.com/api/v4/).");

        // A trailing slash alone (e.g. a self-hosted instance's bare root, "https://gitlab.example.com/")
        // passes the check above but is not a REST v4 base address — GitLabGraphQlEndpoint.Resolve encodes
        // the exact "ends in api/v4/" shape both the REST client and the GraphQL endpoint derivation need,
        // so reuse it here rather than re-deriving the same segment check a second time (adversarial review
        // finding, DEC-032): this turns a misconfiguration that used to fail lazily and unhelpfully on the
        // first epic/work-item tool call into a startup fail-fast with the same message either way.
        try
        {
            GitLabGraphQlEndpoint.Resolve(baseAddress);
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail($"GitLab:BaseAddress is invalid: {ex.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}