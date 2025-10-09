// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
using Agent.Plugins.Kusto.Tools;
using YamlDotNet.Serialization;

namespace Agent.Cli.Services;

/// <summary>
/// Service for discovering and managing YAML tool definitions and types.
/// </summary>
public static class ToolDefinitionService
{
    private static readonly Lazy<List<Assembly>> _relevantAssemblies = new(DiscoverRelevantAssemblies);
    private static readonly Lazy<List<ToolTypeInfo>> _cachedToolTypes = new(() => DiscoverToolTypes());
    private static readonly Lazy<List<ConnectorTypeInfo>> _cachedConnectorTypes = new(() => DiscoverConnectorTypes());

    /// <summary>
    /// Gets all available tool types by scanning for ToolTypeAttribute in assemblies.
    /// </summary>
    public static List<ToolTypeInfo> GetAvailableToolTypes()
    {
        return _cachedToolTypes.Value;
    }

    /// <summary>
    /// Dynamically discovers assemblies that contain tool or connector definitions.
    /// </summary>
    private static List<Assembly> DiscoverRelevantAssemblies()
    {
        var assemblies = new List<Assembly>();

        try
        {
            // Get all loaded assemblies
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in loadedAssemblies)
            {
                try
                {
                    // Skip system assemblies and other non-relevant assemblies
                    var assemblyName = assembly.GetName().Name ?? string.Empty;
                    if (assemblyName.StartsWith("System.") ||
                        assemblyName.StartsWith("Microsoft.") && !assemblyName.Contains("Agent") ||
                        assemblyName.StartsWith("netstandard") ||
                        assemblyName.StartsWith("mscorlib"))
                        continue;

                    // Check if assembly contains relevant types
                    var types = assembly.GetTypes();
                    var hasToolTypes = types.Any(t => t.GetCustomAttribute<ToolTypeAttribute>() != null);
                    var hasConnectorTypes = types.Any(t => t.IsSubclassOf(typeof(DataConnectorDefinitionBase)) && !t.IsAbstract);

                    if (hasToolTypes || hasConnectorTypes)
                    {
                        assemblies.Add(assembly);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                    continue;
                }
                catch (Exception)
                {
                    // Skip assemblies with other loading issues
                    continue;
                }
            }
        }
        catch (Exception)
        {
            // Fallback to known assemblies if dynamic discovery fails
            return new List<Assembly>
            {
                typeof(KustoToolType).Assembly, // Agent.Plugins
                typeof(YamlToolDefinitionBase).Assembly,  // Agent.Framework
            };
        }

        return assemblies;
    }

    /// <summary>
    /// Discovers all tool types across relevant assemblies.
    /// </summary>
    private static List<ToolTypeInfo> DiscoverToolTypes()
    {
        var toolTypes = new List<ToolTypeInfo>();

        foreach (var assembly in _relevantAssemblies.Value)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<ToolTypeAttribute>() != null)
                    .ToList();

