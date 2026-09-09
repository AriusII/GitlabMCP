namespace GitlabMCP.Contracts.Common;

/// <summary>
///     Every field is a server-known scalar (DEC-007's all-scalar exception) — no GitLab call, no GitLab
///     text, so this is the rare bare-record return type rather than a <c>GitLabContent.Wrap</c>.
/// </summary>
public sealed record PingResult(string Status, string Profile, DateTimeOffset ServerTimeUtc);