# GitLab.Client domain map

The 143 resource client interfaces of `GitLab.Client` 1.0.0, grouped for navigation. Verified
exhaustive and disjoint against the R1 interface list: **143 assigned, 0 duplicates, 0 missing**.
The family boundaries are a navigation aid; the library does not assert them.


| Family | Clients |
|---|---|
| **Planning & work tracking (13)** | `IIssuesClient` `IBoardsClient` `IMilestonesClient` `IIterationsClient` `ILabelsClient` `INotesClient` `IDiscussionsClient` `IAwardEmojiClient` `IResourceEventsClient` `IResourceSubscriptionsClient` `ITodosClient` `IEventsClient` `IWikisClient` |
| **Code / SCM (10)** | `IBranchesClient` `ICommitsClient` `ITagsClient` `IRepositoriesClient` `IRepositoryFilesClient` `IProtectedBranchesClient` `IProtectedTagsClient` `IProjectMirrorsClient` (pull) `IRemoteMirrorsClient` (push) `ISnippetsClient` |
| **Merge requests & review (7)** | `IMergeRequestsClient` `IMergeRequestApprovalsClient` `IApprovalRulesClient` `IMergeTrainsClient` `IDraftNotesClient` `ISuggestionsClient` `IExternalStatusChecksClient` |
| **CI/CD, deploy & environments (24)** | `IPipelinesClient` `IPipelineSchedulesClient` `IJobsClient` `IJobArtifactsClient` `IJobTokenScopeClient` `IRunnersClient` `IRunnerControllersClient` `ITriggersClient` `IVariablesClient` `ICiLintClient` `ICiCatalogClient` `ICommitStatusesClient` `IEnvironmentsClient` `IProtectedEnvironmentsClient` `IDeploymentsClient` `IFreezePeriodsClient` `IResourceGroupsClient` `IReleasesClient` `IPagesClient` `IFeatureFlagsClient` `IClusterAgentsClient` `ITerraformStatesClient` `ISecureFilesClient` `IRolloutsClient` |
| **Packages & registries (17)** | `IPackagesCargoClient` `IPackagesComposerClient` `IPackagesConanClient` `IPackagesDebianClient` `IPackagesGenericClient` (Maven proxy + Go modules) `IPackagesHelmClient` `IPackagesNpmClient` `IPackagesNuGetClient` `IPackagesPyPiClient` `IPackagesRpmClient` `IPackagesRubyGemsClient` `IPackagesTerraformModulesClient` `IContainerRegistryClient` `IDependencyProxyClient` `IProjectContainerRegistryProtectionRulesClient` `IProjectContainerRegistryProtectionTagRulesClient` `IProjectPackageProtectionRulesClient` |
| **Security & compliance (6)** | `IAuditEventsClient` `IAttestationsClient` `IComplianceSettingsClient` `IDependenciesClient` (dependency list / SBOM) `IPushRulesClient` `IGroupCredentialsInventoryClient` |
| **Admin & instance (16)** | `IInstanceClient` `IApplicationsClient` `IBroadcastMessagesClient` `ISidekiqClient` `IFeaturesClient` `IExperimentsClient` `ILicensesClient` (EE activation + managed-licence policies) `IUsageDataClient` `IStorageMovesClient` `IAdminMigrationsClient` `IBackgroundMigrationsClient` `IDataManagementClient` `ICodeSearchClient` (Zoekt) `IActiveContextClient` `IKnowledgeGraphClient` `ICustomAttributesClient` |
| **Users, membership & access (17)** | `IUsersClient` `ICurrentUserClient` `IMembersClient` `IMemberRolesClient` `IAccessRequestsClient` `IInvitationsClient` `IPersonalAccessTokensClient` `IAccessTokensClient` (project/group-scoped) `IDeployTokensClient` `IDeployKeysClient` `ISshKeysClient` `IGpgKeysClient` `IServiceAccountsClient` `ISamlGroupLinksClient` `IProviderIdentitiesClient` `INotificationSettingsClient` `IMobilePushSubscriptionsClient` |
| **Namespaces, projects & groups (10)** | `IProjectsClient` `IGroupsClient` `INamespacesClient` `IOrganizationsClient` `ITopicsClient` `IProjectAliasesClient` `IBadgesClient` `IProjectImportClient` `IGroupImportClient` `IBulkImportsClient` (direct transfer) |
| **Everything else (23)** | *webhooks* `IProjectHooksClient` `IGroupHooksClient` `ISystemHooksClient` · *integrations* `IIntegrationsClient` `IJiraConnectClient` `IPlatformIntegrationsClient` · *search & analytics* `ISearchClient` `IAnalyticsClient` · *monitor* `IAlertManagementClient` `IErrorTrackingClient` · *AI & ML* `IDuoClient` `IDuoWorkflowsClient` `IProjectAiAgentsClient` `IMlExperimentsClient` `IMlModelsClient` `IMlModelPackageFilesClient` · *content & utility* `IMarkdownClient` `ITemplatesClient` `IProjectUploadsClient` `IAvatarsClient` · *tooling & internal* `IVsCodeClient` `IWorkspacesClient` `IInternalClient` |

`IReleasesClient` is filed under CI/CD, not Code/SCM: a release is addressed by tag name
(`/releases/:tag_name/assets/links`), so people hunting for it reach for `ITagsClient` first.

`IBulkImportsClient` is filed with the import clients, not Admin: direct transfer is what the
library's own docs recommend over the file-based export/import on `IGroupImportClient`.

Regenerate the interface list this was checked against with **R1** in `../SKILL.md`.
