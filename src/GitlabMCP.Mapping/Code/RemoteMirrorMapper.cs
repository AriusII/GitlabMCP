using GitLab.Client.Models;
using GitlabMCP.Contracts.Code;

namespace GitlabMCP.Mapping.Code;

/// <summary>Projects push-mirror (<see cref="GitLabRemoteMirror" />) DTOs into the owned "code" records.</summary>
public static class RemoteMirrorMapper
{
    public static RemoteMirrorHostKeySummary ToSummary(GitLabMirrorHostKey key)
    {
        return new RemoteMirrorHostKeySummary(
            key.FingerprintSha256);
    }

    public static RemoteMirrorSummary ToSummary(GitLabRemoteMirror mirror)
    {
        return new RemoteMirrorSummary(
            mirror.Id,
            mirror.Enabled,
            mirror.Url,
            mirror.UpdateStatus,
            mirror.LastUpdateAt,
            mirror.LastUpdateStartedAt,
            mirror.LastSuccessfulUpdateAt,
            mirror.LastError,
            mirror.OnlyProtectedBranches,
            mirror.KeepDivergentRefs,
            mirror.AuthMethod,
            mirror.MirrorBranchRegex,
            mirror.HostKeys?.Select(ToSummary).ToList() ?? []);
    }
}