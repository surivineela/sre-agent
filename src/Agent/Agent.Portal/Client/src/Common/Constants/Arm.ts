export const DeploymentProvisioningStates = {
    succeeded: 'Succeeded',
    deploymentValidation: 'DeploymentValidation',
    validationSubmitted: 'ValidationSubmitted',
    validationFailed: 'ValidationFailed',
    deploymentSubmitted: 'DeploymentSubmitted',
    deploymentFailed: 'DeploymentFailed',
    Deleting: 'Deleting',
    Failed: 'Failed',
};

export class ResourceTypes {
    public static ResourcesProvider = 'Microsoft.Resources';
    public static ResourceDeploymentType = `${ResourceTypes.ResourcesProvider}/deployments`;

    public static WebProvider = 'Microsoft.Web';
    public static SitesResourceType = 'sites';
    public static KubeEnvironmentResourceType = ResourceTypes.WebProvider + '/kubeEnvironments';
    public static WebAppResourceType = ResourceTypes.WebProvider + '/sites';
    public static SWAResourceType = ResourceTypes.WebProvider + '/staticSites';
    public static ServerFarmType = ResourceTypes.WebProvider + '/serverFarms';

    public static NetworkProvider = 'Microsoft.Network';
    public static VNETResourceType = ResourceTypes.NetworkProvider + '/virtualNetworks';

    public static InsightsProvider = 'microsoft.insights';
    public static AppInsightsResourceType = `${ResourceTypes.InsightsProvider}/components`;

    public static WorkspaceProvider = 'Microsoft.OperationalInsights';
    public static workspaceResourceType = `${ResourceTypes.WorkspaceProvider}/workspaces`;

    public static AppProvider = 'Microsoft.App';
    public static ContainerAppResourceType = ResourceTypes.AppProvider + '/containerapps';
    public static ManagedEnvironmentResourceType = ResourceTypes.AppProvider + '/managedEnvironments';
    public static ConnectedEnvironmentResourceType = ResourceTypes.AppProvider + '/connectedEnvironments';
    public static CertificateResourceType = ResourceTypes.ManagedEnvironmentResourceType + '/certificates';
    public static SreAgent = 'agents';

    public static ContainerServiceProvider = 'Microsoft.ContainerService';

    public static ClassicStorage = 'microsoft.classicstorage';

    //Database servers
    public static CosmosDatabaseAccount = 'Microsoft.DocumentDb/databaseAccounts';
    public static MySqlFlexServer = 'Microsoft.DBforMySQL/flexibleServers';
    public static PostgreSQLFlexServer = 'Microsoft.DBforPostgreSQL/flexibleServers';
    public static PostgreSQLSingleServer = 'Microsoft.DBforPostgreSQL/servers';
    public static SqlServer = 'Microsoft.Sql/Servers';
    public static SqlServerDatabases = 'Microsoft.Sql/Servers/databases';
    public static StaticSiteType = 'Microsoft.Web/staticsites';

    public static ContainerRegistryProvider = 'Microsoft.ContainerRegistry';
    public static ContainerRegistryResourceType = ResourceTypes.ContainerRegistryProvider + '/registries';

    public static ManagedIdentityProvider = 'Microsoft.ManagedIdentity';
    public static UserIdentityResourceType = ResourceTypes.ManagedIdentityProvider + '/userAssignedIdentities';

    public static AuthorizationProvider = 'Microsoft.Authorization';
    public static RoleAssignmentResourceType = ResourceTypes.AuthorizationProvider + '/roleAssignments';
    public static RedisCacheResourceType = 'Microsoft.Cache/Redis';
    public static SpringboardResourceType = 'springboard';

    // Integration Spaces
    public static Spaces = 'Microsoft.IntegrationSpaces/spaces';
    public static Applications = `${ResourceTypes.Spaces}/applications`;
    public static BusinessProcess = `${ResourceTypes.Spaces}/applications/businessProcesses`;
    public static BusinessProcessVersion = `${ResourceTypes.Spaces}/applications/businessProcesses/versions`;

    // Business Processes
    public static BusinessProcesses = 'Microsoft.Logic/businessprocesses';

    // Monitoring
    public static KeyVaultProvider = 'Microsoft.KeyVault';
    public static KeyVaultResourceType = `${ResourceTypes.KeyVaultProvider}/vaults`;
    public static OperationalInsightsProvider = 'Microsoft.OperationalInsights';
    public static OperationalInsightsWorkspaceResourceType = `${ResourceTypes.OperationalInsightsProvider}/workspaces`;
    public static PortalProvider = 'Microsoft.Portal';
    public static PortalDashboardResourceType = `${ResourceTypes.PortalProvider}/dashboards`;
}
