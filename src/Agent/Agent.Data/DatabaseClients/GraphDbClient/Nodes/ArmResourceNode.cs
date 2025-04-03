// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Azure.Core;
using k8s;
using OpenTelemetry.Resources;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public interface IResourceGraphNode
    {
        public string GetNodeLabel();
        public string GetNodeId();
        public string GetResourceType();
        public IDictionary<string, object> GetNodeProperties();
    }

    public abstract class GraphNode : IResourceGraphNode
    {
        public long UpdateTs { get; set; }
        public abstract string GetNodeId();
        public abstract string GetNodeLabel();
        public abstract IDictionary<string, object> GetNodeProperties();
        public abstract string GetResourceType();

        public abstract string GetHashString();
        public abstract string GetSubscriptionId();
    }
    public class ArmResourceNode : GraphNode
    {
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string ResourceName { get; set; }
        public string Location { get; set; }
        public bool SystemMI { get; set; }

        public ArmResourceNode() { }
        public ArmResourceNode(string resourceType, string subscriptionId)
            : this(resourceType, null, subscriptionId, null, null) { }

        public ArmResourceNode(string resourceType, string subscriptionId, string resourceGroupName, string location)
            : this(resourceType, null, subscriptionId, resourceGroupName, null, location) { }

        public ArmResourceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string location = null)
        {
            UpdateTs = DateTime.UtcNow.Ticks;
            ResourceType = resourceType?.ToLowerInvariant();
            ResourceId = resourceId?.ToLowerInvariant();
            SubscriptionId = subscriptionId?.ToLowerInvariant();
            ResourceGroupName = resourceGroupName?.ToLowerInvariant();
            ResourceName = resourceName?.ToLowerInvariant();
            Location = location?.NormalizeLocation();
            //SystemMI = systemMI;
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
            var properties = new Dictionary<string, object>
            {
                // resourceType is partition key, cannot be updated
                //{ "resourceType", ResourceType },
                { "updateTs", UpdateTs },
                { "resourceId", ResourceId },
                { "subscriptionId", SubscriptionId },
                { "resourceGroupName", ResourceGroupName },
                { "resourceName", ResourceName }
            };

            if (!string.IsNullOrEmpty(Location))
            {
                properties.Add("location", Location);
            }

            return properties;
        }

        // Mainly for system MI
        // To be able to crawl same resource again with ManagedIdentityNode
        public override string GetHashString()
        {
            return $"{ResourceId}|{GetType()}";
        }

        public override string GetSubscriptionId()
        {
            return SubscriptionId;
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
