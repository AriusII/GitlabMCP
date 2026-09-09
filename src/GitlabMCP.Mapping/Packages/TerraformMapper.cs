using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>
///     Projects <see cref="GitLabTerraformModuleVersionsEntry" /> into the owned
///     <see cref="TerraformModuleEntrySummary" />.
/// </summary>
public static class TerraformMapper
{
    public static TerraformModuleEntrySummary ToSummary(GitLabTerraformModuleVersionsEntry entry, int versionLimit)
    {
        var versions = entry.Versions ?? [];
        var truncated = versions.Count > versionLimit;

        var taken = new List<TerraformModuleVersionSummary>(Math.Min(versions.Count, versionLimit));
        for (var i = 0; i < versions.Count && i < versionLimit; i++)
        {
            var version = versions[i];
            taken.Add(new TerraformModuleVersionSummary(version.Version, version.Submodules?.Count ?? 0));
        }

        return new TerraformModuleEntrySummary(entry.Source?.ToString(), taken, truncated);
    }

    public static TerraformModuleSummary ToSummary(GitLabTerraformModule module, int versionLimit)
    {
        var versions = module.Versions ?? [];
        var versionsTruncated = versions.Count > versionLimit;
        var boundedVersions = versionsTruncated
            ? versions.Take(versionLimit).ToList()
            : versions;

        return new TerraformModuleSummary(
            module.Name,
            module.Provider,
            module.Providers ?? [],
            module.Version,
            boundedVersions,
            module.Source?.ToString(),
            module.Root?.Dependencies?.Count ?? 0,
            module.Root?.Providers?.Select(p => $"{p.Name}@{p.Version}").ToList() ?? [],
            versionsTruncated);
    }
}