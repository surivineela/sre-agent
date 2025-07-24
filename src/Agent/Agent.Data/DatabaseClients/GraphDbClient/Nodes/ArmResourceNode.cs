// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public interface IResourceGraphNode
    {
        public string? GetNodeLabel();
        public string GetNodeId();
        public string? GetResourceType();
        public string? GetResourceKind();
        public IDictionary<string, object> GetNodeProperties();
    }

    public abstract class GraphNode : IResourceGraphNode
    {
        [GraphProperty("updateTs")]
        public long UpdateTs { get; set; }

        [GraphProperty("isDeleted")]
        public bool IsDeleted { get; set; } = false;

        public abstract string GetNodeId();

        public abstract string GetNodeLabel();

        protected GraphNode() { }

        protected GraphNode(IDictionary<string, object> properties)
        {
            SetNodeProperties(properties);
            UpdateTs = DateTime.UtcNow.Ticks;
        }

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
                    var value = attr is GraphJsonPropertyAttribute jsonAttr
                        ? JsonSerializer.Serialize(prop.GetValue(this))
                        : prop.GetValue(this);
                    if (value != null)
                    {
                        // Use the attribute's name as the key, property value as the value
                        properties[attr.PropertyName] = value;
                    }
                }
            }

            return properties;
        }

        /// <summary>
        /// This method is used to set the properties of the current instance based on the provided dictionary.
        /// It'd be called in the constructor of the derived class that wants to support reading a
        /// Dictionary&lt;string, object&gt; as a parameter for initializing its properties.
        /// </summary>
        private void SetNodeProperties(IDictionary<string, object> properties)
        {
            // List all the current type properties with [GraphProperty] attribute
            // Get the PropertyName, PropertyType and GraphPropertyName
            var graphProperties = GetType().GetProperties()
                .Select(prop =>
                {
                    var attribute = prop.GetCustomAttributes(typeof(GraphPropertyAttribute), true)
                        .Cast<GraphPropertyAttribute>()
                        .FirstOrDefault();
                    return attribute != null
                        ? new
                        {
                            PropertyName = prop.Name,
                            PropertyType = prop.PropertyType,
                            GraphPropertyName = attribute.PropertyName
                        }
                        : null;
                })
                .Where(x => x != null)
                .ToList();

            // Set the properties on the current instance.
            foreach (var prop in graphProperties)
            {
                if (prop != null && !string.IsNullOrEmpty(prop.GraphPropertyName) && properties.TryGetValue(prop.GraphPropertyName, out var value))
                {
                    var property = GetType().GetProperty(prop.PropertyName);
                    if (property != null && property.CanWrite)
                    {
                        if (value is IEnumerable<object> enumerable)
                        {
                            if (property.PropertyType.IsAssignableFrom(typeof(IEnumerable<object>)))
                            {
                                TryAssignValue(property, enumerable);
                            }
                            else if (enumerable.Any())
                            {
                                var firstValue = enumerable.FirstOrDefault();
                                TryAssignValue(property, firstValue);
                            }
                        }
                        else
                        {
                            TryAssignValue(property, value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method is used to assign a value to a property of the current instance.
        /// the graph client/service changes booleans to strings. Enums unknown.
        /// /// </summary>
        private bool TryAssignValue(PropertyInfo property, object? value)
        {
            // Check if the property can be written to
            if (!property.CanWrite)
            {
                // throw new InvalidOperationException($"Property '{property.Name}' is read-only.");
                return false;
            }

            var targetType = Nullable.GetUnderlyingType((property.PropertyType)) ?? property.PropertyType;
            object? convertedValue = null;

            if (value != null)
            {
                var valueType = value.GetType();
                if (targetType.IsAssignableFrom(valueType))
                {
                    convertedValue = value;
                }
                else
                {
                    try
                    {
                        convertedValue = Convert.ChangeType(value, targetType);
                    }
                    catch (InvalidCastException)
                    {
                        // throw new ArgumentException($"Cannot convert value of type '{valueType}' to property type '{targetType}'.");
                        return false;
                    }
                }
            }
            else if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
            {
                // assigning null to a non-nullable value type property
                // throw new InvalidOperationException($"Cannot assign null to non-nullable property '{propertyName}'.");
                return false;
            }

            try
            {
                property.SetValue(this, convertedValue);
                return true;
            }
            catch (Exception)
            {
                // Handle the exception as needed
                // throw new InvalidOperationException($"Failed to set property '{property.Name}': {ex.Message}", ex);
                return false;
            }
        }

        public abstract string GetResourceType();

        public abstract string GetResourceKind();

        public abstract void SetResourceKind(string NewResourceKind);

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
        public DateTime? TimeSinceLastActivity { get; set; }

        // if resource IsActive
        [JsonIgnore]
        public bool IsActive
        {
            get
            {
                if (Transactions != null && Transactions > 0 || AvgCpuUsage != null && AvgCpuUsage > 0 || AvgMemoryUsage != null && AvgMemoryUsage > 0)
                {
                    TimeSinceLastActivity = DateTime.UtcNow;
                }

                // If we have scanned in the last 30 mins and never set a timeSinceLastActivity then it has never been active
                // If there's been no activity for 24 hours, it's inactive
                if ((DateTime.UtcNow - LastDataCaptureTimeStampInUTC) > TimeSpan.FromMinutes(30) && TimeSinceLastActivity == null ||
                        (TimeSinceLastActivity.HasValue && DateTime.UtcNow - TimeSinceLastActivity.Value >= TimeSpan.FromHours(24)))
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
        Degraded,
        Unhealthy,
        Unknown
    }

    public class ArmResourceNode : GraphNode
    {
        public string ResourceType { get; set; } = string.Empty;

        public string ResourceKind { get; set; } = string.Empty;

        [GraphProperty("resourceId")]
        public string ResourceId { get; set; } = string.Empty;

        [GraphProperty("subscriptionId")]
        public string SubscriptionId { get; set; } = string.Empty;

        [GraphProperty("resourceGroupName")]
        public string? ResourceGroupName { get; set; }

        [GraphProperty("resourceName")]
        public string? ResourceName { get; set; }

        [GraphProperty("location")]
        public string? Location { get; set; }

        [GraphJsonProperty("appHealthInfo")]
        public AppHealthInfo? AppHealthInfo { get; set; }

        [GraphJsonProperty("remarks")]
        public string? Remarks { get; set; }

        public ArmResourceNode() { }

        public ArmResourceNode(IDictionary<string, object> properties)
            : base(properties) { }

        public ArmResourceNode(string resourceType, string subscriptionId)
            : this(resourceType, string.Empty, subscriptionId, string.Empty, string.Empty) { }

        public ArmResourceNode(string resourceType, string subscriptionId, string resourceGroupName, string location)
            : this(resourceType, string.Empty, subscriptionId, resourceGroupName, string.Empty, location) { }

        public ArmResourceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string? resourceKind = null,
            string? remarks = null,
            string? location = null,
            AppHealthInfo? appHealthInfo = null)
        {
            UpdateTs = DateTime.UtcNow.Ticks;
            ResourceKind = ResourceKindHelper.getResourceKind(resourceType, resourceKind);
            ResourceType = resourceType ?? string.Empty.ToLowerInvariant();
            ResourceId = resourceId ?? string.Empty.ToLowerInvariant();
            SubscriptionId = subscriptionId ?? string.Empty.ToLowerInvariant();
            ResourceGroupName = resourceGroupName ?? string.Empty.ToLowerInvariant();
            ResourceName = resourceName ?? string.Empty.ToLowerInvariant();
            Location = location?.NormalizeLocation() ?? string.Empty;
            AppHealthInfo = appHealthInfo ?? new AppHealthInfo();
            Remarks = remarks;
        }

        public override string GetNodeLabel()
        {
            //var parts = ResourceType.Split('/');
            //return parts[parts.Length - 1];

            // use full arm type to avoid potential conflict
            return ResourceType?.ToLower() ?? string.Empty;
        }

        public override string GetNodeId()
        {
            return ResourceId;
        }

        public override string GetResourceType()
        {
            return ResourceType;
        }

        public override string GetResourceKind()
        {
            return ResourceKind;
        }

        public override void SetResourceKind(string NewResourceKind)
        {
            ResourceKind = NewResourceKind;
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
