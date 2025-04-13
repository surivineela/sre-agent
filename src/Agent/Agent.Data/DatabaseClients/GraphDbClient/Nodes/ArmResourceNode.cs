// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
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

    public class AppHealthInfo
    {
        // last captured timestamp
        public DateTime LastDataCaptureTimeStampInUTC { get; set; } = DateTime.UtcNow;
        public ScorecardHealthState Health { get; set; } = ScorecardHealthState.Unknown;

        // availability
        public double? Availability { get; set; }

        // activity (requests/transactions)
        public double? Transactions { get; set; }

        // costs ($ USD)
        public double? Costs { get; set; }
        
        // average latency (ms)
        public double? AvgLatencyInMs { get; set; }

        public double? AvgMemoryUsage { get; set; }

        public double? AvgCpuUsage { get; set; }

        // maybe not needed? 
        public IDictionary<string, object> AdditionalMetrics { get; set; } = new Dictionary<string, object>();

        // time since lastActivity
        public DateTime? timeSinceLastActivity { get; set; }

        // if resource IsActive 
        public bool IsActive
        {
            get
            {
                if (Transactions != null && Transactions > 0 || AvgCpuUsage != null && AvgCpuUsage > 0 || AvgMemoryUsage != null && AvgMemoryUsage > 0)
                {
                    timeSinceLastActivity = DateTime.UtcNow;
                }

                // If we have scanned in the last 30 mins and never set a timeSinceLastActivity then it has never been active
                // If there's been no activity for 24 hours, it's inactive
                if ((DateTime.UtcNow - LastDataCaptureTimeStampInUTC) > TimeSpan.FromMinutes(30) && timeSinceLastActivity == null ||
                        (timeSinceLastActivity.HasValue && DateTime.UtcNow - timeSinceLastActivity.Value >= TimeSpan.FromHours(24)))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ScorecardHealthState
    {
        Healthy,
        Unhealthy,
        Unknown
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
        public AppHealthInfo AppHealthInfo { get; set; }

        [GraphProperty("isProd")]
        public bool IsProd
        {
            // to do: add logic to check tags
            get
            {
                return string.IsNullOrEmpty(ResourceName) || 
                       !ResourceName.Contains("dev", StringComparison.OrdinalIgnoreCase);
            }
        }

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
            string location = null,
            AppHealthInfo appHealthInfo = null)
        {
            UpdateTs = DateTime.UtcNow.Ticks;
            ResourceType = resourceType?.ToLowerInvariant();
            ResourceId = resourceId?.ToLowerInvariant();
            SubscriptionId = subscriptionId?.ToLowerInvariant();
            ResourceGroupName = resourceGroupName?.ToLowerInvariant();
            ResourceName = resourceName?.ToLowerInvariant();
            Location = location?.NormalizeLocation();
            AppHealthInfo = appHealthInfo;
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
            
            if (AppHealthInfo != null)
            {
                string jsonAppHealthInfo = JsonSerializer.Serialize(AppHealthInfo);
                properties["appHealthInfo"] = jsonAppHealthInfo;
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
