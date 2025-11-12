// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Helpers;

public static class ResourceKindHelper
{
    public const string SubscriptionType = "subscriptions";
    public const string ResourceGroupType = "resourcegroups";
    public const string ContainerAppType = "Microsoft.App/containerApps";
    public const string ContainerAppJobType = "Microsoft.App/containerAppJobs";
    public const string ContainerAppRevisionType = "Microsoft.App/containerApps/revisions";
    public const string ContainerAppEnvironmentType = "Microsoft.App/managedEnvironments";
    public const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    public const string VirtualNetworkSubnetType = "Microsoft.Network/virtualNetworks/subnets";
    public const string LoadBalancerType = "Microsoft.Network/loadBalancers";
    public const string ManagedClusterType = "Microsoft.ContainerService/managedClusters";
    public const string UserAssignedManagedIdentityType = "Microsoft.ManagedIdentity/userAssignedIdentities";
    public const string AzureKubernetesServiceType = "Microsoft.ContainerService/managedClusters";
    public const string AzureKubernetesServiceDeploymentType = "k8s/apps/v1/deployments";
    public const string AzureKubernetesServiceStatefulSetType = "k8s/apps/v1/statefulsets";
    public const string AzureKubernetesServicePodType = "k8s/core/v1/pods";
    public const string AzureRedisCacheType = "Microsoft.Cache/redis";
    public const string AzureSQLType = "Microsoft.Sql/servers";
    public const string CosmosDbType = "Microsoft.DocumentDB/databaseAccounts";
    public const string PostgreSqlFlexServerType = "Microsoft.DBforPostgreSQL/flexibleServers";
    public const string StorageType = "Microsoft.Storage/storageAccounts";
    public const string EventHubType = "microsoft.eventhub/namespaces";
    public const string ServiceBusType = "microsoft.servicebus/namespaces";
    public const string AzureMonitorWorkspaceType = "Microsoft.Monitor/accounts";
    public const string KeyVaultType = "Microsoft.KeyVault/vaults";
    public const string ManagedDiskType = "Microsoft.Compute/disks";
    public const string ApiManagementType = "Microsoft.ApiManagement/service";
    public const string ApiManagementBackendType = "Microsoft.ApiManagement/service/backends";
    public const string ApiCenterType = "Microsoft.ApiCenter/services";
    public const string LogicAppResourceKind = "logicApp";
    public const string EventGridTopicType = "Microsoft.EventGrid/topics";
    public const string SynapseWorkspaceType = "Microsoft.Synapse/workspaces";

    // New constants for App Service Web/Function Apps and App Service Plans.
    public const string AppServiceType = "Microsoft.Web/sites";
    public const string AppServicePlanType = "Microsoft.Web/serverFarms";

    // Friendly names for resource types
    static readonly Dictionary<string, string> ResourceFriendlyName = new Dictionary<string, string>
    {
        // Compute / containers
        [ContainerAppType] = "containerapps",
        [ContainerAppJobType] = "containerappjobs",
        [ContainerAppEnvironmentType] = "managedenvironments",

        // Kubernetes / orchestrators
        [ManagedClusterType] = "managedclusters",

        // Web & Functions
        [AppServicePlanType] = "serverfarms",

        // Databases & caches
        [CosmosDbType] = "cosmosdb",
        [PostgreSqlFlexServerType] = "postgresql",
        [AzureSQLType] = "sqlserver",
        [AzureRedisCacheType] = "redis",

        // Azure Monitoring & Insights
        [AzureMonitorWorkspaceType] = "azmonitor",

        // Networking
        [VirtualNetworkType] = "vnet",
        [VirtualNetworkSubnetType] = "subnet",

        [ApiManagementType] = "apimanagement",
        [ApiManagementBackendType] = "apimanagementbackend",
        [ApiCenterType] = "apicenter"
    };

    public static string getResourceKind(string type, string? kind)
    {
        // Handle specific cases for web apps
        if (String.Equals(AppServiceType, type, StringComparison.OrdinalIgnoreCase))
        {
            var kindLower = kind?.ToLowerInvariant();
            if (kindLower != null)
            {
                if (kindLower.Contains("functionapp") && !kindLower.Contains("workflowapp"))
                {
                    return "functionapp";
                }
                else if (kindLower.Contains("workflowapp"))
                {
                    return LogicAppResourceKind;
                }
                else
                {
                    return "webApp";
                }
            }
        }

        var match = ResourceFriendlyName.Where(k =>
    type.Contains(k.Key, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        if (match.Value != null)
        {
            return match.Value;
        }
        else
        {
            // Extract the type from resourceType path as fallback
            var typeArray = type.Split('/');
            return typeArray[typeArray.Length - 1];
        }
    }
}
