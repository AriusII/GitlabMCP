using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Package registries & container registry (CLAUDE.md's DevOps persona table: "ContainerRegistry,
    // Packages*"). Every tool in this domain's catalog entry is DevOps-only.
    private static IReadOnlyDictionary<string, ToolGrant> PackagesRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_group_packages"] = new(Grant.DevOps, true),
            ["gitlab_list_container_repositories"] = new(Grant.DevOps, true),
            ["gitlab_list_debian_distributions"] = new(Grant.DevOps, true),
            ["gitlab_list_terraform_module_versions"] = new(Grant.DevOps, true),
            ["gitlab_list_package_protection_rules"] = new(Grant.DevOps, true),
            ["gitlab_list_container_registry_protection_rules"] = new(Grant.DevOps, true),
            ["gitlab_delete_package"] = new(Grant.DevOps, false),
            ["gitlab_delete_container_repository"] = new(Grant.DevOps, false),
            ["gitlab_create_package_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_create_container_registry_protection_rule"] = new(Grant.DevOps, false),

            // Backlog wave (part-1): still every tool in this domain is DevOps-only (CLAUDE.md).
            ["gitlab_list_packages"] = new(Grant.DevOps, true),
            ["gitlab_get_package"] = new(Grant.DevOps, true),
            ["gitlab_list_package_files"] = new(Grant.DevOps, true),
            ["gitlab_delete_package_file"] = new(Grant.DevOps, false),
            ["gitlab_list_package_pipelines"] = new(Grant.DevOps, true),
            ["gitlab_upload_generic_package_file"] = new(Grant.DevOps, false),
            ["gitlab_download_generic_package_file"] = new(Grant.DevOps, true),
            ["gitlab_list_group_container_repositories"] = new(Grant.DevOps, true),
            ["gitlab_get_container_repository"] = new(Grant.DevOps, true),
            ["gitlab_list_container_repository_tags"] = new(Grant.DevOps, true),
            ["gitlab_get_container_repository_tag"] = new(Grant.DevOps, true),
            ["gitlab_delete_container_repository_tag"] = new(Grant.DevOps, false),
            ["gitlab_delete_container_repository_tags"] = new(Grant.DevOps, false),
            ["gitlab_purge_dependency_proxy_cache"] = new(Grant.DevOps, false),
            ["gitlab_update_package_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_delete_package_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_update_container_registry_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_delete_container_registry_protection_rule"] = new(Grant.DevOps, false),

            // Backlog wave (part-2): still every tool in this domain is DevOps-only (CLAUDE.md).
            ["gitlab_list_container_registry_protection_tag_rules"] = new(Grant.DevOps, true),
            ["gitlab_create_container_registry_protection_tag_rule"] = new(Grant.DevOps, false),
            ["gitlab_update_container_registry_protection_tag_rule"] = new(Grant.DevOps, false),
            ["gitlab_delete_container_registry_protection_tag_rule"] = new(Grant.DevOps, false),
            ["gitlab_get_debian_distribution"] = new(Grant.DevOps, true),
            ["gitlab_create_debian_distribution"] = new(Grant.DevOps, false),
            ["gitlab_update_debian_distribution"] = new(Grant.DevOps, false),
            ["gitlab_delete_debian_distribution"] = new(Grant.DevOps, false),
            ["gitlab_get_debian_distribution_key"] = new(Grant.DevOps, true),
            ["gitlab_list_group_debian_distributions"] = new(Grant.DevOps, true),
            ["gitlab_get_group_debian_distribution"] = new(Grant.DevOps, true),
            ["gitlab_get_terraform_module"] = new(Grant.DevOps, true),
            ["gitlab_get_terraform_module_version"] = new(Grant.DevOps, true)
        };
    }
}