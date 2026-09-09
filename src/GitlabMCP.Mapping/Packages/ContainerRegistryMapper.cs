using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>Projects <see cref="GitLabRegistryRepository" /> into the owned <see cref="ContainerRepositorySummary" />.</summary>
public static class ContainerRegistryMapper
{
    public static ContainerRepositorySummary ToSummary(GitLabRegistryRepository repository)
    {
        return new ContainerRepositorySummary(
            repository.Id,
            repository.Name,
            repository.Path,
            repository.ProjectId,
            repository.Location,
            repository.CreatedAt,
            repository.TagsCount,
            repository.Size,
            repository.Status);
    }

    /// <summary>
    ///     Projects <see cref="GitLabRegistryRepositoryTag" /> into the owned
    ///     <see cref="ContainerRepositoryTagSummary" />.
    /// </summary>
    public static ContainerRepositoryTagSummary ToSummary(GitLabRegistryRepositoryTag tag)
    {
        return new ContainerRepositoryTagSummary(
            tag.Name,
            tag.Path,
            tag.Location);
    }

    /// <summary>
    ///     Projects <see cref="GitLabRegistryRepositoryTagDetails" /> into the owned
    ///     <see cref="ContainerRepositoryTagDetail" />.
    /// </summary>
    public static ContainerRepositoryTagDetail ToDetail(GitLabRegistryRepositoryTagDetails tag)
    {
        return new ContainerRepositoryTagDetail(
            tag.Name,
            tag.Path,
            tag.Location,
            tag.Revision,
            tag.ShortRevision,
            tag.Digest,
            tag.CreatedAt,
            tag.TotalSize);
    }
}