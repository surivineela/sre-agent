// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

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
            if (prop.GetCustomAttribute<GraphPropertyAttribute>(true) is { } attr)
            {
                var value = attr is GraphJsonPropertyAttribute
                    ? JsonSerializer.Serialize(prop.GetValue(this))
                    : prop.GetValue(this);
                if (value is not null)
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

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
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
