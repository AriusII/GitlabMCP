using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class DeployTokenMapper
{
    public static DeployTokenSummary ToSummary(GitLabDeployToken token)
    {
        return new DeployTokenSummary(
            token.Id,
            token.Name,
            token.Username,
            token.ExpiresAt,
            token.Scopes ?? [],
            token.Revoked,
            token.Expired);
    }

    /// <summary>
    ///     Deliberately keeps <see cref="GitLabDeployTokenWithSecret.Token" /> — see
    ///     <see cref="DeployTokenCreated" />'s own doc for why this is a reviewed exception, not an oversight.
    /// </summary>
    public static DeployTokenCreated ToCreated(GitLabDeployTokenWithSecret token)
    {
        return new DeployTokenCreated(
            token.Id,
            token.Name,
            token.Username,
            token.Token ??
            throw new InvalidOperationException("GitLab did not return the deploy token's plaintext secret."),
            token.ExpiresAt,
            token.Scopes ?? []);
    }
}