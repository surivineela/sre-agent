using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Text;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Cli.Validations;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles tool-related command operations.
/// </summary>
public static class ToolCommandHandlers
{
    /// <summary>
    /// Handles the tool create command.
    /// </summary>
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ToolCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting tool create command");

        try
        {
            var name = parseResult.GetValue(ToolCommandOptions.NameOption);
            var type = parseResult.GetValue(ToolCommandOptions.TypeOption);
            var customPath = parseResult.GetValue(ToolCommandOptions.PathOption);
            var extra = parseResult.GetValue(ToolCommandOptions.ExtraOption);

            DebugLogger.Debug("Parameters", $"Name: {name}, Type: {type}, Path: {customPath ?? "default"}, Extra: {extra?.Length ?? 0} items");

            // Validate tool type is supported
            var availableTypes = ToolDefinitionService.GetAvailableToolTypes();
            var toolTypeInfo = availableTypes.FirstOrDefault(t =>
                t.Name.Equals(type, StringComparison.OrdinalIgnoreCase));

            if (toolTypeInfo == null)
            {
                DebugLogger.Debug("Validation", $"Unknown tool type: {type}");
                Console.WriteLine($"[ERROR] Unknown tool type '{type}'");
                Console.WriteLine("Available tool types:");
                foreach (var availableType in availableTypes)
                {
                    Console.WriteLine($"  - {availableType.Name}: {availableType.Description}");
                }
                Console.WriteLine("\nUse 'srectl tool show-types' for more details.");
                Environment.Exit(1);
                return;
            }

            // Tool validation
            if (!ToolValidation.ValidateTool(name!, type!, out var errors))
            {
                DebugLogger.LogValidation($"Tool {name}", false, errors);
                Console.WriteLine("❌ Tool validation failed:");
                foreach (var error in errors)
                    Console.WriteLine($"  - {error}");
                Environment.Exit(1);
                return;
            }

            DebugLogger.LogValidation($"Tool {name}", true);

            // Create tool using template based on actual definitions
            var toolYaml = CreateToolFromTemplate(name!, type!, extra);

            // Write tool to file
            var toolsDir = "tools";
            string yamlPath;

            if (!string.IsNullOrWhiteSpace(customPath))
            {
                // Use custom path: tools/{customPath}/{name}.yaml
                var toolDir = Path.Combine(toolsDir, customPath);
                Directory.CreateDirectory(toolDir);
                yamlPath = Path.Combine(toolDir, $"{name}.yaml");
            }
            else
            {
                // Use legacy structure: tools/{name}/{name}.yaml
                var toolDir = Path.Combine(toolsDir, name!);
                Directory.CreateDirectory(toolDir);
                yamlPath = Path.Combine(toolDir, $"{name}.yaml");
            }

            DebugLogger.LogFile("WRITE", yamlPath, $"Tool YAML content size: {toolYaml.Length} characters");

            await File.WriteAllTextAsync(yamlPath, toolYaml);
            Console.WriteLine($"[SUCCESS] Tool YAML created at {yamlPath}");
            Console.WriteLine($"Tool type: {toolTypeInfo.Name} - {toolTypeInfo.Description}");
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Review and customize the generated YAML file");
            Console.WriteLine("2. Update the connector reference");
            Console.WriteLine("3. Validate using: srectl tool validate --name " + name);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"CreateTool failed: {ex.Message}");
            Console.WriteLine($"[ERROR] {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool validate command.
    /// </summary>
    public static void HandleValidateCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ToolCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting tool validate command");

        var validateAll = parseResult.GetValue(ToolCommandOptions.AllOption);
        var name = parseResult.GetValue(ToolCommandOptions.NameOptionValidate);

        DebugLogger.Debug("Parameters", $"ValidateAll: {validateAll}, Name: {name ?? "none"}");

        var deserializer = YamlHelper.CreateCamelCaseDeserializer();

        if (validateAll)
        {
            ValidateAllTools(deserializer);
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            ValidateSingleTool(name, deserializer);
        }
        else
        {
            Console.WriteLine("❌ Please provide either --name or --all for tool validation.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool apply command.
    /// </summary>
    public static async Task HandleApplyCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ToolCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting tool apply command");

        var name = parseResult.GetValue(ToolCommandOptions.ApplyNameOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        if (dryRun)
        {
            await HandleApplyDryRun(name!);
            return;
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyToolAsync(name!);

        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the tool show-types command to display available tool types.
    /// </summary>
    public static void HandleShowTypesCommand(ParseResult parseResult)
    {
        try
        {
            var verbose = parseResult.GetValue(ToolCommandOptions.VerboseOption);
            var toolType = parseResult.GetValue(ToolCommandOptions.TypeFilterOption);

            if (!string.IsNullOrEmpty(toolType))
            {
                // Show details for a specific tool type
                ShowSpecificToolTypeDetails(toolType);
            }
            else
            {
                // Show all available tool types
                ShowAllToolTypes(verbose);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool show-connectors command to display available connector types.
    /// </summary>
    public static void HandleShowConnectorsCommand(ParseResult parseResult)
    {
        try
        {
            var verbose = parseResult.GetValue(ToolCommandOptions.VerboseOption);

            Console.WriteLine("=====================================");
            Console.WriteLine("Available Connector Types");
            Console.WriteLine("=====================================");

            var connectorTypes = ToolDefinitionService.GetAvailableConnectorTypes();

            if (!connectorTypes.Any())
            {
                Console.WriteLine("[INFO] No connector types found.");
                return;
            }

            foreach (var connector in connectorTypes)
            {
                Console.WriteLine($"\n[{connector.Name}]");
                Console.WriteLine($"  Description: {connector.Description}");

                if (verbose)
                {
                    Console.WriteLine($"  Type: {connector.TypeName}");
                    Console.WriteLine($"  Assembly: {connector.Assembly}");
                    Console.WriteLine($"  Namespace: {connector.Namespace}");
                }
            }

            Console.WriteLine($"\n[SUCCESS] Found {connectorTypes.Count} connector type(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static Dictionary<string, object> CreateKustoToolTemplate(string name)
    {
        return new Dictionary<string, object>
        {
            ["name"] = name,
            ["type"] = "KustoTool",
            ["connector"] = "default-kusto-connector",
            ["description"] = $"A Kusto query tool for {name}. Please update this description with specific details about what this tool does.",
            ["mode"] = "query",
            ["function"] = name,
            ["query"] = "// Please provide your KQL query here\n// Example:\n// MyTable\n// | where TimeGenerated > ago(1h)\n// | take 10",
            ["file"] = $"Queries/{name}.kql",
            ["database"] = "DefaultDB",
            ["clusterHint"] = "default-cluster",
            ["parameters"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "timeRange",
                    ["type"] = "string",
                    ["required"] = true,
                    ["description"] = "Time range for the query (e.g., '1h', '24h')",
                    ["mapTo"] = "args",
                    ["target"] = "dictionary:args:string"
                }
            },
            ["attributes"] = new List<string>(),
            ["metadata"] = new Dictionary<string, object>
            {
                ["owner"] = "team-name",
                ["version"] = "1.0",
                ["tags"] = new List<string> { "query", "kusto" },
                ["lastUpdated"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }
        };
    }

    private static string CreateToolFromTemplate(string name, string type, string[]? extra)
    {
        // Get the tool type details to generate template
        var details = ToolDefinitionService.GetToolTypeDetails(type);
        if (details == null)
        {
            throw new InvalidOperationException($"Unable to get details for tool type '{type}'");
        }

        // Start with the sample YAML as base template
        var yamlContent = details.SampleYaml.Replace("MyKustoTool", name)
                                           .Replace("MyKustoQuery", name)
                                           .Replace($"My{type}", name);

        // Apply any extra parameters provided
        if (extra != null && extra.Length > 0)
        {
            var keyValuePairs = ArgumentParser.ParseKeyValuePairs(extra);

            // Simple replacement for basic properties
            foreach (var kv in keyValuePairs)
            {
                // Replace property values in YAML (basic approach)
                var propertyPattern = $"{kv.Key}:.*";
                var replacement = $"{kv.Key}: {kv.Value}";

                if (yamlContent.Contains($"{kv.Key}:"))
                {
                    yamlContent = System.Text.RegularExpressions.Regex.Replace(
                        yamlContent,
                        $"^{kv.Key}:.*$",
                        replacement,
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                }
                else
                {
                    // Add new property
                    yamlContent += $"\n{kv.Key}: {kv.Value}";
                }
            }
        }

        return yamlContent;
    }

    private static void ValidateAllTools(YamlDotNet.Serialization.IDeserializer deserializer)
    {
        var toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            Console.WriteLine("No tools directory found.");
            Environment.Exit(1);
        }

        var files = Directory.GetFiles(toolsDir, "*.yaml", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            Console.WriteLine("No tool YAML files found in tools directory.");
            Environment.Exit(1);
        }

        bool allValid = true;
        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file, Encoding.UTF8);
                var doc = deserializer.Deserialize<Dictionary<string, object>>(yaml);

                string toolName = doc.TryGetValue("name", out var n) ? n?.ToString() ?? string.Empty : string.Empty;
                string toolType = doc.TryGetValue("type", out var t) ? t?.ToString() ?? string.Empty : string.Empty;

                if (ToolValidation.ValidateTool(toolName, toolType, out var errors))
                {
                    Console.WriteLine($"✅ {file}: Validation succeeded.");
                }
                else
                {
                    allValid = false;
                    Console.WriteLine($"❌ {file}: Validation failed:");
                    foreach (var error in errors)
                        Console.WriteLine($"   - {error}");
                }
            }
            catch (Exception ex)
            {
                allValid = false;
                Console.WriteLine($"❌ {file}: Exception during validation: {ex.Message}");
            }
        }
        if (allValid)
            Console.WriteLine("All tool YAML files are valid.");
        else
        {
            Console.WriteLine("Some tool YAML files failed validation.");
            Environment.Exit(1);
        }
    }

    private static void ValidateSingleTool(string name, YamlDotNet.Serialization.IDeserializer deserializer)
    {
        var filePath = FindToolFile(name);
        if (filePath == null)
        {
            Console.WriteLine($"❌ Tool YAML file not found for tool '{name}'");
            Console.WriteLine($"   Searched in tools directory and subdirectories for '{name}.yaml'");
            Environment.Exit(1);
        }

        try
        {
            var yaml = File.ReadAllText(filePath, Encoding.UTF8);
            var doc = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            string toolName = doc.TryGetValue("name", out var n) ? n?.ToString() ?? string.Empty : string.Empty;
            string toolType = doc.TryGetValue("type", out var t) ? t?.ToString() ?? string.Empty : string.Empty;

            if (ToolValidation.ValidateTool(toolName, toolType, out var errors))
            {
                Console.WriteLine($"✅ Tool validation succeeded for '{name}' at {filePath}");
            }
            else
            {
                Console.WriteLine($"❌ Tool validation failed for '{name}' at {filePath}:");
                foreach (var error in errors)
                    Console.WriteLine($"  - {error}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception during validation: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void ShowAllToolTypes(bool verbose)
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("Available Tool Types");
        Console.WriteLine("=====================================");

        var toolTypes = ToolDefinitionService.GetAvailableToolTypes();

        if (!toolTypes.Any())
        {
            Console.WriteLine("[INFO] No tool types found.");
            return;
        }

        foreach (var toolType in toolTypes)
        {
            Console.WriteLine($"\n[{toolType.Name}]");
            Console.WriteLine($"  Description: {toolType.Description}");

            if (verbose)
            {
                Console.WriteLine($"  Type: {toolType.TypeName}");
                Console.WriteLine($"  Assembly: {toolType.Assembly}");
                Console.WriteLine($"  Namespace: {toolType.Namespace}");
            }
        }

        Console.WriteLine($"\n[SUCCESS] Found {toolTypes.Count} tool type(s)");
        Console.WriteLine("\nUsage: srectl tool show-types --type <ToolTypeName> for detailed information");
    }

    private static void ShowSpecificToolTypeDetails(string toolTypeName)
    {
        var details = ToolDefinitionService.GetToolTypeDetails(toolTypeName);

        if (details == null)
        {
            Console.WriteLine($"[ERROR] Tool type '{toolTypeName}' not found.");
            Console.WriteLine("Use 'srectl tool show-types' to see available tool types.");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("=====================================");
        Console.WriteLine($"Tool Type Details: {details.Name}");
        Console.WriteLine("=====================================");

        Console.WriteLine($"Description: {details.Description}");
        Console.WriteLine($"Type: {details.TypeName}");
        Console.WriteLine($"Assembly: {details.Assembly}");
        Console.WriteLine($"Namespace: {details.Namespace}");

        if (details.SupportedProperties.Any())
        {
            Console.WriteLine("\nSupported Properties:");
            foreach (var prop in details.SupportedProperties)
            {
                Console.WriteLine($"  - {prop}");
            }
        }

        Console.WriteLine("\nSample YAML:");
        Console.WriteLine("-------------------------------------");
        Console.WriteLine(details.SampleYaml);
        Console.WriteLine("-------------------------------------");

        Console.WriteLine($"\n[SUCCESS] Tool type details displayed for '{toolTypeName}'");
    }

    /// <summary>
    /// Handles the tool delete command.
    /// </summary>
    public static async Task HandleDeleteCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ToolCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting tool delete command");

        var toolName = parseResult.GetValue(ToolCommandOptions.DeleteNameOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.DeleteDryRunOption);

        DebugLogger.Debug("Parameters", $"ToolName: {toolName}, DryRun: {dryRun}");

        if (string.IsNullOrWhiteSpace(toolName))
        {
            Console.WriteLine("❌ Tool name is required.");
            Environment.Exit(1);
            return;
        }

        if (dryRun)
        {
            await HandleDeleteDryRun(toolName);
            return;
        }

        try
        {
            using var apiService = new ApiService();
            Console.WriteLine($"🗑️  Deleting tool '{toolName}'...");

            var (success, response) = await apiService.DeleteToolAsync(toolName);

            if (success)
            {
                Console.WriteLine($"✅ {response}");
            }
            else
            {
                Console.WriteLine($"❌ {response}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteTool failed: {ex.Message}");
            Console.WriteLine($"❌ Failed to delete tool: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Finds a tool YAML file by searching recursively under the tools directory.
    /// Supports flexible folder organization.
    /// </summary>
    /// <param name="toolName">The name of the tool to find</param>
    /// <returns>The full path to the tool YAML file, or null if not found</returns>
    public static string? FindToolFile(string toolName)
    {
        var toolsDir = "tools";
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
    /// Handles dry-run for tool apply command.
    /// </summary>
    private static async Task HandleApplyDryRun(string toolName)
    {
        try
        {
            Console.WriteLine($"🔍 DRY RUN: Tool apply for '{toolName}'");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Find and validate tool file exists
            var toolFilePath = FindToolFile(toolName);
            if (toolFilePath == null)
            {
                Console.WriteLine($"❌ Tool file not found for '{toolName}'");
                Console.WriteLine($"   Searched in tools directory and subdirectories for '{toolName}.yaml'");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"📂 Tool file found: {toolFilePath}");

            // Read and parse the YAML file
            var yamlContent = await File.ReadAllTextAsync(toolFilePath);
            Console.WriteLine($"📄 YAML content size: {yamlContent.Length} characters");

            // Validate YAML structure
            var deserializer = YamlHelper.CreateCamelCaseDeserializer();
            try
            {
                var toolConfig = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                Console.WriteLine("✅ YAML structure is valid");
                Console.WriteLine($"📝 Tool details:");

                if (toolConfig.TryGetValue("name", out var nameValue))
                    Console.WriteLine($"   Name: {nameValue}");
                if (toolConfig.TryGetValue("type", out var typeValue))
                    Console.WriteLine($"   Type: {typeValue}");
                if (toolConfig.TryGetValue("description", out var descValue))
                    Console.WriteLine($"   Description: {descValue}");
                if (toolConfig.TryGetValue("connector", out var connectorValue))
                    Console.WriteLine($"   Connector: {connectorValue}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ YAML parsing failed: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Check server connectivity
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("❌ Configuration not found. Run 'srectl init' first.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🌐 Target server: {config.ResourceUrl}");
            Console.WriteLine($"🔐 Authentication required: {config.AuthRequired}");

            Console.WriteLine("\n✅ DRY RUN COMPLETE");
            Console.WriteLine("📋 Summary:");
            Console.WriteLine($"   • Tool '{toolName}' configuration is valid");
            Console.WriteLine("   • YAML file can be parsed successfully");
            Console.WriteLine("   • Server configuration is available");
            Console.WriteLine($"   • Would apply to: {config.ResourceUrl}");
            Console.WriteLine("\n💡 To actually apply the tool, run: srectl tool apply --name " + toolName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DRY RUN FAILED: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles dry-run for tool delete command.
    /// </summary>
    private static async Task HandleDeleteDryRun(string toolName)
    {
        try
        {
            Console.WriteLine($"🔍 DRY RUN: Tool delete for '{toolName}'");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Check if tool exists locally
            var toolFilePath = FindToolFile(toolName);
            if (toolFilePath != null)
            {
                Console.WriteLine($"📂 Local tool file found: {toolFilePath}");
            }
            else
            {
                Console.WriteLine($"⚠️  No local tool file found for '{toolName}'");
            }

            // Check server connectivity
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("❌ Configuration not found. Run 'srectl init' first.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🌐 Target server: {config.ResourceUrl}");
            Console.WriteLine($"🔐 Authentication required: {config.AuthRequired}");

            // Check for dependencies by searching for tool references in agent files
            var dependencies = FindToolDependencies(toolName);
            if (dependencies.Any())
            {
                Console.WriteLine($"⚠️  Found {dependencies.Count} potential dependencies:");
                foreach (var dep in dependencies)
                {
                    Console.WriteLine($"   • {dep}");
                }
                Console.WriteLine("   Delete might fail if these agents are deployed and reference this tool");
            }
            else
            {
                Console.WriteLine("✅ No local dependencies found");
            }

            Console.WriteLine("\n✅ DRY RUN COMPLETE");
            Console.WriteLine("📋 Summary:");
            Console.WriteLine($"   • Tool '{toolName}' would be deleted from server");
            Console.WriteLine($"   • Target server: {config.ResourceUrl}");
            if (dependencies.Any())
            {
                Console.WriteLine($"   • ⚠️  {dependencies.Count} potential dependencies found");
            }
            Console.WriteLine("\n💡 To actually delete the tool, run: srectl tool delete --name " + toolName);
            if (dependencies.Any())
            {
                Console.WriteLine("⚠️  Consider updating dependent agents first to avoid deployment issues");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DRY RUN FAILED: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Finds agents that depend on the specified tool.
    /// </summary>
    private static List<string> FindToolDependencies(string toolName)
    {
        var dependencies = new List<string>();
        var agentsDir = "agents";

        if (!Directory.Exists(agentsDir))
            return dependencies;

        var yamlFiles = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories);

        foreach (var file in yamlFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains(toolName))
                {
                    var agentName = Path.GetFileNameWithoutExtension(file);
                    var relativePath = Path.GetRelativePath(agentsDir, file);
                    dependencies.Add($"{agentName} ({relativePath})");
                }
            }
            catch
            {
                // Ignore files that can't be read
            }
        }

        return dependencies;
    }
}
