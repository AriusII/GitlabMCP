using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>
///     Projects <see cref="GitLabVariable" /> into the owned <see cref="VariableSummary" /> — never
///     <see cref="GitLabVariable.Value" /> (CLAUDE.md rule 5's closed secret-field list).
/// </summary>
public static class VariableMapper
{
    public static VariableSummary ToSummary(GitLabVariable variable)
    {
        return new VariableSummary(
            variable.Key,
            variable.VariableType,
            variable.Protected,
            variable.Masked,
            variable.Hidden,
            variable.Raw,
            variable.EnvironmentScope,
            variable.Description);
    }
}