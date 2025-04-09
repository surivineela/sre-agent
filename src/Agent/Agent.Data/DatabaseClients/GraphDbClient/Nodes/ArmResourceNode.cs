// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.Attributes;

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
        [GraphProperty("updateTs")]
        public long UpdateTs { get; set; }
        public abstract string GetNodeId();
        public abstract string GetNodeLabel();
        public virtual IDictionary<string, object> GetNodeProperties()
        {
            var properties = new Dictionary<string, object>();

            var type = GetType();
            var props = type.GetProperties();

            foreach (var prop in props)
            {
                // Check if property has GraphProperty attribute
                var attr = prop.GetCustomAttributes(typeof(GraphPropertyAttribute), true)
                            .FirstOrDefault() as GraphPropertyAttribute;

                if (attr != null)
                {
                    // Use the attribute's name as the key, property value as the value
                    var value = prop.GetValue(this);
                    if (value != null)
                    {
                        properties[attr.PropertyName] = value;
                    }
                }
            }

            return properties;
        }

        public abstract string GetResourceType();

        public abstract string GetHashString();
        public abstract string GetSubscriptionId();
    }

    public class Scorecard
    {
        // last captured timestamp
        public DateTime LastDataCaptureTimeStampInUTC { get; set; } = DateTime.UtcNow;

        // availability
        public double? Availability { get; set; }

        // activity (requests/transactions)
        public long? Transactions { get; set; }

        // costs ($ USD)
        public double? Costs { get; set; }

        // average latency (ms)
        public double? AvgLatencyInMs { get; set; }
    }

    public class ArmResourceNode : GraphNode
    {
        public string ResourceType { get; set; }
        [GraphProperty("resourceId")]
        public string ResourceId { get; set; }
        [GraphProperty("subscriptionId")]
        public string SubscriptionId { get; set; }
        [GraphProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }
        [GraphProperty("resourceName")]
        public string ResourceName { get; set; }
        [GraphProperty("location")]
        public string Location { get; set; }
        public Scorecard Scorecard { get; set; }

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
            Scorecard = new Scorecard();
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
