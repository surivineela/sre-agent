namespace Agent.Data.DatabaseManagers.GraphDatabase
{
    public interface IArmResourceGraphNode
    {
        public string GetNodeLabel();
        public string GetNodeId();
        public string GetResourceType();
        public IDictionary<string, object> GetNodeProperties();
    }

    public class KubernetesResourceNode : IArmResourceGraphNode
    {
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }  // Maps to Namespace in K8s
        public string ResourceName { get; set; }
        public string Namespace { get; set; }
        public string Kind { get; set; }
        public string ApiVersion { get; set; }

        public KubernetesResourceNode() { }

        public KubernetesResourceNode(
            string resourceType,
            string resourceId,
            string subscriptionId,
            string @namespace,
            string resourceName,
            string kind,
            string apiVersion)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            SubscriptionId = subscriptionId;
            Namespace = @namespace;
            ResourceGroupName = @namespace;
            ResourceName = resourceName;
            Kind = kind;
            ApiVersion = apiVersion;
        }

        public string GetNodeLabel()
        {
            // Return a standardized label combining resource type and kind
            return $"K8s_{Kind}";
        }

        public string GetNodeId()
        {
            // Using ResourceId as the unique identifier
            // This should already be in the format: {clusterResourceId}/{kind}/{namespace}/{name}
            return ResourceId;
        }

        public string GetResourceType()
        {
            // Return the Kubernetes resource type in a standardized format
            return $"K8s/{Kind}";
        }

        public IDictionary<string, object> GetNodeProperties()
        {
            // Return all relevant properties as a dictionary
            return new Dictionary<string, object>
        {
            { "resourceType", ResourceType },
            { "resourceId", ResourceId },
            { "subscriptionId", SubscriptionId },
            { "resourceGroupName", ResourceGroupName },
            { "resourceName", ResourceName },
            { "namespace", Namespace },
            { "kind", Kind },
            { "apiVersion", ApiVersion }
        };
        }
    }

    public class ArmResourceNode : IArmResourceGraphNode
    {
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string ResourceName { get; set; }
        public bool SystemMI { get; set; }

        public ArmResourceNode() { }
        public ArmResourceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            bool systemMI = false)
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            SubscriptionId = subscriptionId;
            ResourceGroupName = resourceGroupName;
            ResourceName = resourceName;
            SystemMI = systemMI;
        }

        public virtual string GetNodeLabel()
        {
            var parts = ResourceType.Split('/');
            return parts[parts.Length - 1].ToLowerInvariant();
        }

        public virtual string GetNodeId()
        {
            return ResourceId.ToLowerInvariant();
        }

        public virtual string GetResourceType()
        {
            return ResourceType;
        }

        public virtual IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object>
            {
                // resourceType is partition key, cannot be updated
                //{ "resourceType", ResourceType },
                { "resourceId", ResourceId },
                { "subscriptionId", SubscriptionId },
                { "resourceGroupName", ResourceGroupName },
                { "resourceName", ResourceName }
            };
        }

        // Mainly for system MI
        // To be able to crawl same resource again with ManagedIdentityNode
        public string GetHashString()
        {
            return $"{ResourceId}|{GetType()}";
        }
    }

    public sealed class SubscriptionNode : ArmResourceNode
    {
        public SubscriptionNode() { }

        public SubscriptionNode(string subscriptionId) : base()
        {
            ResourceType = "Subscription";
            SubscriptionId = subscriptionId;
            ResourceName = subscriptionId;
            ResourceId = $"/subscriptions/{subscriptionId}";
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object>
            {
                { "subscriptionId", SubscriptionId },
            };
        }
    }

    public sealed class ResourceGroupNode : ArmResourceNode
    {
        public ResourceGroupNode() { }

        public ResourceGroupNode(
            string subscriptionId,
            string resoureGroupName) : base()
        {
            ResourceType = "ResourceGroup";
            SubscriptionId = subscriptionId;
            ResourceName = resoureGroupName;
            ResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resoureGroupName}";
            ResourceGroupName = resoureGroupName;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object>
            {
                { "subscriptionId", SubscriptionId },
                { "resourceGroupName", ResourceGroupName },
            };
        }
    }

    public sealed class ContainerAppEnvironmentNode : ArmResourceNode
    {
        public string Location { get; set; }
        public string? VnetId { get; set; }
        public string? LbId { get; set; }

        public ContainerAppEnvironmentNode() : base() { }

        public ContainerAppEnvironmentNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string location = null,
            string? vnetId = null,
            string? lbId = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName)
        {
            Location = location;
            VnetId = vnetId;
            LbId = lbId;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            var props = base.GetNodeProperties();
            props.Add("location", Location);
            if (VnetId != null)
            {
                props.Add("vnetId", VnetId);
            }
            if (LbId != null)
            {
                props.Add("lbId", LbId);
            }

            return props;
        }
    }

    public sealed class ManagedIdentityNode : ArmResourceNode
    {
        public string IdentityType { set; get; }
        public string TenantId { get; set; }
        public string PrincipalId { get; set; }
        public string ClientId { get; set; }

        public const string UserAssignedManagedIdentityType = "UserAssigned";
        public const string SystemAssignedManagedIdentityType = "System";

        public ManagedIdentityNode() : base() { }
        public ManagedIdentityNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string type,
            string tenantId = null,
            string principalId = null,
            string clientId = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName)
        {
            IdentityType = type;
            TenantId = tenantId;
            PrincipalId = principalId;
            ClientId = clientId;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            var props = base.GetNodeProperties();
            props.Add("identityType", IdentityType);
            if (TenantId != null)
            {
                props.Add("tenantId", TenantId);
            }
            if (PrincipalId != null)
            {
                props.Add("principalId", PrincipalId);
            }
            if (ClientId != null)
            {
                props.Add("clientId", ClientId);
            }
            return props;
        }
    }
}