                foreach (var type in types)
                {
                    var attribute = type.GetCustomAttribute<ToolTypeAttribute>()!;
                    var toolTypeInfo = new ToolTypeInfo
                    {
                        Name = attribute.Name,
                        TypeName = type.Name,
                        Assembly = assembly.GetName().Name ?? string.Empty,
                        Namespace = type.Namespace ?? string.Empty,
                        Description = GetDynamicToolTypeDescription(type, attribute.Name)
                    };

                    toolTypes.Add(toolTypeInfo);
                }
            }
            catch (Exception)
            {
                // Skip assemblies with loading issues
                continue;
            }
        }

        return toolTypes.OrderBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Gets detailed information about a specific tool type.
    /// </summary>
    public static ToolTypeDetails? GetToolTypeDetails(string toolTypeName)
    {
        var toolTypes = GetAvailableToolTypes();
        var toolType = toolTypes.FirstOrDefault(t =>
            t.Name.Equals(toolTypeName, StringComparison.OrdinalIgnoreCase));

        if (toolType == null)
            return null;

        // Find the actual type across all relevant assemblies
        foreach (var assembly in _relevantAssemblies.Value)
        {
            try
            {
                var type = assembly.GetTypes()
                    .FirstOrDefault(t => t.GetCustomAttribute<ToolTypeAttribute>()?.Name
                        .Equals(toolTypeName, StringComparison.OrdinalIgnoreCase) == true);

                if (type != null)
                {
                    return new ToolTypeDetails
                    {
                        Name = toolType.Name,
                        TypeName = toolType.TypeName,
                        Assembly = toolType.Assembly,
                        Namespace = toolType.Namespace,
                        Description = toolType.Description,
                        SampleYaml = GenerateDynamicSampleYaml(type, toolTypeName),
                        SupportedProperties = GetDynamicSupportedProperties(type)
                    };
                }
            }
            catch (Exception)
            {
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all available connector types by scanning DataConnectorDefinitionBase implementations.
    /// </summary>
    public static List<ConnectorTypeInfo> GetAvailableConnectorTypes()
    {
        return _cachedConnectorTypes.Value;
    }

    /// <summary>
    /// Discovers all connector types across relevant assemblies.
    /// </summary>
    private static List<ConnectorTypeInfo> DiscoverConnectorTypes()
    {
        var connectorTypes = new List<ConnectorTypeInfo>();

        foreach (var assembly in _relevantAssemblies.Value)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(DataConnectorDefinitionBase)) && !t.IsAbstract)
                    .ToList();

                foreach (var type in types)
                {
                    var connectorInfo = new ConnectorTypeInfo
                    {
                        Name = GetConnectorName(type),
                        TypeName = type.Name,
                        Assembly = assembly.GetName().Name ?? string.Empty,
                        Namespace = type.Namespace ?? string.Empty,
                        Description = GetDynamicConnectorTypeDescription(type)
                    };

                    connectorTypes.Add(connectorInfo);
                }
            }
            catch (Exception)
            {
                // Skip assemblies with loading issues
                continue;
            }
        }

        return connectorTypes.OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Gets the connector name, trying different approaches to find a meaningful name.
    /// </summary>
    private static string GetConnectorName(Type type)
    {
        // Try to get name from DataConnectorAttribute if it exists
        var dataConnectorAttr = type.GetCustomAttribute<Agent.Core.DataConnectors.DataConnectorAttribute>();
        if (dataConnectorAttr != null && !string.IsNullOrEmpty(dataConnectorAttr.Type))
        {
            return dataConnectorAttr.Type;
        }

        // Fallback to class name, removing "Definition" suffix if present
        var typeName = type.Name;
        if (typeName.EndsWith("Definition"))
        {
            return typeName.Substring(0, typeName.Length - "Definition".Length);
        }

        return typeName;
    }

    /// <summary>
    /// Gets a dynamic description for a tool type by examining the class and its attributes.
    /// </summary>
    private static string GetDynamicToolTypeDescription(Type type, string toolTypeName)
    {
        // Try to get description from DescriptionAttribute
        var descAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
        {
            return descAttr.Description;
        }

        // Try to get XML documentation summary (if available)
        var xmlDoc = GetXmlDocumentation(type);
        if (!string.IsNullOrEmpty(xmlDoc))
        {
            return xmlDoc;
        }

        // Fallback to known descriptions or generate a generic one
        return toolTypeName switch
        {
            "KustoTool" => "Execute Kusto queries, functions, or scripts against Azure Data Explorer clusters",
            "KustoQuery" => "Execute raw Kusto queries with direct parameter support",
            _ => $"Tool type '{toolTypeName}' implemented by {type.Name}"
        };
    }

    /// <summary>
    /// Gets a dynamic description for a connector type.
    /// </summary>
    private static string GetDynamicConnectorTypeDescription(Type type)
    {
        // Try to get description from DescriptionAttribute
        var descAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
        {
            return descAttr.Description;
        }

        // Try to get XML documentation summary (if available)
        var xmlDoc = GetXmlDocumentation(type);
        if (!string.IsNullOrEmpty(xmlDoc))
        {
            return xmlDoc;
        }

        // Fallback to known descriptions or generate a generic one
        return type.Name switch
        {
            "KustoConnectorDefinition" => "Connects to Azure Data Explorer (Kusto) clusters for data querying",
            _ => $"Data connector implemented by {type.Name}"
        };
    }

    /// <summary>
    /// Attempts to extract XML documentation summary for a type.
    /// </summary>
    private static string GetXmlDocumentation(Type type)
    {
        // This is a simplified approach - in a full implementation,
        // you might want to parse XML documentation files
        return string.Empty;
    }

    /// <summary>
    /// Dynamically generates sample YAML based on the tool type's properties and structure.
    /// </summary>
    private static string GenerateDynamicSampleYaml(Type type, string toolTypeName)
    {
        try
        {
            // Create an instance of the tool type to inspect its properties
            var instance = Activator.CreateInstance(type);
            if (instance == null)
                return GenerateFallbackYaml(toolTypeName);

            var yaml = new StringBuilder();

            // Generate YAML based on the tool's properties
            yaml.AppendLine($"name: My{toolTypeName}");
            yaml.AppendLine($"type: {toolTypeName}");

            // Add connector if the tool has one
            if (HasProperty(type, "Connector"))
            {
                yaml.AppendLine("connector: my-connector");
            }

            yaml.AppendLine($"description: Sample {toolTypeName} description");

            // Get all YAML-serializable properties and add sample values
            var properties = GetYamlProperties(type);
            foreach (var prop in properties)
            {
                var sampleValue = GenerateSampleValue(prop);
                if (!string.IsNullOrEmpty(sampleValue))
                {
                    var yamlName = GetYamlPropertyName(prop);
                    if (!IsCommonProperty(yamlName)) // Skip already added common properties
                    {
                        yaml.AppendLine($"{yamlName}: {sampleValue}");
                    }
                }
            }

            // Add sample parameters section
            yaml.AppendLine("parameters:");
            yaml.AppendLine("  - name: sampleParam");
            yaml.AppendLine("    type: string");
            yaml.AppendLine("    required: false");
            yaml.AppendLine("    description: Sample parameter description");

            return yaml.ToString();
        }
        catch (Exception)
        {
            // Fallback to known templates or generic template
            return GenerateFallbackYaml(toolTypeName);
        }
    }

    /// <summary>
    /// Generates fallback YAML when dynamic generation fails.
    /// </summary>
    private static string GenerateFallbackYaml(string toolTypeName)
    {
        return toolTypeName switch
        {
            "KustoTool" => @"name: MyKustoTool
type: KustoTool
connector: analytics-cluster
mode: query
description: |
  !!!!!!!!IMPORTANT!!!!! THIS IS A PLACEHOLDER TEMPLATE: <PLACE YOUR TOOL DESCRIPTION HERE AND CHANGE THE VALUES ABOVE>
  Purpose:
  Comprehensive check for resource impact scenarios affecting a subscription or tenant

  Usage - Call with any ONE of these parameters:
  - SubscriptionId: Check specific subscription (GUID format)
  - Tenant: Check all subscriptions under a tenant (GUID format)

  Output Format:
  Returns table data with columns: Scenario, Tenant, Subscription, Region, ResourceGroup, ResourceName, ResourceType, ImpactLevel

  CRITICAL - When data is returned:
  1. ALWAYS present results in a clear table format showing ALL rows
  2. Group results by Scenario and count affected resources per scenario
  3. Emphasize required actions and deadlines
  4. Provide scenario-specific next steps
  5. NEVER TRUNCATE RESULTS - show every single affected resource
  6. If no data returned, confirm the subscription/tenant is not currently affected

  Note: This tool queries the comprehensive analytics data source for accurate, real-time impact assessment.
query: |
  cluster('analytics-cluster.region.kusto.windows.net').database('ImpactAnalysis').ResourceImpactTable
  | extend parsed = parse_json(ImpactData)
  | mv-expand scenario = bag_keys(parsed)
  | extend scenarioData = parsed[tostring(scenario)]
  | mv-expand row = scenarioData
  | evaluate bag_unpack(row)
  | extend Scenario = scenario
  | distinct tostring(Scenario), Tenant, Subscription, Region, ResourceGroup, ResourceName, ResourceType, ImpactLevel
  | where
      (""##SubscriptionId##"" != """" and Subscription == ""##SubscriptionId##"") or
      (""##Tenant##"" != """" and Tenant == ""##Tenant##"")
  | order by Scenario, ImpactLevel desc
parameters:
  - name: SubscriptionId
    type: string
    required: false
    description: The subscription ID (GUID) to check for impact scenarios
    map_to: args
    target: dictionary:args:string
    value: """"
  - name: Tenant
    type: string
    required: false
    description: The tenant ID to check for impact scenarios across all subscriptions
    map_to: args
    target: dictionary:args:string
    value: """"",
            "KustoQuery" => @"name: MyKustoQuery
type: KustoQuery
connector: my-kusto-connector
description: Direct Kusto query execution
query: |
  MyTable | take 10
parameters:
  - name: limit
    type: int
    required: false
    description: Number of results to return",
            _ => $"# Sample YAML for {toolTypeName}\nname: My{toolTypeName}\ntype: {toolTypeName}\nconnector: my-connector\ndescription: Sample tool description"
        };
    }

    /// <summary>
    /// Gets all properties that have YAML serialization attributes.
    /// </summary>
    private static PropertyInfo[] GetYamlProperties(Type type)
    {
        return type.GetProperties()
            .Where(p => p.GetCustomAttribute<YamlMemberAttribute>() != null ||
                       p.GetCustomAttribute<YamlDotNet.Serialization.YamlMemberAttribute>() != null ||
                       ShouldIncludeProperty(p))
            .ToArray();
    }

    /// <summary>
    /// Determines if a property should be included even without explicit YAML attributes.
    /// </summary>
    private static bool ShouldIncludeProperty(PropertyInfo prop)
    {
        // Include common properties that are typically serialized
        var commonProps = new[] { "Name", "Type", "Connector", "Description", "Parameters",
                                 "Mode", "Database", "Query", "ClusterHint" };
        return commonProps.Contains(prop.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the YAML property name for a PropertyInfo.
    /// </summary>
    private static string GetYamlPropertyName(PropertyInfo prop)
    {
        var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
        if (yamlAttr != null && !string.IsNullOrEmpty(yamlAttr.Alias))
        {
            return yamlAttr.Alias;
        }

        var yamlDotNetAttr = prop.GetCustomAttribute<YamlDotNet.Serialization.YamlMemberAttribute>();
        if (yamlDotNetAttr != null && !string.IsNullOrEmpty(yamlDotNetAttr.Alias))
        {
            return yamlDotNetAttr.Alias;
        }

        // Convert property name to snake_case or lowercase
        return ConvertToYamlCase(prop.Name);
    }

    /// <summary>
    /// Converts a property name to YAML-friendly casing.
    /// </summary>
    private static string ConvertToYamlCase(string propertyName)
    {
        // Convert PascalCase to snake_case
        var result = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            if (i > 0 && char.IsUpper(propertyName[i]))
            {
                result.Append('_');
            }
            result.Append(char.ToLower(propertyName[i]));
        }
        return result.ToString();
    }

    /// <summary>
    /// Generates a sample value for a property based on its type.
    /// </summary>
    private static string GenerateSampleValue(PropertyInfo prop)
    {
        var propType = prop.PropertyType;

        // Handle nullable types
        if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            propType = propType.GetGenericArguments()[0];
        }

        return propType.Name switch
        {
            "String" => GetSampleStringValue(prop.Name),
            "Int32" or "Int64" => "100",
            "Boolean" => "true",
            "List`1" => "[]", // Empty list
            _ => "\"\""
        };
    }

    /// <summary>
    /// Gets sample string values based on property names.
    /// </summary>
    private static string GetSampleStringValue(string propertyName)
    {
        return propertyName.ToLower() switch
        {
            "mode" => "query",
            "database" => "MyDatabase",
            "cluster" or "clusterhint" or "cluster_hint" => "westus",
            "query" => "|\n  MyTable | take 10",
            "description" => "Sample description",
            _ => "sample_value"
        };
    }

    /// <summary>
    /// Checks if a property name is a common property that's already handled.
    /// </summary>
    private static bool IsCommonProperty(string yamlName)
    {
        var common = new[] { "name", "type", "connector", "description", "parameters" };
        return common.Contains(yamlName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type has a specific property.
    /// </summary>
    private static bool HasProperty(Type type, string propertyName)
    {
        return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) != null;
    }

    /// <summary>
    /// Dynamically discovers all supported properties for a tool type.
    /// </summary>
    private static List<string> GetDynamicSupportedProperties(Type type)
    {
        var properties = new List<string>();

        try
        {
            // Get all public properties
            var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in allProperties)
            {
                // Check if property should be included
                if (ShouldIncludePropertyInList(prop))
                {
                    var yamlName = GetYamlPropertyName(prop);
                    var typeInfo = GetPropertyTypeInfo(prop);
                    var requiredInfo = IsPropertyRequired(prop) ? " (required)" : " (optional)";

                    properties.Add($"{yamlName} ({typeInfo}){requiredInfo}");
                }
            }
        }
        catch (Exception)
        {
            // Fallback for specific known types
            return GetFallbackProperties(type);
        }

        return properties.OrderBy(p => p).ToList();
    }

    /// <summary>
    /// Determines if a property should be included in the supported properties list.
    /// </summary>
    private static bool ShouldIncludePropertyInList(PropertyInfo prop)
    {
        // Exclude properties that are typically not user-configurable
        var excludedProperties = new[] { "GetType", "ToString", "Equals", "GetHashCode" };
        if (excludedProperties.Contains(prop.Name))
            return false;

        // Include properties with YAML attributes
        if (prop.GetCustomAttribute<YamlMemberAttribute>() != null ||
            prop.GetCustomAttribute<YamlDotNet.Serialization.YamlMemberAttribute>() != null)
            return true;

        // Include properties that are settable and commonly used
        return prop.CanWrite && ShouldIncludeProperty(prop);
    }

    /// <summary>
    /// Gets user-friendly type information for a property.
    /// </summary>
    private static string GetPropertyTypeInfo(PropertyInfo prop)
    {
        var propType = prop.PropertyType;

        // Handle nullable types
        if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var innerType = propType.GetGenericArguments()[0];
            return $"{GetSimpleTypeName(innerType)}?";
        }

        // Handle generic types
        if (propType.IsGenericType)
        {
            var genericTypeDef = propType.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(List<>))
            {
                var elementType = propType.GetGenericArguments()[0];
                return $"List<{GetSimpleTypeName(elementType)}>";
            }
            if (genericTypeDef == typeof(Dictionary<,>))
            {
                var keyType = propType.GetGenericArguments()[0];
                var valueType = propType.GetGenericArguments()[1];
                return $"Dictionary<{GetSimpleTypeName(keyType)}, {GetSimpleTypeName(valueType)}>";
            }
        }

        return GetSimpleTypeName(propType);
    }

    /// <summary>
    /// Gets a simplified type name for display.
    /// </summary>
    private static string GetSimpleTypeName(Type type)
    {
        return type.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Boolean" => "bool",
            "Double" => "double",
            "Decimal" => "decimal",
            "DateTime" => "datetime",
            _ => type.Name
        };
    }

    /// <summary>
    /// Determines if a property is required based on attributes or other indicators.
    /// </summary>
    private static bool IsPropertyRequired(PropertyInfo prop)
    {
        // Check for Required attribute
        if (prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null)
            return true;

        // Check YAML member attribute for required flag
        var yamlAttr = prop.GetCustomAttribute<YamlMemberAttribute>();
        if (yamlAttr != null)
        {
            // YamlMember doesn't have a Required property, so check other indicators
            return false;
        }

        // Check if property type is non-nullable value type
        var propType = prop.PropertyType;
        if (propType.IsValueType && Nullable.GetUnderlyingType(propType) == null)
        {
            // Non-nullable value types could be considered required
            var commonRequiredProps = new[] { "Name", "Type" };
            return commonRequiredProps.Contains(prop.Name, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Gets fallback properties for known types when dynamic discovery fails.
    /// </summary>
    private static List<string> GetFallbackProperties(Type type)
    {
        var typeName = type.Name;
        return typeName switch
        {
            "KustoToolDefinition" => new List<string>
            {
                "name (string) (required)",
                "type (string) (required)",
                "connector (string) (required)",
                "description (string) (optional)",
                "mode (string) (optional)",
                "database (string) (optional)",
                "cluster_hint (string) (optional)",
                "query (string) (optional)",
                "parameters (List<YamlParameter>) (optional)"
            },
            _ => new List<string>
            {
                "name (string) (required)",
                "type (string) (required)",
                "connector (string) (optional)",
                "description (string) (optional)",
                "parameters (List<YamlParameter>) (optional)"
            }
        };
    }
}

/// <summary>
/// Information about a tool type.
/// </summary>
public class ToolTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Assembly { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Detailed information about a tool type.
/// </summary>
public class ToolTypeDetails : ToolTypeInfo
{
    public string SampleYaml { get; set; } = string.Empty;
    public List<string> SupportedProperties { get; set; } = new();
}

/// <summary>
/// Information about a connector type.
/// </summary>
public class ConnectorTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Assembly { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
