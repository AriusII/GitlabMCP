using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Packages;
using GitlabMCP.Mapping.Packages;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Package registries and container registry tools (domain "packages") — DevOps-only per
///     CLAUDE.md's persona table (<c>ContainerRegistry</c>, <c>Packages*</c>). Every read here returns
///     GitLab-authored text (package/version/pattern names, registry paths, distribution codenames,
///     checksums, digests, …), so every tool declares <c>Task&lt;CallToolResult&gt;</c> and wraps via
///     <see cref="GitLabContent" /> — the delete/purge/bulk-delete tools are the exception: their result is
///     only an echoed numeric id (or, for the Debian distribution delete, the caller's own codename
///     argument echoed back) plus a bool, with no GitLab-*authored* string, so they return their
///     projection record bare (DEC-007).
/// </summary>
[McpServerToolType]
public sealed class PackagesTools(
    IPackagesGenericClient packagesGeneric,
    IContainerRegistryClient containerRegistry,
    IPackagesDebianClient packagesDebian,
    IPackagesTerraformModulesClient terraformModules,
    IProjectPackageProtectionRulesClient packageProtectionRules,
    IProjectContainerRegistryProtectionRulesClient containerRegistryProtectionRules,
    IProjectContainerRegistryProtectionTagRulesClient containerRegistryProtectionTagRules,
    IDependencyProxyClient dependencyProxy)
{
    private const int MaxLimit = 100;

    /// <summary>
    ///     Upper bound on a base64-encoded file body accepted or returned in one MCP round trip (upload and
    ///     download alike) — mirrors <c>InfraTools.MaxAttestationBytes</c>'s reasoning: refuse rather than
    ///     silently truncate a binary artifact past the point a caller could still trust the bytes.
    /// </summary>
    private const int MaxFileBytes = 4 * 1024 * 1024;

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_packages", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every package published under a group, across every project inside it and every package format (npm, Maven, NuGet, generic, …) — an org-wide 'what have we published' sweep. For one project's packages only, use a project-scoped package listing tool instead.")]
    public async Task<CallToolResult> ListGroupPackagesAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description(
            "Optional package format filter: one of \"maven\", \"npm\", \"conan\", \"nuget\", \"pypi\", \"composer\", \"generic\", \"golang\", \"debian\", \"rubygems\", \"helm\", \"terraform_module\". Omit for every format.")]
        string? packageType = null,
        [Description("Optional exact package name filter. Omit to match every package name.")]
        string? packageName = null,
        [Description("Maximum packages to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new GroupPackageListOptions
        {
            PackageType = ParsePackageType(packageType),
            PackageName = packageName,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PackageSummary> collected = [];
        var truncated = false;

        await foreach (var package in packagesGeneric.ListPackagesForGroupAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PackageMapper.ToSummary(package));
        }

        return GitLabContent.Wrap(new PackageListResult(collected, truncated), "groups/:id/packages");
    }

    [McpServerTool(Name = "gitlab_list_container_repositories", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's container image repositories, to see what Docker/OCI images the project publishes to its GitLab container registry.")]
    public async Task<CallToolResult> ListContainerRepositoriesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "If true (default), ask GitLab to include each repository's tag count. Set false to skip that extra cost.")]
        bool includeTagsCount = true,
        [Description("Maximum repositories to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new RegistryRepositoryListOptions
        {
            TagsCount = includeTagsCount,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ContainerRepositorySummary> collected = [];
        var truncated = false;

        await foreach (var repository in containerRegistry.ListForProjectAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ContainerRegistryMapper.ToSummary(repository));
        }

        return GitLabContent.Wrap(new ContainerRepositoryListResult(collected, truncated),
            "projects/:id/registry/repositories");
    }

    [McpServerTool(Name = "gitlab_list_debian_distributions", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's Debian (APT) repository distributions/codenames, e.g. to see which suites (stable, bullseye-security) are configured.")]
    public async Task<CallToolResult> ListDebianDistributionsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum distributions to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new DebianDistributionListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<DebianDistributionSummary> collected = [];
        var truncated = false;

        await foreach (var distribution in packagesDebian.ListDistributionsForProjectAsync(project, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DebianMapper.ToSummary(distribution));
        }

        return GitLabContent.Wrap(new DebianDistributionListResult(collected, truncated),
            "projects/:id/debian_distributions");
    }

    [McpServerTool(Name = "gitlab_list_terraform_module_versions", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists every published version of a Terraform module in the group-level Terraform module registry, with its dependency metadata, e.g. to decide which version a consumer should pin to.")]
    public async Task<CallToolResult> ListTerraformModuleVersionsAsync(
        [Description("The module's owning group: numeric group id or full path (the registry protocol's 'namespace').")]
        string group,
        [Description("The module name (the registry protocol's 'name'), e.g. \"my-module\".")]
        string moduleName,
        [Description(
            "The module system/provider (the registry protocol's 'system'), e.g. \"aws\", \"local\", \"null\".")]
        string moduleSystem,
        [Description("Maximum versions to return per module entry (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var result = await terraformModules.ListModuleVersionsAsync(group, moduleName, moduleSystem, cancellationToken);
        var modules = result.Modules ?? [];

        var collected = new List<TerraformModuleEntrySummary>(Math.Min(modules.Count, limit));
        for (var i = 0; i < modules.Count && i < limit; i++)
            collected.Add(TerraformMapper.ToSummary(modules[i], limit));

        // The registry protocol wraps a single module's version list in effectively one module
        // entry (see TerraformModuleEntrySummary's doc comment), so comparing modules.Count against
        // limit would almost never reflect real truncation. What actually gets truncated is each
        // entry's nested Versions list — TerraformMapper.ToSummary already computes that per entry
        // from versions.Count > limit, so the top-level flag mirrors it here.
        var truncated = collected.Any(entry => entry.VersionsTruncated);

        return GitLabContent.Wrap(new TerraformModuleVersionListResult(collected, truncated),
            "groups/:id/-/packages/terraform/modules/:module-name/:module-system/versions");
    }

    [McpServerTool(Name = "gitlab_list_package_protection_rules", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a project's package protection rules — which package name patterns, by format, only Maintainer+ (or Owner+ for delete) may push or delete.")]
    public async Task<CallToolResult> ListPackageProtectionRulesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum rules to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<PackageProtectionRuleSummary> collected = [];
        var truncated = false;

        await foreach (var rule in packageProtectionRules.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectionRuleMapper.ToSummary(rule));
        }

        return GitLabContent.Wrap(new PackageProtectionRuleListResult(collected, truncated),
            "projects/:id/packages/protection/rules");
    }

    [McpServerTool(Name = "gitlab_list_container_registry_protection_rules", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a project's container repository protection rules — which image repository path patterns only Maintainer+/Owner+ may push to or delete from.")]
    public async Task<CallToolResult> ListContainerRegistryProtectionRulesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum rules to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ContainerRegistryProtectionRuleSummary> collected = [];
        var truncated = false;

        await foreach (var rule in containerRegistryProtectionRules.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectionRuleMapper.ToSummary(rule));
        }

        return GitLabContent.Wrap(new ContainerRegistryProtectionRuleListResult(collected, truncated),
            "projects/:id/registry/protection/repository/rules");
    }

    [McpServerTool(Name = "gitlab_list_packages", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Browses every package one project holds, across all 12 formats (npm, NuGet, Maven, Conan, PyPI, Debian, RubyGems, Composer, Golang, Generic, Helm, TerraformModule), with name/version/type/status filters — the primary package-registry discovery tool for one project. For an org-wide, group-scoped sweep use gitlab_list_group_packages instead.")]
    public async Task<CallToolResult> ListPackagesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Optional package format filter: one of \"maven\", \"npm\", \"conan\", \"nuget\", \"pypi\", \"composer\", \"generic\", \"golang\", \"debian\", \"rubygems\", \"helm\", \"terraform_module\". Omit for every format.")]
        string? packageType = null,
        [Description("Optional exact package name filter. Omit to match every package name.")]
        string? packageName = null,
        [Description("Optional exact package version filter. Omit to match every version.")]
        string? packageVersion = null,
        [Description(
            "Optional status filter: one of \"default\", \"hidden\", \"processing\", \"error\", \"pending_destruction\", \"deprecated\". Omit for every status.")]
        string? status = null,
        [Description("Maximum packages to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new PackageListOptions
        {
            PackageType = ParsePackageType(packageType),
            PackageName = packageName,
            PackageVersion = packageVersion,
            Status = ParsePackageStatus(status),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PackageSummary> collected = [];
        var truncated = false;

        await foreach (var package in packagesGeneric.ListPackagesAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PackageMapper.ToSummary(package));
        }

        return GitLabContent.Wrap(new PackageListResult(collected, truncated), "projects/:id/packages");
    }

    [McpServerTool(Name = "gitlab_get_package", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one package's full detail — version, status, creator, publishing pipeline — by its numeric id, regardless of format.")]
    public async Task<CallToolResult> GetPackageAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package's numeric id, from gitlab_list_packages or gitlab_list_group_packages.")]
        long packageId,
        CancellationToken cancellationToken = default)
    {
        var package = await packagesGeneric.GetPackageAsync(project, packageId, cancellationToken);
        return GitLabContent.Wrap(PackageMapper.ToDetail(package), "projects/:id/packages/:package_id");
    }

    [McpServerTool(Name = "gitlab_list_package_files", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the individual files that make up one package version, with size and checksums, to inspect what was actually published.")]
    public async Task<CallToolResult> ListPackageFilesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package's numeric id, from gitlab_list_packages or gitlab_get_package.")]
        long packageId,
        [Description("Maximum files to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new PackageFileListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PackageFileSummary> collected = [];
        var truncated = false;

        await foreach (var file in
                       packagesGeneric.ListPackageFilesAsync(project, packageId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PackageFileMapper.ToSummary(file));
        }

        return GitLabContent.Wrap(new PackageFileListResult(collected, truncated),
            "projects/:id/packages/:package_id/package_files");
    }

    [McpServerTool(Name = "gitlab_list_package_pipelines", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Traces a published package back to the CI pipeline(s) that built it, newest first — provenance/audit for a given package version.")]
    public async Task<CallToolResult> ListPackagePipelinesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package's numeric id, from gitlab_list_packages or gitlab_get_package.")]
        long packageId,
        [Description("Maximum pipelines to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new PackagePipelineListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<PackagePipelineSummary> collected = [];
        var truncated = false;

        await foreach (var pipeline in packagesGeneric.ListPackagePipelinesAsync(project, packageId, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PackagePipelineMapper.ToSummary(pipeline));
        }

        return GitLabContent.Wrap(new PackagePipelineListResult(collected, truncated),
            "projects/:id/packages/:package_id/pipelines");
    }

    [McpServerTool(Name = "gitlab_download_generic_package_file", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Downloads a previously published generic package file's raw bytes, base64-encoded — e.g. to inspect a release artifact. Refuses files over 4 MB rather than returning a truncated, unusable partial file; fetch large files directly from GitLab instead.")]
    public async Task<CallToolResult> DownloadGenericPackageFileAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The generic package's name, e.g. \"build-artifacts\".")]
        string packageName,
        [Description("The package version, e.g. \"1.0.0\".")]
        string packageVersion,
        [Description("The file name to download, exactly as it was published.")]
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await using var file = await packagesGeneric.DownloadGenericPackageFileAsync(project, packageName,
            packageVersion, fileName, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxFileBytes)
            throw new McpException(
                $"Package file is {declaredLength} bytes, over the {MaxFileBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var buffer = new byte[MaxFileBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxFileBytes)
            throw new McpException(
                $"Package file exceeds the {MaxFileBytes}-byte limit this tool can return. Fetch it directly from GitLab instead.");

        var payload = new GenericPackageFileDownload(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/packages/generic/:package_name/:package_version/:file_name");
    }

    [McpServerTool(Name = "gitlab_list_group_container_repositories", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists every container image repository across all projects in a group — an org-wide image inventory. For one project's repositories only, use gitlab_list_container_repositories instead.")]
    public async Task<CallToolResult> ListGroupContainerRepositoriesAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Maximum repositories to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new GroupRegistryRepositoryListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ContainerRepositorySummary> collected = [];
        var truncated = false;

        await foreach (var repository in containerRegistry.ListForGroupAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ContainerRegistryMapper.ToSummary(repository));
        }

        return GitLabContent.Wrap(new ContainerRepositoryListResult(collected, truncated),
            "groups/:id/registry/repositories");
    }

    [McpServerTool(Name = "gitlab_get_container_repository", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one container repository's detail — location, size, tag count — by its own global id, when the caller already knows the repository id rather than the project.")]
    public async Task<CallToolResult> GetContainerRepositoryAsync(
        [Description(
            "The container repository's own numeric id (not the project id), from gitlab_list_container_repositories or gitlab_list_group_container_repositories.")]
        long repositoryId,
        [Description("If true (default), ask GitLab to include the repository's tag count in the result.")]
        bool includeTagsCount = true,
        [Description("If true (default), ask GitLab to include the repository's total size in the result.")]
        bool includeSize = true,
        CancellationToken cancellationToken = default)
    {
        var options = new RegistryRepositoryGetOptions
        {
            TagsCount = includeTagsCount,
            Size = includeSize
        };

        var repository = await containerRegistry.GetAsync(repositoryId, options, cancellationToken);
        return GitLabContent.Wrap(ContainerRegistryMapper.ToSummary(repository), "registry/repositories/:id");
    }

    [McpServerTool(Name = "gitlab_list_container_repository_tags", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists the tags pushed to one container repository, to see what versions of an image exist before deciding what to clean up or deploy.")]
    public async Task<CallToolResult> ListContainerRepositoryTagsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The container repository's numeric id, from gitlab_list_container_repositories.")]
        long repositoryId,
        [Description("Maximum tags to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new RegistryRepositoryTagListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ContainerRepositoryTagSummary> collected = [];
        var truncated = false;

        await foreach (var tag in containerRegistry.ListTagsAsync(project, repositoryId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ContainerRegistryMapper.ToSummary(tag));
        }

        return GitLabContent.Wrap(new ContainerRepositoryTagListResult(collected, truncated),
            "projects/:id/registry/repositories/:repository_id/tags");
    }

    [McpServerTool(Name = "gitlab_get_container_repository_tag", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Gets one image tag's full detail — digest, total size, revision — e.g. to confirm exactly which build a tag points at before promoting or deleting it.")]
    public async Task<CallToolResult> GetContainerRepositoryTagAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The container repository's numeric id, from gitlab_list_container_repositories.")]
        long repositoryId,
        [Description("The tag's name, e.g. \"latest\" or \"v1.2.3\".")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        var tag = await containerRegistry.GetTagAsync(project, repositoryId, tagName, cancellationToken);
        return GitLabContent.Wrap(ContainerRegistryMapper.ToDetail(tag),
            "projects/:id/registry/repositories/:repository_id/tags/:tag_name");
    }

    [McpServerTool(Name = "gitlab_list_container_registry_protection_tag_rules", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a project's container image tag protection rules -- which tag name patterns (e.g. \"v*\", \"prod\") only Maintainer+/Owner+ may push or delete. Distinct from gitlab_list_container_registry_protection_rules, which protects repository path patterns rather than tag patterns within a repository.")]
    public async Task<CallToolResult> ListContainerRegistryProtectionTagRulesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum rules to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ContainerRegistryProtectionTagRuleSummary> collected = [];
        var truncated = false;

        await foreach (var rule in containerRegistryProtectionTagRules.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectionRuleMapper.ToSummary(rule));
        }

        return GitLabContent.Wrap(new ContainerRegistryProtectionTagRuleListResult(collected, truncated),
            "projects/:id/registry/protection/tag/rules");
    }

    [McpServerTool(Name = "gitlab_get_debian_distribution", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one project Debian (APT) distribution's full configuration by codename -- suite, origin, retention window, components and architectures.")]
    public async Task<CallToolResult> GetDebianDistributionAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The distribution's codename, e.g. \"bullseye\" or \"stable\".")]
        string codename,
        CancellationToken cancellationToken = default)
    {
        var distribution = await packagesDebian.GetDistributionForProjectAsync(project, codename, cancellationToken);
        return GitLabContent.Wrap(DebianMapper.ToSummary(distribution), "projects/:id/debian_distributions/:codename");
    }

    [McpServerTool(Name = "gitlab_get_debian_distribution_key", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets a project Debian distribution's signing-key metadata by codename, in the same shape as gitlab_get_debian_distribution -- use it when checking a distribution's key-related configuration before handing an apt client its trust chain.")]
    public async Task<CallToolResult> GetDebianDistributionKeyAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The distribution's codename, e.g. \"bullseye\".")]
        string codename,
        CancellationToken cancellationToken = default)
    {
        var distribution = await packagesDebian.GetDistributionKeyForProjectAsync(project, codename, cancellationToken);
        return GitLabContent.Wrap(DebianMapper.ToSummary(distribution),
            "projects/:id/debian_distributions/:codename/key");
    }

    [McpServerTool(Name = "gitlab_list_group_debian_distributions", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Lists Debian (APT) distributions configured at group scope, shared across the group's projects.")]
    public async Task<CallToolResult> ListGroupDebianDistributionsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Maximum distributions to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new DebianDistributionListOptions
        {
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<DebianDistributionSummary> collected = [];
        var truncated = false;

        await foreach (var distribution in packagesDebian.ListDistributionsForGroupAsync(group, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DebianMapper.ToSummary(distribution));
        }

        return GitLabContent.Wrap(new DebianDistributionListResult(collected, truncated),
            "groups/:id/debian_distributions");
    }

    [McpServerTool(Name = "gitlab_get_group_debian_distribution", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Gets one group-scoped Debian distribution's configuration by codename.")]
    public async Task<CallToolResult> GetGroupDebianDistributionAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("The distribution's codename, e.g. \"bullseye\".")]
        string codename,
        CancellationToken cancellationToken = default)
    {
        var distribution = await packagesDebian.GetDistributionForGroupAsync(group, codename, cancellationToken);
        return GitLabContent.Wrap(DebianMapper.ToSummary(distribution), "groups/:id/debian_distributions/:codename");
    }

    [McpServerTool(Name = "gitlab_get_terraform_module", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Looks up the latest published version of a Terraform module by its group namespace, name and provider system -- 'what does our module registry currently publish for aws/vpc'.")]
    public async Task<CallToolResult> GetTerraformModuleAsync(
        [Description("The module's owning group: numeric group id or full path.")]
        string group,
        [Description("The module name, e.g. \"my-module\".")]
        string moduleName,
        [Description("The module system/provider, e.g. \"aws\", \"local\", \"null\".")]
        string moduleSystem,
        [Description("Maximum published versions to include from the module's full version list (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var module = await terraformModules.GetModuleAsync(group, moduleName, moduleSystem, cancellationToken);
        return GitLabContent.Wrap(TerraformMapper.ToSummary(module, limit),
            "groups/:id/-/packages/terraform/modules/:module-name/:module-system");
    }

    [McpServerTool(Name = "gitlab_get_terraform_module_version", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Gets the full metadata (submodule/provider-dependency counts, source) for one specific published version of a Terraform module.")]
    public async Task<CallToolResult> GetTerraformModuleVersionAsync(
        [Description("The module's owning group: numeric group id or full path.")]
        string group,
        [Description("The module name, e.g. \"my-module\".")]
        string moduleName,
        [Description("The module system/provider, e.g. \"aws\", \"local\", \"null\".")]
        string moduleSystem,
        [Description("The version to retrieve, e.g. \"1.0.0\".")]
        string moduleVersion,
        [Description("Maximum published versions to include from the module's full version list (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var module =
            await terraformModules.GetModuleVersionAsync(group, moduleName, moduleSystem, moduleVersion,
                cancellationToken);
        return GitLabContent.Wrap(TerraformMapper.ToSummary(module, limit),
            "groups/:id/-/packages/terraform/modules/:module-name/:module-system/:module-version");
    }

    // ---------------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_package", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently removes a package and every file it owns from the registry, regardless of format. This cannot be undone — e.g. purging a bad or leaked release.")]
    public async Task<PackageDeleteResult> DeletePackageAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package's numeric id, from gitlab_list_group_packages or the GitLab UI.")]
        long packageId,
        CancellationToken cancellationToken = default)
    {
        await packagesGeneric.DeletePackageAsync(project, packageId, cancellationToken);
        return new PackageDeleteResult(packageId, true);
    }

    [McpServerTool(Name = "gitlab_delete_container_repository", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Permanently deletes an entire container repository and every image tag it holds — decommissioning an image entirely. This cannot be undone.")]
    public async Task<ContainerRepositoryDeleteResult> DeleteContainerRepositoryAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The container repository's numeric id, from gitlab_list_container_repositories.")]
        long repositoryId,
        CancellationToken cancellationToken = default)
    {
        await containerRegistry.DeleteRepositoryAsync(project, repositoryId, cancellationToken);
        return new ContainerRepositoryDeleteResult(repositoryId, true);
    }

    [McpServerTool(Name = "gitlab_create_package_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Locks down a package name pattern (e.g. \"release-*\" for npm) so only roles at or above a chosen level can push or delete matching packages.")]
    public async Task<CallToolResult> CreatePackageProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package name pattern to protect, supporting a trailing \"*\" wildcard, e.g. \"release-*\".")]
        string packageNamePattern,
        [Description(
            "The package format the pattern applies to: one of \"cargo\", \"conan\", \"generic\", \"helm\", \"maven\", \"npm\", \"nuget\", \"pypi\", \"terraform_module\".")]
        string packageType,
        [Description(
            "Optional minimum role required to push a matching package: \"maintainer\", \"owner\", or \"admin\". Omit to leave GitLab's default.")]
        string? minimumAccessLevelForPush = null,
        [Description(
            "Optional minimum role required to delete a matching package: \"owner\" or \"admin\". Omit to leave GitLab's default.")]
        string? minimumAccessLevelForDelete = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePackageProtectionRuleRequest
        {
            PackageNamePattern = packageNamePattern,
            PackageType = ParseProtectionRuleType(packageType),
            MinimumAccessLevelForPush = ParsePushAccessLevel(minimumAccessLevelForPush),
            MinimumAccessLevelForDelete = ParseDeleteAccessLevel(minimumAccessLevelForDelete)
        };

        var rule = await packageProtectionRules.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/packages/protection/rules (create)");
    }

    [McpServerTool(Name = "gitlab_create_container_registry_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Protects a container repository path pattern so only roles at or above a chosen level can push images to or delete images from repositories matching it.")]
    public async Task<CallToolResult> CreateContainerRegistryProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "The image repository path pattern to protect, supporting a trailing \"*\" wildcard, e.g. \"my-project/prod-*\".")]
        string repositoryPathPattern,
        [Description(
            "Optional minimum role required to push a matching image: \"maintainer\", \"owner\", or \"admin\". Omit to leave GitLab's default.")]
        string? minimumAccessLevelForPush = null,
        [Description(
            "Optional minimum role required to delete a matching image: \"maintainer\", \"owner\", or \"admin\". Omit to leave GitLab's default.")]
        string? minimumAccessLevelForDelete = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateContainerRegistryProtectionRuleRequest
        {
            RepositoryPathPattern = repositoryPathPattern,
            MinimumAccessLevelForPush = ParseContainerAccessLevel(minimumAccessLevelForPush),
            MinimumAccessLevelForDelete = ParseContainerAccessLevel(minimumAccessLevelForDelete)
        };

        var rule = await containerRegistryProtectionRules.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/registry/protection/repository/rules (create)");
    }

    [McpServerTool(Name = "gitlab_delete_package_file", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes one file from a package while leaving the package and its other files in place — e.g. dropping a stray or oversized artifact without deleting the whole version.")]
    public async Task<PackageFileDeleteResult> DeletePackageFileAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The package's numeric id.")]
        long packageId,
        [Description("The file's numeric id, from gitlab_list_package_files.")]
        long packageFileId,
        CancellationToken cancellationToken = default)
    {
        await packagesGeneric.DeletePackageFileAsync(project, packageId, packageFileId, cancellationToken);
        return new PackageFileDeleteResult(packageId, packageFileId, true);
    }

    [McpServerTool(Name = "gitlab_upload_generic_package_file", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Publishes a build artifact (binary, archive, report) into a project's generic package registry under a package name, version and filename — the file-drop path CI or an agent uses when no dedicated package-manager format applies. GitLab creates the package on first upload if it does not already exist. Provide the file's raw bytes base64-encoded; files whose decoded size exceeds 4 MB are refused rather than silently truncated.")]
    public async Task<CallToolResult> UploadGenericPackageFileAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The generic package's name, e.g. \"build-artifacts\".")]
        string packageName,
        [Description("The package version, e.g. \"1.0.0\".")]
        string packageVersion,
        [Description("The file name to publish it under within this package version, e.g. \"report.zip\".")]
        string fileName,
        [Description("The file's raw bytes, base64-encoded.")]
        string contentBase64,
        [Description(
            "MIME content type of the file, e.g. \"application/zip\". Defaults to \"application/octet-stream\" if omitted.")]
        string? contentType = null,
        [Description(
            "Optional file status: \"hidden\" to publish without it appearing in package listings until explicitly promoted, or omit for GitLab's default visible status.")]
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64.");
        }

        if (bytes.Length > MaxFileBytes)
            throw new McpException(
                $"Decoded file is {bytes.Length} bytes, over the {MaxFileBytes}-byte limit this tool can upload. Upload it directly to GitLab instead.");

        await using var stream = new MemoryStream(bytes);
        var upload = new GitLabFileUpload
        {
            Content = stream,
            FileName = fileName,
            ContentType = contentType ?? "application/octet-stream"
            // FieldName left at its default: only endpoints that deviate from GitLabFileUpload's
            // DefaultFieldName (e.g. "avatar") need to set it, per the library's own doc.
        };

        var file = await packagesGeneric.UploadGenericPackageFileAsync(
            project, packageName, packageVersion, fileName, upload, ParsePackageFileStatus(status), cancellationToken);

        return GitLabContent.Wrap(PackageFileMapper.ToSummary(file),
            "projects/:id/packages/generic/:package_name/:package_version/:file_name (upload)");
    }

    [McpServerTool(Name = "gitlab_delete_container_repository_tag", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Deletes one named image tag from a repository, e.g. removing a bad or leaked build. This cannot be undone.")]
    public async Task<ContainerRepositoryTagDeleteResult> DeleteContainerRepositoryTagAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The container repository's numeric id, from gitlab_list_container_repositories.")]
        long repositoryId,
        [Description("The tag's name to delete, e.g. \"v1.2.3\".")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        await containerRegistry.DeleteTagAsync(project, repositoryId, tagName, cancellationToken);
        return new ContainerRepositoryTagDeleteResult(repositoryId, true);
    }

    [McpServerTool(Name = "gitlab_delete_container_repository_tags", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Bulk-deletes image tags on a repository matching a name regex, a keep-newest-N count, or an age threshold — the ad hoc equivalent of running a registry cleanup policy on demand. GitLab performs the actual removal asynchronously; this call only schedules it. At least one filter must be given, or GitLab would otherwise delete every tag by accident.")]
    public async Task<ContainerRepositoryTagsBulkDeleteResult> DeleteContainerRepositoryTagsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The container repository's numeric id, from gitlab_list_container_repositories.")]
        long repositoryId,
        [Description(
            "Delete tags whose name matches this regex; pass \".*\" to match every tag. Omit if using nameRegexKeep, keepN or olderThan instead.")]
        string? nameRegexDelete = null,
        [Description("Retain tags whose name matches this regex even if they would otherwise be deleted.")]
        string? nameRegexKeep = null,
        [Description(
            "Keep this many of the most recent tags matching nameRegexKeep; older matching tags are deleted. Omit to not apply a keep-count filter.")]
        int? keepN = null,
        [Description("Only delete tags older than this duration, e.g. \"1h\", \"1d\", \"1month\".")]
        string? olderThan = null,
        CancellationToken cancellationToken = default)
    {
        if (nameRegexDelete is null && nameRegexKeep is null && keepN is null && olderThan is null)
            throw new McpException(
                "At least one of nameRegexDelete, nameRegexKeep, keepN, or olderThan must be given.");

        var options = new DeleteRegistryRepositoryTagsOptions
        {
            NameRegexDelete = nameRegexDelete,
            NameRegexKeep = nameRegexKeep,
            KeepN = keepN,
            OlderThan = olderThan
        };

        await containerRegistry.DeleteTagsAsync(project, repositoryId, options, cancellationToken);
        return new ContainerRepositoryTagsBulkDeleteResult(repositoryId, true);
    }

    [McpServerTool(Name = "gitlab_purge_dependency_proxy_cache", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Schedules deletion of every cached upstream image/package manifest and blob in a group's dependency proxy — e.g. forcing a refetch after an upstream image was fixed or replaced. GitLab performs the purge asynchronously (202 Accepted); this call only queues it. Requires the Owner role on the group.")]
    public async Task<DependencyProxyCachePurgeResult> PurgeDependencyProxyCacheAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        CancellationToken cancellationToken = default)
    {
        await dependencyProxy.PurgeCacheAsync(group, cancellationToken);
        return new DependencyProxyCachePurgeResult(true);
    }

    [McpServerTool(Name = "gitlab_update_package_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes an existing package protection rule's name pattern, format, or required access level. Fields left omitted keep their current value.")]
    public async Task<CallToolResult> UpdatePackageProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_package_protection_rules.")]
        long ruleId,
        [Description("New package name pattern, supporting a trailing \"*\" wildcard. Omit to leave unchanged.")]
        string? packageNamePattern = null,
        [Description(
            "New package format: one of \"cargo\", \"conan\", \"generic\", \"helm\", \"maven\", \"npm\", \"nuget\", \"pypi\", \"terraform_module\". Omit to leave unchanged.")]
        string? packageType = null,
        [Description(
            "New minimum role required to push a matching package: \"maintainer\", \"owner\", or \"admin\". Omit to leave unchanged.")]
        string? minimumAccessLevelForPush = null,
        [Description(
            "New minimum role required to delete a matching package: \"owner\" or \"admin\". Omit to leave unchanged.")]
        string? minimumAccessLevelForDelete = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePackageProtectionRuleRequest
        {
            PackageNamePattern = packageNamePattern,
            PackageType = ParseProtectionRuleTypeOptional(packageType),
            MinimumAccessLevelForPush = ParsePushAccessLevel(minimumAccessLevelForPush),
            MinimumAccessLevelForDelete = ParseDeleteAccessLevel(minimumAccessLevelForDelete)
        };

        var rule = await packageProtectionRules.UpdateAsync(project, ruleId, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/packages/protection/rules/:rule_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_package_protection_rule", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Removes a package protection rule, lifting the push/delete restriction it enforced.")]
    public async Task<PackageProtectionRuleDeleteResult> DeletePackageProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_package_protection_rules.")]
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        await packageProtectionRules.DeleteAsync(project, ruleId, cancellationToken);
        return new PackageProtectionRuleDeleteResult(ruleId, true);
    }

    [McpServerTool(Name = "gitlab_update_container_registry_protection_rule", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes an existing container repository protection rule's path pattern or required access levels. Fields left omitted keep their current value; pass \"unset\" for an access level to remove that restriction entirely.")]
    public async Task<CallToolResult> UpdateContainerRegistryProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_container_registry_protection_rules.")]
        long ruleId,
        [Description(
            "New image repository path pattern, supporting a trailing \"*\" wildcard. Omit to leave unchanged.")]
        string? repositoryPathPattern = null,
        [Description(
            "New minimum role required to push a matching image: \"maintainer\", \"owner\", \"admin\", or \"unset\" to remove the restriction. Omit to leave unchanged.")]
        string? minimumAccessLevelForPush = null,
        [Description(
            "New minimum role required to delete a matching image: \"maintainer\", \"owner\", \"admin\", or \"unset\" to remove the restriction. Omit to leave unchanged.")]
        string? minimumAccessLevelForDelete = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateContainerRegistryProtectionRuleRequest
        {
            RepositoryPathPattern = repositoryPathPattern,
            MinimumAccessLevelForPush = ParseContainerAccessLevelOrUnset(minimumAccessLevelForPush),
            MinimumAccessLevelForDelete = ParseContainerAccessLevelOrUnset(minimumAccessLevelForDelete)
        };

        var rule = await containerRegistryProtectionRules.UpdateAsync(project, ruleId, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/registry/protection/repository/rules/:rule_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_container_registry_protection_rule", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Removes a container repository protection rule.")]
    public async Task<ContainerRegistryProtectionRuleDeleteResult> DeleteContainerRegistryProtectionRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_container_registry_protection_rules.")]
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        await containerRegistryProtectionRules.DeleteAsync(project, ruleId, cancellationToken);
        return new ContainerRegistryProtectionRuleDeleteResult(ruleId, true);
    }

    [McpServerTool(Name = "gitlab_create_container_registry_protection_tag_rule", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Protects an image tag name pattern (e.g. \"release-*\") so only roles at or above chosen push/delete levels can overwrite or remove matching tags. Both access levels are required -- unlike repository-path rules, there is no unrestricted default for tags.")]
    public async Task<CallToolResult> CreateContainerRegistryProtectionTagRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "The image tag name pattern to protect, supporting a trailing \"*\" wildcard, e.g. \"release-*\".")]
        string tagNamePattern,
        [Description(
            "Minimum role required to push a matching tag: \"maintainer\", \"owner\", or \"admin\". Required -- there is no unrestricted default.")]
        string minimumAccessLevelForPush,
        [Description(
            "Minimum role required to delete a matching tag: \"maintainer\", \"owner\", or \"admin\". Required -- there is no unrestricted default.")]
        string minimumAccessLevelForDelete,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateContainerRegistryProtectionTagRuleRequest
        {
            TagNamePattern = tagNamePattern,
            MinimumAccessLevelForPush =
                ParseContainerAccessLevelRequired(minimumAccessLevelForPush, nameof(minimumAccessLevelForPush)),
            MinimumAccessLevelForDelete =
                ParseContainerAccessLevelRequired(minimumAccessLevelForDelete, nameof(minimumAccessLevelForDelete))
        };

        var rule = await containerRegistryProtectionTagRules.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/registry/protection/tag/rules (create)");
    }

    [McpServerTool(Name = "gitlab_update_container_registry_protection_tag_rule", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes an existing image tag protection rule's pattern or required access levels. Fields left omitted keep their current value; pass \"unset\" for an access level to remove that restriction entirely.")]
    public async Task<CallToolResult> UpdateContainerRegistryProtectionTagRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_container_registry_protection_tag_rules.")]
        long ruleId,
        [Description("New image tag name pattern, supporting a trailing \"*\" wildcard. Omit to leave unchanged.")]
        string? tagNamePattern = null,
        [Description(
            "New minimum role required to push a matching tag: \"maintainer\", \"owner\", \"admin\", or \"unset\" to remove the restriction. Omit to leave unchanged.")]
        string? minimumAccessLevelForPush = null,
        [Description(
            "New minimum role required to delete a matching tag: \"maintainer\", \"owner\", \"admin\", or \"unset\" to remove the restriction. Omit to leave unchanged.")]
        string? minimumAccessLevelForDelete = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateContainerRegistryProtectionTagRuleRequest
        {
            TagNamePattern = tagNamePattern,
            MinimumAccessLevelForPush = ParseContainerAccessLevelOrUnset(minimumAccessLevelForPush),
            MinimumAccessLevelForDelete = ParseContainerAccessLevelOrUnset(minimumAccessLevelForDelete)
        };

        var rule = await containerRegistryProtectionTagRules.UpdateAsync(project, ruleId, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionRuleMapper.ToSummary(rule),
            "projects/:id/registry/protection/tag/rules/:rule_id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_container_registry_protection_tag_rule", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Removes an image tag protection rule.")]
    public async Task<ContainerRegistryProtectionTagRuleDeleteResult> DeleteContainerRegistryProtectionTagRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The protection rule's numeric id, from gitlab_list_container_registry_protection_tag_rules.")]
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        await containerRegistryProtectionTagRules.DeleteAsync(project, ruleId, cancellationToken);
        return new ContainerRegistryProtectionTagRuleDeleteResult(ruleId, true);
    }

    [McpServerTool(Name = "gitlab_create_debian_distribution", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Sets up a new Debian (APT) distribution/codename on a project so packages can be published to it. Only the codename is required; everything else takes GitLab's default when omitted.")]
    public async Task<CallToolResult> CreateDebianDistributionAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The new distribution's codename, e.g. \"bullseye\". Cannot be changed after creation.")]
        string codename,
        [Description("Optional suite name, e.g. \"stable\". Omit for GitLab's default.")]
        string? suite = null,
        [Description("Optional origin label shown to apt clients. Omit for GitLab's default.")]
        string? origin = null,
        [Description("Optional short label shown to apt clients. Omit for GitLab's default.")]
        string? label = null,
        [Description("Optional human-readable description. Omit for GitLab's default.")]
        string? description = null,
        [Description("Optional signing-key validity window in seconds. Omit for GitLab's default.")]
        int? validTimeDurationSeconds = null,
        [Description(
            "Optional list of components to create, e.g. [\"main\", \"contrib\"]. Omit for GitLab's default (\"main\").")]
        string[]? components = null,
        [Description(
            "Optional list of architectures to create, e.g. [\"amd64\", \"arm64\"]. Omit for GitLab's default.")]
        string[]? architectures = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDebianDistributionRequest
        {
            Codename = codename,
            Suite = suite,
            Origin = origin,
            Label = label,
            Description = description,
            ValidTimeDurationSeconds = validTimeDurationSeconds,
            Components = components,
            Architectures = architectures
        };

        var distribution = await packagesDebian.CreateDistributionForProjectAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(DebianMapper.ToSummary(distribution), "projects/:id/debian_distributions (create)");
    }

    [McpServerTool(Name = "gitlab_update_debian_distribution", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a Debian distribution's suite, origin, label, description, retention window, components or architectures. The codename itself cannot be changed; fields left omitted keep their current value.")]
    public async Task<CallToolResult> UpdateDebianDistributionAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The distribution's codename to update, e.g. \"bullseye\".")]
        string codename,
        [Description("New suite name. Omit to leave unchanged.")]
        string? suite = null,
        [Description("New origin label. Omit to leave unchanged.")]
        string? origin = null,
        [Description("New short label. Omit to leave unchanged.")]
        string? label = null,
        [Description("New human-readable description. Omit to leave unchanged.")]
        string? description = null,
        [Description("New signing-key validity window in seconds. Omit to leave unchanged.")]
        int? validTimeDurationSeconds = null,
        [Description("New list of components, replacing the current list. Omit to leave unchanged.")]
        string[]? components = null,
        [Description("New list of architectures, replacing the current list. Omit to leave unchanged.")]
        string[]? architectures = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateDebianDistributionRequest
        {
            Suite = suite,
            Origin = origin,
            Label = label,
            Description = description,
            ValidTimeDurationSeconds = validTimeDurationSeconds,
            Components = components,
            Architectures = architectures
        };

        var distribution =
            await packagesDebian.UpdateDistributionForProjectAsync(project, codename, request, cancellationToken);
        return GitLabContent.Wrap(DebianMapper.ToSummary(distribution),
            "projects/:id/debian_distributions/:codename (update)");
    }

    [McpServerTool(Name = "gitlab_delete_debian_distribution", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes a Debian distribution from a project, scheduling deletion of its packages and index files. GitLab performs the removal asynchronously (202 Accepted); this call only schedules it. This cannot be undone.")]
    public async Task<DebianDistributionDeleteResult> DeleteDebianDistributionAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The distribution's codename to delete, e.g. \"bullseye\".")]
        string codename,
        CancellationToken cancellationToken = default)
    {
        await packagesDebian.DeleteDistributionForProjectAsync(project, codename, null, cancellationToken);
        return new DebianDistributionDeleteResult(codename, true);
    }

    // ---------------------------------------------------------------------------------------------
    // Parsing helpers
    // ---------------------------------------------------------------------------------------------

    private static GitLabPackageType? ParsePackageType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "maven" => GitLabPackageType.Maven,
            "npm" => GitLabPackageType.Npm,
            "conan" => GitLabPackageType.Conan,
            "nuget" => GitLabPackageType.NuGet,
            "pypi" => GitLabPackageType.PyPi,
            "composer" => GitLabPackageType.Composer,
            "generic" => GitLabPackageType.Generic,
            "golang" => GitLabPackageType.Golang,
            "debian" => GitLabPackageType.Debian,
            "rubygems" => GitLabPackageType.RubyGems,
            "helm" => GitLabPackageType.Helm,
            "terraform_module" or "terraformmodule" => GitLabPackageType.TerraformModule,
            _ => throw new McpException(
                $"packageType '{value}' is not recognised. Use one of: maven, npm, conan, nuget, pypi, composer, generic, golang, debian, rubygems, helm, terraform_module.")
        };
    }

    private static GitLabPackageProtectionRuleType ParseProtectionRuleType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "cargo" => GitLabPackageProtectionRuleType.Cargo,
            "conan" => GitLabPackageProtectionRuleType.Conan,
            "generic" => GitLabPackageProtectionRuleType.Generic,
            "helm" => GitLabPackageProtectionRuleType.Helm,
            "maven" => GitLabPackageProtectionRuleType.Maven,
            "npm" => GitLabPackageProtectionRuleType.Npm,
            "nuget" => GitLabPackageProtectionRuleType.NuGet,
            "pypi" => GitLabPackageProtectionRuleType.PyPi,
            "terraform_module" or "terraformmodule" => GitLabPackageProtectionRuleType.TerraformModule,
            _ => throw new McpException(
                $"packageType '{value}' is not recognised. Use one of: cargo, conan, generic, helm, maven, npm, nuget, pypi, terraform_module.")
        };
    }

    private static GitLabPackageProtectionRulePushAccessLevel? ParsePushAccessLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "maintainer" => GitLabPackageProtectionRulePushAccessLevel.Maintainer,
            "owner" => GitLabPackageProtectionRulePushAccessLevel.Owner,
            "admin" => GitLabPackageProtectionRulePushAccessLevel.Admin,
            _ => throw new McpException(
                $"minimumAccessLevelForPush '{value}' is not recognised. Use one of: maintainer, owner, admin.")
        };
    }

    private static GitLabPackageProtectionRuleDeleteAccessLevel? ParseDeleteAccessLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "owner" => GitLabPackageProtectionRuleDeleteAccessLevel.Owner,
            "admin" => GitLabPackageProtectionRuleDeleteAccessLevel.Admin,
            _ => throw new McpException(
                $"minimumAccessLevelForDelete '{value}' is not recognised. Use one of: owner, admin.")
        };
    }

    private static GitLabContainerRegistryProtectionAccessLevel? ParseContainerAccessLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "maintainer" => GitLabContainerRegistryProtectionAccessLevel.Maintainer,
            "owner" => GitLabContainerRegistryProtectionAccessLevel.Owner,
            "admin" => GitLabContainerRegistryProtectionAccessLevel.Admin,
            _ => throw new McpException(
                $"access level '{value}' is not recognised. Use one of: maintainer, owner, admin.")
        };
    }

    private static GitLabContainerRegistryProtectionAccessLevel ParseContainerAccessLevelRequired(string value,
        string paramName)
    {
        return value.ToLowerInvariant() switch
        {
            "maintainer" => GitLabContainerRegistryProtectionAccessLevel.Maintainer,
            "owner" => GitLabContainerRegistryProtectionAccessLevel.Owner,
            "admin" => GitLabContainerRegistryProtectionAccessLevel.Admin,
            _ => throw new McpException(
                $"{paramName} '{value}' is not recognised. Use one of: maintainer, owner, admin.")
        };
    }

    private static GitLabPackageStatus? ParsePackageStatus(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "default" => GitLabPackageStatus.Default,
            "hidden" => GitLabPackageStatus.Hidden,
            "processing" => GitLabPackageStatus.Processing,
            "error" => GitLabPackageStatus.Error,
            "pending_destruction" or "pendingdestruction" => GitLabPackageStatus.PendingDestruction,
            "deprecated" => GitLabPackageStatus.Deprecated,
            _ => throw new McpException(
                $"status '{value}' is not recognised. Use one of: default, hidden, processing, error, pending_destruction, deprecated.")
        };
    }

    private static GitLabPackageFileStatus? ParsePackageFileStatus(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "default" => GitLabPackageFileStatus.Default,
            "hidden" => GitLabPackageFileStatus.Hidden,
            _ => throw new McpException($"status '{value}' is not recognised. Use one of: default, hidden.")
        };
    }

    private static GitLabPackageProtectionRuleType? ParseProtectionRuleTypeOptional(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : ParseProtectionRuleType(value);
    }

    private static GitLabContainerRegistryProtectionAccessLevelOrUnset? ParseContainerAccessLevelOrUnset(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "maintainer" => GitLabContainerRegistryProtectionAccessLevelOrUnset.Maintainer,
            "owner" => GitLabContainerRegistryProtectionAccessLevelOrUnset.Owner,
            "admin" => GitLabContainerRegistryProtectionAccessLevelOrUnset.Admin,
            "unset" => GitLabContainerRegistryProtectionAccessLevelOrUnset.Unset,
            _ => throw new McpException(
                $"access level '{value}' is not recognised. Use one of: maintainer, owner, admin, unset.")
        };
    }
}