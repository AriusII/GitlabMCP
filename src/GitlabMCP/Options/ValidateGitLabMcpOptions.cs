using GitlabMCP.Abstractions;
using Microsoft.Extensions.Options;

namespace GitlabMCP.Options;

/// <summary>
///     [OptionsValidator] on an empty partial class: the source generator reads the DataAnnotations already
///     on <see cref="GitLabMcpOptions" /> and emits a reflection-free <see cref="IValidateOptions{TOptions}" />
///     implementation. No hand-written <c>Validate()</c> body.
/// </summary>
[OptionsValidator]
public sealed partial class ValidateGitLabMcpOptions : IValidateOptions<GitLabMcpOptions>;