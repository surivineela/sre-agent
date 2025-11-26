// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;

namespace Agent.Graph.Crawler.ARM;

public class Constants
{
    public const string SubscriptionType = SubscriptionNode.Type;
    public const string ResourceGroupType = ResourceGroupNode.Type;
    public const string ContainerAppType = "Microsoft.App/containerApps";
    public const string ContainerAppRevisionType = "Microsoft.App/containerApps/revisions";
    public const string ContainerAppEnvironmentType = "Microsoft.App/managedEnvironments";
    public const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
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
    public const string AzureMonitorResourceKind = "azmonitor";
    public const string KeyVaultType = "Microsoft.KeyVault/vaults";
    public const string ManagedDiskType = "Microsoft.Compute/disks";
    public const string ApiManagementType = "Microsoft.ApiManagement/service";
    public const string NetworkSecurityGroupType = "Microsoft.Network/networkSecurityGroups";
    public const string ApiManagementBackendType = "Microsoft.ApiManagement/service/backends";
    public const string ApiCenterType = "Microsoft.ApiCenter/services";
    public const string ApplicationInsightsType = "Microsoft.Insights/components";
    public const string ServiceProviderConnectionType = "Microsoft.Web/sites/connections";
    public const string ApiConnectionType = "Microsoft.Web/connections";
    public const string ApiManagementConnectionType = "Microsoft.Web/sites/apiManagementConnections";
    public const string FunctionConnectionType = "Microsoft.Web/sites/functionConnections";

    // New constants for App Service Web/Function Apps and App Service Plans.
    public const string AppServiceType = "Microsoft.Web/sites";
    public const string AppServicePlanType = "Microsoft.Web/serverFarms";
    public const string FunctionAppKind = "functionapp";
    public const string LogicAppKind = "logicapp";

    public const string ApplicationInsightsKind = "applicationInsights";

    // k8s
    // groups
    public const string KubernetesCoreGroup = "core";

    // api management
    public const string AzureBackend = "AzureBackend";
    public const string APIManagementBackend = "APIManagementBackend";
    public const string APICenter = "APICenter";
    public const string APICenterDependency = "APICenterDependency";
    public const string ResourceName = "resourceName";
    public const string ResourceUri = "requestUri";
    public const string ArmResourceId = "armResourceId";
    public const string ConnectedAPIs = "connectedApis";

    // versions
    public const string KubernetesV1Version = "v1";

    // kinds
    public const string KubernetesNamespaceType = "namespaces";
    public const string KubernetesPodType = "pods";
    public const string KubernetesDeploymentType = "deployments";
    public const string KubernetesDaemonSetType = "daemonsets";
    public const string KubernetesStatefulSetType = "statefulsets";
    public const string KubernetesReplicaSetType = "replicasets";
    public const string KubernetesServiceType = "services";
    public const string KubernetesConfigMapType = "configmaps";
    public const string KubernetesSecretType = "secrets";
    public const string KubernetesNodeType = "nodes";
    public const string KubernetesPersistentVolumeType = "persistentvolumes";
    public const string KubernetesPersistentVolumeClaimType = "persistentvolumeclaims";

    // Node properties

    // Edge properties
    // Edge relationship types
    public class Relationships
    {
        public const string Contains = "CONTAINS";
        public const string Linked = "LINKED";
        public const string Manages = "MANAGES";
        public const string SqlConnected = "SQL_CONNECTED";
        public const string PostgreSqlConnected = "POSTGRESQL_CONNECTED";
        public const string RedisConnected = "REDIS_CONNECTED";
        public const string UsesRedis = "USES_REDIS";
        public const string HasRole = "HAS_ROLE";
        public const string HasIdentity = "HAS_IDENTITY";
        public const string Connected = "CONNECTED";
        public const string Hosts = "HOSTS";
        public const string HostedOn = "HOSTED_ON";
        public const string ServesCode = "SERVES_CODE";
        public const string References = "REFERENCES";
        public const string BackedBy = "BACKED_BY";
        public const string RevisionOf = "REVISION_OF";
        public const string OwnedBy = "OWNED_BY";
        public const string RelatedToIncident = "RELATED_TO_INCIDENT";
        public const string MonitoredBy = "MONITORED_BY";
        public const string HasIgnoreConfig = "HAS_IGNORE_CONFIG";
        public const string StoresIn = "STORES_IN";
        public const string Uses = "USES";
        public const string PullsFrom = "PULLS_FROM";
        public const string DelegatedTo = "DELEGATED_TO";
        public const string UsesDnsZone = "USES_DNS_ZONE";
        public const string UsesAction = "USES_ACTION";
        public const string UsesTrigger = "USES_TRIGGER";
        public const string UsesTriggerAction = "USES_TRIGGER_ACTION";
    }

    // indicates this node is a part of specific topology
    public const string NetworkPathKey = "NetworkPath";
    public const string NetworkPathIngress = "Ingress";
    public const string NetworkPathEgress = "Egress";
    public const string RbacPath = "RbacPath";
    public const string RbacPathInherited = "Inherited";
    public const string RbacPathExplicit = "Explicit";

    // RBAC properties
    public const string RoleAssignmentIdKey = "RoleAssignmentId";

    // Reference properties
    public const string ReferenceTypeKey = "ReferenceType";
    public const string ReferenceTypeVolumeMount = "VolumeMount";
    public const string ReferenceTypeEnv = "Env";
    public const string ReferenceTypePersistentVolumeClaim = "PersistentVolumeClaim";

    // BackedBy properties
    public const string BackendStatusKey = "BackendStatus";
    public const string BackendStatusReady = "Ready";
    public const string BackendStatusNotReady = "NotReady";

    // Connection properties
    public const string ConnectionType = "connectionType";
    public const string ConnectionTypeNetwork = "network";

    // App Health Info Constants
    public const double AppHealthHealthyThreshold = 99.0;
    public const double AppHealthDegradedThreshold = 95.0;
    public const int AppHealthDecimalPlaces = 2;

    // API Management Azure Monitor Constants
    public const string GatewayCpuPercent = "CpuPercent_Gateway";
    public const string GatewayMemoryPercent = "MemoryPercent_Gateway";
    public const string GatewayRequestsDuration = "Duration";
    public const string BackendRequestsDuration = "BackendDuration";

    // General Azure Monitor Constants
    public const string Requests = "Requests";
    public const string Capacity = "Capacity";
    public const string BackendDuration = "BackendDuration";
    public const string NetworkConnectivity = "NetworkConnectivity";
    public const string Gateway = "gateway";
    public const string Backend = "backend";
    public const string AzureManagementPrefix = "https://management.azure.com";

    public const string UnitCount = "Count";
    public const string UnitPercent = "Percent";
    public const string UnitMilliSeconds = "MilliSeconds";

    public const string AggregationAverage = "Average";
    public const string AggregationTotal = "Total";
}

