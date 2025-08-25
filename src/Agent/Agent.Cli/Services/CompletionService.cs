using System.CommandLine.Completions;

namespace Agent.Cli.Services;

/// <summary>
/// Provides shell completion functionality for CLI commands.
/// </summary>
public static class CompletionService
{
    /// <summary>
    /// Gets available tool names for completion.
    /// </summary>
    public static IEnumerable<CompletionItem> GetToolNames(CompletionContext context)
    {
        var toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        var toolNames = new List<CompletionItem>();
        
        // Find all YAML files in tools directory and subdirectories
        var yamlFiles = Directory.GetFiles(toolsDir, "*.yaml", SearchOption.AllDirectories);
        
        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var relativePath = Path.GetRelativePath(toolsDir, file);
            var label = $"{fileName} ({relativePath})";
            
            toolNames.Add(new CompletionItem(fileName, label));
        }

        return toolNames.Distinct();
    }

    /// <summary>
    /// Gets available agent names for completion.
    /// </summary>
    public static IEnumerable<CompletionItem> GetAgentNames(CompletionContext context)
    {
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            return Enumerable.Empty<CompletionItem>();
        }

        var agentNames = new List<CompletionItem>();
        
        // Find all YAML files in agents directory and subdirectories
        var yamlFiles = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories);
        
        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var relativePath = Path.GetRelativePath(agentsDir, file);
            var label = $"{fileName} ({relativePath})";
            
            agentNames.Add(new CompletionItem(fileName, label));
        }

        return agentNames.Distinct();
    }

    /// <summary>
    /// Gets available tool types for completion.
    /// </summary>
    public static IEnumerable<CompletionItem> GetToolTypes(CompletionContext context)
    {
        try
        {
            var toolTypes = ToolDefinitionService.GetAvailableToolTypes();
            return toolTypes.Select(t => new CompletionItem(t.Name, $"{t.Name} - {t.Description}"));
        }
        catch
        {
            return Enumerable.Empty<CompletionItem>();
        }
    }

    /// <summary>
    /// Gets available profiles for completion.
    /// </summary>
    public static IEnumerable<CompletionItem> GetProfileNames(CompletionContext context)
    {
        try
        {
            var configService = new CliConfigurationService();
            var profiles = configService.GetAvailableProfiles();
            return profiles.Select(p => new CompletionItem(p));
        }
        catch
        {
            return Enumerable.Empty<CompletionItem>();
        }
    }
}