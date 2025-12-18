// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Cli.Mcp.Tools;

/// <summary>
/// Provides platform tool names from PublishedTools.json and ICM plugin definitions.
/// Used by MCP tools to identify built-in vs custom tools.
/// </summary>
public static class PlatformToolsProvider
{
    private static readonly Lazy<HashSet<string>> _platformTools = new(LoadPlatformTools);
    private static readonly object _lock = new();

    /// <summary>
    /// ICM tools from ICMPluginDefinition - these are code-defined and stable.
    /// </summary>
    private static readonly string[] IcmTools =
    [
        "GetIncidentInfo",
        "GetCustomFields",
        "SearchIncidents",
        "GetCurrentUtcDateTime",
        "GetIcmCorrelationAndLinkingRules",
        "GetAlertingDiscussionEntry",
        "GetDiscussionEntries",
        "GetTopDiscussionEntries",
        "TransferIncident",
        "MitigateIncident",
        "DowngradeSeverity",
        "UpdateIncidentSeverity",
        "ResolveIncident",
        "PostDiscussionEntry",
        "AddTagToIncident",
        "AddKeywordToIncident",
        "AcknowledgeIncident",
        "GetIncidentRepairItems",
        "GetLinkedRelatedIncidentInfo",
        "AddRelatedIncidentLink",
        "RemoveRelatedIncidentLink",
        "GetParentIncidentInfo",
        "AddParentIncidentLink",
        "RemoveParentIncidentLink",
        "GetChildIncidentsInfo",
        "AddIncidentAttachmentFromFile",
        "AddIncidentAttachmentFromContent",
        "GetIssueInvestigationTimeRange",
        "ListIncidentAttachments"
    ];

    /// <summary>
    /// Gets the set of all platform tool names (case-insensitive).
    /// </summary>
    public static HashSet<string> GetPlatformTools()
    {
        return _platformTools.Value;
    }

    /// <summary>
    /// Checks if a tool name is a platform tool.
    /// </summary>
    public static bool IsPlatformTool(string toolName)
    {
        return _platformTools.Value.Contains(toolName);
    }

    private static HashSet<string> LoadPlatformTools()
    {
        var tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add ICM tools
        foreach (var tool in IcmTools)
        {
            tools.Add(tool);
        }

        // Load tools from PublishedTools.json
        var publishedTools = LoadPublishedToolsFromJson();
        foreach (var tool in publishedTools)
        {
            tools.Add(tool);
        }

        return tools;
    }

    private static List<string> LoadPublishedToolsFromJson()
    {
        var toolNames = new List<string>();

        try
        {
            // Try multiple paths to find PublishedTools.json
            var paths = GetPublishedToolsPaths();

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<PublishedToolsConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (config?.Tools != null)
                    {
                        foreach (var tool in config.Tools)
                        {
                            if (!string.IsNullOrWhiteSpace(tool.Name))
                            {
                                toolNames.Add(tool.Name);
                            }
                        }
                    }

                    return toolNames;
                }
            }
        }
        catch
        {
            // If we can't load from JSON, return empty list
            // The caller should handle this gracefully
        }

        return toolNames;
    }

    private static IEnumerable<string> GetPublishedToolsPaths()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Try various paths relative to current directory
        yield return Path.Combine(currentDir, "PublishedTools.json");
        yield return Path.Combine(currentDir, "src", "Agent", "Agent.Web", "PublishedTools.json");

        // Try paths relative to assembly location
        var assemblyDir = Path.GetDirectoryName(typeof(PlatformToolsProvider).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            yield return Path.Combine(assemblyDir, "PublishedTools.json");

            // Walk up to find src directory
            var dir = new DirectoryInfo(assemblyDir);
            while (dir != null)
            {
                var srcPath = Path.Combine(dir.FullName, "src", "Agent", "Agent.Web", "PublishedTools.json");
                if (File.Exists(srcPath))
                {
                    yield return srcPath;
                }
                dir = dir.Parent;
            }
        }
    }

    /// <summary>
    /// Model for deserializing PublishedTools.json
    /// </summary>
    private class PublishedToolsConfig
    {
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<PublishedTool>? Tools { get; set; }
    }

    private class PublishedTool
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
    }
}
