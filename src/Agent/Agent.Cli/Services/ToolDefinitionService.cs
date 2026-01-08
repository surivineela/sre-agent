// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Cli.Models;
using Agent.Framework;
using Agent.Plugins.Kusto.Tools;
using Agent.Plugins.Tools;

namespace Agent.Cli.Services;

/// <summary>
/// Service for discovering and managing YAML tool definitions and types.
/// </summary>
public static class ToolDefinitionService
{
    private static readonly Lazy<List<Assembly>> _relevantAssemblies = new(DiscoverRelevantAssemblies);
    private static readonly Lazy<List<ToolTypeInfo>> _cachedToolTypes = new(DiscoverToolTypes);
    private static readonly Lazy<List<ConnectorTypeInfo>> _cachedConnectorTypes = new(DiscoverConnectorTypes);

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
            return
            [
                typeof(KustoToolExecutorFactory).Assembly, // Agent.Plugins
                typeof(YamlToolDefinitionBase).Assembly,  // Agent.Framework
            ];
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

        return [.. toolTypes.OrderBy(t => t.Name)];
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

        return [.. connectorTypes.OrderBy(c => c.Name)];
    }

    /// <summary>
    /// Gets the connector name, trying different approaches to find a meaningful name.
    /// </summary>
    private static string GetConnectorName(Type type)
    {
        // Try to get name from DataConnectorAttribute if it exists
        var dataConnectorAttr = type.GetCustomAttribute<Core.DataConnectors.DataConnectorAttribute>();
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
            var t when ToolName.KustoTool == t => "Execute Kusto queries, functions, or scripts against Azure Data Explorer clusters",
            var t when ToolName.KustoQuery == t => "Execute raw Kusto queries with direct parameter support",
            var t when ToolName.LinkTool == t => "Link tool description",
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
