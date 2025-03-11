namespace Agent.Graph.Crawler.ARM;

public class Constants
{
    public const string SubscriptionType = "Subscription";
    public const string ResourceGroupType = "ResourceGroup";
    public const string ContainerAppType = "Microsoft.App/containerApps";
    public const string ContainerAppEnvironmentType = "Microsoft.App/managedEnvironments";
    public const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    public const string LoadBalancerType = "Microsoft.Network/loadBalancers";
    public const string ManagedClusterType = "Microsoft.ContainerService/managedClusters";
    public const string UserAssignedManagedIdentityType = "Microsoft.ManagedIdentity/userAssignedIdentities";
    public const string AzureKubernetesServiceType = "Microsoft.ContainerService/managedClusters";

    // New constants for App Service Web/Function Apps and App Service Plans.
    public const string AppServiceType = "Microsoft.Web/sites";
    public const string AppServicePlanType = "Microsoft.Web/serverFarms";

    // Node properties

    // Edge properties
    // Edge relationship types
    public class Relationships
    {
        public const string Contains = "CONTAINS";
        public const string Linked = "LINKED";
        public const string SqlConnected = "SQL_CONNECTED";
        public const string RedisConnected = "REDIS_CONNECTED";
        public const string HasRole = "HAS_ROLE";
        public const string HasIdentity = "HAS_IDENTITY";
        public const string Connected = "CONNECTED";
        public const string Hosts = "HOSTS";
        public const string HostedOn = "HOSTED_ON";
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
}
