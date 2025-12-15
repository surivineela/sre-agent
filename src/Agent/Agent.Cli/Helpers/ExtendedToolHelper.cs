// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating and managing ExtendedToolV2 instances.
/// </summary>
public static class ExtendedToolHelper
{
    /// <summary>
    /// Gets all available tool types supported by the CLI.
    /// </summary>
    public static List<ToolTypeInfo> GetAvailableToolTypes()
    {
        return
        [
            new ToolTypeInfo
            {
                Name = ToolName.KustoTool,
                Description = "Execute Kusto queries against Azure Data Explorer clusters"
            },
            new ToolTypeInfo
            {
                Name = ToolName.LinkTool,
                Description = "Generate URLs based on templates with parameter substitution"
            }
        ];
    }

    /// <summary>
    /// Creates a KustoTool ExtendedToolV2 instance with the provided parameters.
    /// Defaults: connector, database, description always use defaults when not provided.
    /// If query is not provided, uses default query + default parameters.
    /// If query IS provided, uses user's parameters (no defaults added).
    /// </summary>
    public static ExtendedToolV2 CreateKustoTool(
        string name,
        string? connector = null,
        string? database = null,
        string? description = null,
        string? query = null,
        string[]? parameters = null)
    {
        var defaultConnector = "analytics-cluster";
        var defaultDatabase = "kustodb";

        var defaultDescription = @"!!!!!!!!IMPORTANT!!!!! THIS IS A PLACEHOLDER TEMPLATE: <PLACE YOUR TOOL DESCRIPTION HERE AND CHANGE THE VALUES ABOVE>
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

Note: This tool queries the comprehensive analytics data source for accurate, real-time impact assessment.";

        var defaultQuery = @"cluster('analytics-cluster.region.kusto.windows.net').database('ImpactAnalysis').ResourceImpactTable
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
| order by Scenario, ImpactLevel desc";

        var defaultParameters = new[]
        {
            "SubscriptionId:The subscription ID (GUID) to check for impact scenarios",
            "Tenant:The tenant ID to check for impact scenarios across all subscriptions"
        };

        // Apply default template only when query is not provided
        var effectiveQuery = query ?? defaultQuery;
        var effectiveParameters = query == null ? defaultParameters : parameters;

        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = name
            },
            Spec = new KustoToolSpecV2
            {
                Type = ToolName.KustoTool,
                Connector = connector ?? defaultConnector,
                Database = database ?? defaultDatabase,
                Description = description ?? defaultDescription,
                Query = effectiveQuery,
                Parameters = CreateParameterSpecs(effectiveParameters, isKustoTool: true)
            }
        };

        return tool;
    }

    /// <summary>
    /// Creates a LinkTool ExtendedToolV2 instance with the provided parameters.
    /// Defaults: template and parameters use defaults when not provided.
    /// If template is not provided, uses default template + default parameters.
    /// If template IS provided, uses user's parameters (no defaults added).
    /// </summary>
    public static ExtendedToolV2 CreateLinkTool(
        string name,
        string? description = null,
        string? template = null,
        string[]? parameters = null)
    {
        var defaultTemplate = "https://example.com/{resourceId}";
        var defaultParameters = new[]
        {
            "resourceId:The resource identifier to include in the URL"
        };

        // Apply default template and parameters only when template is not provided
        var effectiveTemplate = template ?? defaultTemplate;
        var effectiveParameters = template == null ? defaultParameters : parameters;

        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = name
            },
            Spec = new LinkToolSpecV2
            {
                Type = ToolName.LinkTool,
                Connector = string.Empty,
                Description = description ?? "Sample LinkTool description",
                Template = effectiveTemplate,
                Parameters = CreateParameterSpecs(effectiveParameters, isKustoTool: false)
            }
        };

        return tool;
    }

    /// <summary>
    /// Creates parameter specifications from parameter names.
    /// Supports format: "name" or "name:description"
    /// Each parameter gets: type=string, required=true, description="The {name} parameter" (or custom description if provided)
    /// </summary>
    private static List<ToolParameterV2>? CreateParameterSpecs(string[]? parameters, bool isKustoTool = false)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return null;
        }

        var parsedParameters = new List<ToolParameterV2>();

        foreach (var param in parameters)
        {
            var trimmedParam = param.Trim();
            if (string.IsNullOrWhiteSpace(trimmedParam))
            {
                continue;
            }

            // Parse parameter format: "name" or "name:description"
            var parts = trimmedParam.Split(':', 2);
            var paramName = parts[0].Trim();
            var paramDescription = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
                ? parts[1].Trim()
                : $"The {paramName} parameter";

            var toolParam = new ToolParameterV2
            {
                Name = paramName,
                Type = "string",
                Description = paramDescription,
                Required = true,
                MapTo = string.Empty,
                Target = string.Empty
            };

            // Add KustoTool-specific properties
            if (isKustoTool)
            {
                toolParam.MapTo = "args";
                toolParam.Target = "dictionary:args:string";
            }

            parsedParameters.Add(toolParam);
        }

        return parsedParameters.Count > 0 ? parsedParameters : null;
    }

    /// <summary>
    /// Finds a tool YAML file by searching recursively under the tools directory.
    /// Supports flexible folder organization.
    /// </summary>
    /// <param name="toolName">The name of the tool to find</param>
    /// <returns>The full path to the tool YAML file, or null if not found</returns>
    public static string? FindToolFile(string toolName)
    {
        const string toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            return null;
        }

        // First, try the legacy structure: tools/{toolName}/{toolName}.yaml
        var legacyPath = Path.Combine(toolsDir, toolName, $"{toolName}.yaml");
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        // Then try the flat structure: tools/{toolName}.yaml
        var flatPath = Path.Combine(toolsDir, $"{toolName}.yaml");
        if (File.Exists(flatPath))
        {
            return flatPath;
        }

        // Finally, search recursively for any YAML file with the matching tool name
        var yamlFiles = Directory.GetFiles(toolsDir, "*.yaml", SearchOption.AllDirectories);

        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Equals(toolName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects the YAML API version of a tool file.
    /// </summary>
    /// <param name="filePath">Path to the tool YAML file</param>
    /// <returns>The detected YamlApiVersion, or null if not a valid extended tool YAML</returns>
    public static YamlApiVersion? DetectVersion(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var yamlContent = File.ReadAllText(filePath);

            // Try to deserialize as ResourceModel to get Kind and ApiVersion
            var deserializer = ResourceModel.GetDeserializerBuilder().Build();

            var resourceModel = deserializer.Deserialize<ResourceModel>(yamlContent);

            if (resourceModel == null)
            {
                return null;
            }

            // Check if this has Kind field (V2 format)
            if (string.Equals(resourceModel.Kind, ResourceModel.ResourceKind.ExtendedAgentToolV2, StringComparison.OrdinalIgnoreCase))
            {
                var version = YamlApiVersion.Parse(resourceModel.ApiVersion);
                return version;
            }

            // No recognized Kind field - check if it's a V1 tool by looking for V1-specific flat structure
            // V1 tools have flat fields like name, type at root level
            var dict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            if (dict != null && dict.ContainsKey("name") && dict.ContainsKey("type"))
            {
                // Verify the type is a valid tool type
                var typeValue = dict["type"]?.ToString();
                var availableTypes = GetAvailableToolTypes();
                if (!string.IsNullOrWhiteSpace(typeValue) &&
                    availableTypes.Any(t => t.Name.Equals(typeValue, StringComparison.OrdinalIgnoreCase)))
                {
                    return YamlApiVersion.V1;
                }
            }

            // Not a recognized tool format
            return null;
        }
        catch
        {
            return null;
        }
    }


}
