// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public interface IArmResourceGraphNode
    {
        public string GetNodeLabel();
        public string GetNodeId();
        public string GetResourceType();
        public IDictionary<string, object> GetNodeProperties();
    }

    public abstract class GraphNode : IArmResourceGraphNode
    {
        public long UpdateTs { get; set; }
        public abstract string GetNodeId();
        public abstract string GetNodeLabel();
        public abstract IDictionary<string, object> GetNodeProperties();
        public abstract string GetResourceType();
    }

    public class KubernetesResourceNode : GraphNode
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
            UpdateTs = DateTime.UtcNow.Ticks;
            ResourceType = resourceType.ToLowerInvariant();
            ResourceId = resourceId.ToLowerInvariant();
            SubscriptionId = subscriptionId.ToLowerInvariant();
            Namespace = @namespace.ToLowerInvariant();
            ResourceGroupName = @namespace.ToLowerInvariant();
            ResourceName = resourceName.ToLowerInvariant();
            Kind = kind.ToLowerInvariant();
            ApiVersion = apiVersion.ToLowerInvariant();
        }

        public override string GetNodeLabel()
        {
            // Return a standardized label combining resource type and kind
            return $"K8s_{Kind}";
        }

        public override string GetNodeId()
        {
            // Using ResourceId as the unique identifier
            // This should already be in the format: {clusterResourceId}/{kind}/{namespace}/{name}
            return ResourceId;
        }

        public override string GetResourceType()
        {
            // Return the Kubernetes resource type in a standardized format
            return $"K8s/{Kind}";
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            // Return all relevant properties as a dictionary
            return new Dictionary<string, object>
            {
                { "updateTs", UpdateTs},
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

    public class ArmResourceNode : GraphNode
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
            UpdateTs = DateTime.UtcNow.Ticks;
            ResourceType = resourceType?.ToLowerInvariant();
            ResourceId = resourceId?.ToLowerInvariant();
            SubscriptionId = subscriptionId?.ToLowerInvariant();
            ResourceGroupName = resourceGroupName?.ToLowerInvariant();
            ResourceName = resourceName?.ToLowerInvariant();
            SystemMI = systemMI;
        }

        public override string GetNodeLabel()
        {
            //var parts = ResourceType.Split('/');
            //return parts[parts.Length - 1];

            // use full arm type to avoid potential conflict
            return ResourceType;
        }

        public override string GetNodeId()
        {
            return ResourceId;
        }

        public override string GetResourceType()
        {
            return ResourceType;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            return new Dictionary<string, object>
            {
                // resourceType is partition key, cannot be updated
                //{ "resourceType", ResourceType },
                { "updateTs", UpdateTs },
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
            ResourceType = "subscription";
            SubscriptionId = subscriptionId.ToLowerInvariant();
            ResourceName = subscriptionId.ToLowerInvariant();
            ResourceId = $"/subscriptions/{SubscriptionId}";
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
            ResourceType = "resourcegroup";
            SubscriptionId = subscriptionId.ToLowerInvariant();
            ResourceName = resoureGroupName.ToLowerInvariant();
            ResourceGroupName = resoureGroupName.ToLowerInvariant();
            ResourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}";
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
        public string? Location { get; set; }
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
            Location = location?.NormalizeLocation();
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

    public class AppServiceNode : ArmResourceNode
    {
        public string? Location { get; set; }
        public string? VnetId { get; set; }
        public string? MinTlsVersion { get; set; }
        public AppServiceNode() : base() { }
        public AppServiceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string location = null,
            string? vnetId = null,
            string? tlsVersion = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName)
        {
            Location = location?.NormalizeLocation();
            VnetId = vnetId;
            MinTlsVersion = tlsVersion;
        }
        public override IDictionary<string, object> GetNodeProperties()
        {
            var props = base.GetNodeProperties();
            props.Add("location", Location);
            if (VnetId != null)
            {
                props.Add("vnetId", VnetId);
            }
            if (MinTlsVersion != null)
            {
                props.Add("minTlsVersion", MinTlsVersion);
            }
            return props;
        }
    }

    public class CosmosDbNode : ArmResourceNode
    {
        public string? Location { get; set; }
        public string? ConsistencyPolicy { get; set; }

        public CosmosDbNode() : base() { }

        public CosmosDbNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string location = null,
            string? consistencyPolicy = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName)
        {
            Location = location?.NormalizeLocation();
            ConsistencyPolicy = consistencyPolicy;
        }

        public override IDictionary<string, object> GetNodeProperties()
        {
            var props = base.GetNodeProperties();
            props.Add("location", Location);
            if (ConsistencyPolicy != null)
            {
                props.Add("consistencyPolicy", ConsistencyPolicy);
            }
            return props;
        }
    }

    public static partial class LocationExtensions
    {
        public static string NormalizeLocation(this string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentNullException(nameof(location));
            }

            return LocationNormalizationRegex().Replace(location, string.Empty).ToLowerInvariant();
        }

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex LocationNormalizationRegex();
    }
}
