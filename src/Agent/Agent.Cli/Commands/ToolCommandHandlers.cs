// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles tool-related command operations.
/// </summary>
public static class ToolCommandHandlers
{
    private const string ToolsDirectory = "tools";

    /// <summary>
    /// Handles the tool create command.
    /// </summary>
    public static async Task<int> HandleCreateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool create command");

        var name = parseResult.GetValue(ToolCommandOptions.Create.NameOption);
        var type = parseResult.GetValue(ToolCommandOptions.Create.TypeOption);
        var customPath = parseResult.GetValue(ToolCommandOptions.Create.PathOption);
        var connector = parseResult.GetValue(ToolCommandOptions.Create.ConnectorOption);
        var database = parseResult.GetValue(ToolCommandOptions.Create.DatabaseOption);
        var description = parseResult.GetValue(ToolCommandOptions.Create.DescriptionOption);
        var query = parseResult.GetValue(ToolCommandOptions.Create.QueryOption);
        var template = parseResult.GetValue(ToolCommandOptions.Create.TemplateOption);
        var parameters = parseResult.GetValue(ToolCommandOptions.Create.ParameterOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, Type: {type}, Path: {customPath ?? "default"}");

        // Get tool type info for display purposes
        var availableTypes = ExtendedToolHelper.GetAvailableToolTypes();
        var toolTypeInfo = availableTypes.FirstOrDefault(t =>
            t.Name.Equals(type, StringComparison.OrdinalIgnoreCase));

        DebugLogger.LogValidation($"Tool {name}", true);

        // Always use V2 structured creation with ExtendedToolV2
        string toolYaml = CreateToolV2(name!, type!, connector, database, description, query, template, parameters);

        // For KustoTool, prepend a helpful header with modification + permissions guidance
        if (ToolName.KustoTool == type)
        {
            var kustoToolHeader = new[]
            {
                "# NOTE: This is a Kusto tool template. Update it before applying.",
                "# - Set a meaningful description, database, and query.",
                "# - Ensure the 'connector' points to a configured Kusto connector.",
                "# - Verify the connector principal has required ADX permissions (see http://aka.ms/1psreagent).",
                ""
            };
            var header = string.Join('\n', kustoToolHeader);
            toolYaml = header + toolYaml;
        }

        // Write tool to file
        string yamlPath;

        if (customPath != null && customPath.Length > 0)
        {
            // Use custom path: tools/{customPath}/{name}.yaml
            var toolDir = Path.Combine(ToolsDirectory, customPath);
            Directory.CreateDirectory(toolDir);
            yamlPath = Path.Combine(toolDir, $"{name}.yaml");
        }
        else if (customPath == string.Empty)
        {
            // Use flat structure: tools/{name}.yaml
            Directory.CreateDirectory(ToolsDirectory);
            yamlPath = Path.Combine(ToolsDirectory, $"{name}.yaml");
        }
        else
        {
            // Use legacy structure: tools/{name}/{name}.yaml
            var toolDir = Path.Combine(ToolsDirectory, name!);
            Directory.CreateDirectory(toolDir);
            yamlPath = Path.Combine(toolDir, $"{name}.yaml");
        }

        DebugLogger.LogFile("WRITE", yamlPath, $"Tool YAML content size: {toolYaml.Length} characters");

        await File.WriteAllTextAsync(yamlPath, toolYaml);
        ConsoleUI.WriteStatus(true, $"Tool YAML created at {yamlPath}");
        if (toolTypeInfo != null)
        {
            ConsoleUI.WriteKeyValue("Tool type", $"{toolTypeInfo.Name} - {toolTypeInfo.Description}");
        }
        Console.WriteLine();
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteCommand("Review and customize", "Edit the generated YAML file");
        ConsoleUI.WriteCommand("Update connector", "Set the correct connector reference");
        ConsoleUI.WriteCommand("Validate tool", $"srectl tool validate --name {name}");
        ConsoleUI.WriteCommand("Apply tool", $"srectl tool apply --name {name}");

        // Kusto-specific post-create reminders
        if (!string.IsNullOrWhiteSpace(type) && ToolName.KustoTool == type)
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Kusto prerequisites");
            ConsoleUI.WriteBullet("Edit YAML to set database, cluster, data connection and query.", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet("Ensure the connector principal has the required ADX permissions.", ConsoleColor.Yellow);
            ConsoleUI.WriteCommand("Docs", "http://aka.ms/1psreagent");
        }

        return 0;
    }

    /// <summary>
    /// Handles the tool validate command.
    /// </summary>
    public static async Task<int> HandleValidateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool validate command");

        var validateAll = parseResult.GetValue(ToolCommandOptions.Validate.AllOption);
        var name = parseResult.GetValue(ToolCommandOptions.Validate.NameOption);

        DebugLogger.Debug("Parameters", $"ValidateAll: {validateAll}, Name: {name ?? "none"}");

        bool success;
        if (validateAll)
        {
            success = await ValidateAllToolsAsync();
        }
        else
        {
            success = await ValidateSingleToolAsync(name!);
        }

        return success ? 0 : 1;
    }

    /// <summary>
    /// Handles the tool apply command.
    /// </summary>
    public static async Task<int> HandleApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool apply command");

        var name = parseResult.GetValue(ToolCommandOptions.Apply.NameOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.Apply.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        // Find tool YAML file using flexible search
        var toolFilePath = ExtendedToolHelper.FindToolFile(name!);
        if (toolFilePath == null)
        {
            ConsoleUI.WriteStatus(false, $"Tool file not found for '{name}'. Searched in tools directory and subdirectories for '{name}.yaml'");
            return 1;
        }

        // Check tool version before attempting to apply
        var detectedVersion = ExtendedToolHelper.DetectVersion(toolFilePath);
        if (detectedVersion != YamlApiVersion.V2)
        {
            var versionName = detectedVersion == YamlApiVersion.V1 ? "V1" : "unknown";
            ConsoleUI.WriteStatus(false, $"Tool '{name}' is using {versionName} format and must be migrated to V2 before applying.");
            ConsoleUI.WriteBullet($"Run: srectl tool migrate --name {name}", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet("Or:  srectl tool migrate --all", ConsoleColor.Yellow);
            return 1;
        }

        // Read and parse the YAML file as ExtendedToolV2
        var tool = await ExtendedToolV2.LoadYamlAsync(toolFilePath);
        if (tool == null)
        {
            ConsoleUI.WriteStatus(false, $"Failed to parse tool YAML file: {toolFilePath}");
            return 1;
        }

        // Check if --name parameter differs from the name in YAML
        var yamlName = tool.Metadata?.Name;
        if (!string.IsNullOrEmpty(yamlName) && !string.Equals(name, yamlName, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUI.WriteBullet($"Warning: --name parameter '{name}' differs from YAML metadata.name '{yamlName}'", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet($"Using name from YAML: '{yamlName}'", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyExtendedToolAsync(tool, dryRun);

        Console.WriteLine(response);
        return success ? 0 : 1;
    }

    /// <summary>
    /// Handles the tool show-types command to display available tool types.
    /// </summary>
    public static int HandleShowTypesCommand(ParseResult parseResult)
    {
        var toolType = parseResult.GetValue(ToolCommandOptions.ShowTypes.TypeOption);

        if (!string.IsNullOrEmpty(toolType))
        {
            // Show details for a specific tool type
            return ShowSpecificToolTypeDetails(toolType);
        }
        else
        {
            // Show all available tool types
            ShowAllToolTypes();
            return 0;
        }
    }

    /// <summary>
    /// Handles the tool show-connectors command to display available connector types.
    /// </summary>
    public static async Task<int> HandleShowConnectorsCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        // First, try to show configured data connectors from the server (these are the actual names to use in YAML)
        ConsoleUI.WriteSection("Configured Data Connectors (use these names in YAML)");
        bool printedConfigured = false;
        using (var api = new ApiService())
        {
            try
            {
                var (success, response) = await api.ListDataConnectorsAsync();
                if (success)
                {
                    Console.WriteLine(response);
                    printedConfigured = true;
                    Console.WriteLine();
                    ConsoleUI.WriteInfo("YAML: connector: <name>", ConsoleColor.Gray);
                }
                else
                {
                    ConsoleUI.WriteInfo("Could not fetch connectors from server.", ConsoleColor.Yellow);
                    ConsoleUI.WriteInfo("Tip: run 'srectl init' and 'srectl list data-connectors' when connected.", ConsoleColor.Gray);
                }
            }
            catch
            {
                ConsoleUI.WriteInfo("Unable to connect to server to list configured connectors.", ConsoleColor.Yellow);
            }
        }

        // Then, show available connector types from local SDK discovery (helpful reference)
        if (printedConfigured)
        {
            Console.WriteLine();
        }
        ConsoleUI.WriteSection("Available Connector Types");

        var connectorTypes = ToolDefinitionService.GetAvailableConnectorTypes();

        if (connectorTypes.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No connector types found.");
            return 1;
        }

        foreach (var connector in connectorTypes)
        {
            ConsoleUI.WriteKeyValue(connector.Name, connector.Description, 20, ConsoleColor.Yellow, ConsoleColor.Gray);
            Console.WriteLine();
        }

        ConsoleUI.WriteKeyValue("Total", $"{connectorTypes.Count} connector type(s)", 10);
        return 0;
    }

    /// <summary>
    /// Handles the tool list command to display available tools.
    /// </summary>
    public static async Task<int> HandleListCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool list command");

        var search = parseResult.GetValue(ToolCommandOptions.List.SearchOption);
        var name = parseResult.GetValue(ToolCommandOptions.List.NameOption);
        var detail = parseResult.GetValue(ToolCommandOptions.List.DetailOption);

        DebugLogger.Debug("Parameters", $"Search: {search}, Name: {name}, Detail: {detail}");

        using var apiService = new ApiService();

        var (toolsList, error) = await apiService.ListExtendedToolsAsync(search);

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (toolsList.Count == 0)
        {
            ConsoleUI.WriteInfo("No extended tools found on the server.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Use 'srectl tool apply <tool-name>' to add tools to the server.", ConsoleColor.Gray);
            return 1;
        }

        // Filter by name if specified
        if (!string.IsNullOrWhiteSpace(name))
        {
            var tool = toolsList.FirstOrDefault(t =>
                string.Equals(t.Metadata?.Name, name, StringComparison.OrdinalIgnoreCase));

            if (tool == null)
            {
                ConsoleUI.WriteStatus(false, $"Tool '{name}' not found.");
                return 1;
            }

            ConsoleUI.WriteSection("Remote Extended Tool");
            Console.WriteLine(tool.ToYaml());
            return 0;
        }

        ConsoleUI.WriteSection("Remote Extended Tools");

        for (int i = 0; i < toolsList.Count; i++)
        {
            if (detail)
            {
                var yamlOutput = toolsList[i].ToYaml();
                Console.WriteLine(yamlOutput);
                if (i < toolsList.Count - 1)
                {
                    ConsoleUI.DrawLine();
                }
            }
            else
            {
                var toolName = toolsList[i].Metadata?.Name ?? "Unknown";
                ConsoleUI.WriteBullet(toolName);
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{toolsList.Count} extended tool(s)", 0);
        return 0;
    }

    /// <summary>
    /// Handles the tool delete command.
    /// </summary>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool delete command");

        var toolName = parseResult.GetValue(ToolCommandOptions.Delete.NameOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.Delete.DryRunOption);

        DebugLogger.Debug("Parameters", $"ToolName: {toolName}, DryRun: {dryRun}");

        // Null check (validation should have caught this, but be defensive)
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new InvalidOperationException("Tool name validation should have been caught by the validator.");
        }

        using var apiService = new ApiService();

        if (dryRun)
        {
            ConsoleUI.WriteInfo($"Validating tool deletion for '{toolName}' (dry run)...", ConsoleColor.Yellow);
        }
        else
        {
            ConsoleUI.WriteInfo($"Deleting tool '{toolName}'...", ConsoleColor.Yellow);
        }

        var (success, message) = await apiService.DeleteExtendedToolAsync(toolName, dryRun);

        if (success)
        {
            ConsoleUI.WriteStatus(true, message);

            // After successful server deletion (not dry-run), offer to clean up local files
            if (!dryRun)
            {
                OfferLocalToolCleanup(toolName);
            }

            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, message);
            return 1;
        }
    }

    /// <summary>
    /// Handles the tool diff command to compare local and remote configurations.
    /// </summary>
    public static async Task<int> HandleDiffCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool diff command");

        var toolName = parseResult.GetValue(ToolCommandOptions.Diff.NameOption);
        var diffTool = parseResult.GetValue(ToolCommandOptions.Diff.ToolOption) ?? "git";
        var showRaw = parseResult.GetValue(ToolCommandOptions.Diff.RawOption);

        DebugLogger.Debug("Parameters", $"ToolName: {toolName}, Tool: {diffTool}, Raw: {showRaw}");

        if (string.IsNullOrWhiteSpace(toolName))
        {
            ConsoleUI.WriteStatus(false, "Tool name is required.");
            return 1;
        }

        try
        {
            // Find local tool file
            var localPath = ExtendedToolHelper.FindToolFile(toolName);
            if (localPath == null)
            {
                ConsoleUI.WriteStatus(false, $"Local tool file not found for '{toolName}'");
                ConsoleUI.WriteBullet($"Expected: tools/{toolName}.yaml or tools/{toolName}/{toolName}.yaml", ConsoleColor.Yellow);
                return 1;
            }

            ConsoleUI.WriteInfo($"Comparing tool '{toolName}'...", ConsoleColor.Cyan);

            // Get remote configuration
            using var apiService = new ApiService();
            var (success, remoteYaml, errorMessage) = await apiService.GetToolConfigurationAsync(toolName);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to get remote configuration: {errorMessage}");
                return 1;
            }


            // Read local configuration
            var localYaml = await File.ReadAllTextAsync(localPath);

            // If both are identical, no need to diff
            if (string.Equals(localYaml.Trim(), remoteYaml.Trim(), StringComparison.Ordinal))
                // If both are identical, no need to diff
                if (string.Equals(localYaml.Trim(), remoteYaml.Trim(), StringComparison.Ordinal))
                {
                    ConsoleUI.WriteStatus(true, "Local and remote configurations are identical");
                    return 0;
                }

            // Parse the remote YAML content to get tool type first, then deserialize using specific tool type class
            string localContent, remoteContent;
            try
            {
                // Parse YAML to get tool type using underscored convention for local compatibility
                var typeDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlDict = typeDeserializer.Deserialize<Dictionary<string, object>>(remoteYaml);
                var toolType = yamlDict.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : null;

                // Use camelCase deserializer for remote YAML (server returns camelCase)
                var remoteDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                // Use underscored deserializer for local YAML (local files use snake_case)
                var localDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                // Deserialize YAML to the correct derived type to preserve all fields (like query)
                YamlToolDefinitionBase remoteTool = toolType switch
                {
                    var t when ToolName.KustoTool == t => remoteDeserializer.Deserialize<KustoToolDefinition>(remoteYaml),
                    // var t when ToolName.LinkTool == t => remoteDeserializer.Deserialize<LinkToolDefinition>(remoteYaml),
                    _ => throw new NotSupportedException($"Unknown tool type: {toolType}")
                };

                // Also deserialize local YAML using the underscored tool type
                YamlToolDefinitionBase localTool = toolType switch
                {
                    var t when ToolName.KustoTool == t => localDeserializer.Deserialize<KustoToolDefinition>(localYaml),
                    // var t when ToolName.LinkTool == t => localDeserializer.Deserialize<LinkToolDefinition>(localYaml),
                    _ => throw new NotSupportedException($"Unknown tool type: {toolType}")
                };



                // Normalize parameter values: convert empty strings to null for consistent comparison
                if (remoteTool is KustoToolDefinition remoteKusto && localTool is KustoToolDefinition localKusto)
                {
                    if (remoteKusto.Parameters != null)
                    {
                        foreach (var param in remoteKusto.Parameters)
                        {
                            if (string.IsNullOrEmpty(param.Value as string)) param.Value = null;
                        }
                    }

                    if (localKusto.Parameters != null)
                    {
                        foreach (var param in localKusto.Parameters)
                        {
                            if (string.IsNullOrEmpty(param.Value as string)) param.Value = null;
                        }
                    }
                }

                // Serialize back to YAML for normalized comparison using the same serializer settings
                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .Build();

                localContent = serializer.Serialize(localTool);
                remoteContent = serializer.Serialize(remoteTool);


            }
            catch (Exception parseEx)
            {
                ConsoleUI.WriteBullet($"Failed to parse tool YAML with specific type: {parseEx.Message}", ConsoleColor.Yellow);
                ConsoleUI.WriteBullet("Falling back to generic YAML comparison...", ConsoleColor.Yellow);

                // Fallback to generic YAML normalization
                localContent = NormalizeYaml(localYaml);
                remoteContent = NormalizeYaml(remoteYaml);
            }

            if (showRaw)
            {
                // Show inline diff
                ShowInlineDiff(localContent, remoteContent, toolName);
            }
            else
            {
                // Use external diff tool
                // Use external diff tool
                await LaunchDiffTool(localContent, remoteContent, toolName, diffTool, ".yaml");
            }

            return 0;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Diff failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to compare tool: {ex.Message}");
            return 1;
        }
    }
    /// <summary>
    /// Determines if we should use V2 structured creation based on provided parameters.
    /// </summary>
    /// <summary>
    /// Creates a tool using V2 structured approach with ExtendedToolV2.
    /// </summary>
    private static string CreateToolV2(string name, string type, string? connector, string? database,
        string? description, string? query, string? template, string[]? parameters)
    {
        ExtendedToolV2 tool;

        if (ToolName.KustoTool == type)
        {
            tool = ExtendedToolHelper.CreateKustoTool(name, connector, database, description, query, parameters);
        }
        else if (ToolName.LinkTool == type)
        {
            tool = ExtendedToolHelper.CreateLinkTool(name, description, template, parameters);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported tool type '{type}'. Only KustoTool and LinkTool are supported.");
        }

        // Serialize to YAML
        return tool.ToYaml();
    }

    private static async Task<bool> ValidateAllToolsAsync()
    {
        if (!Directory.Exists(ToolsDirectory))
        {
            ConsoleUI.WriteStatus(false, "No tools directory found.");
            return false;
        }

        var files = Directory.GetFiles(ToolsDirectory, "*.yaml", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No tool YAML files found in tools directory.");
            return false;
        }

        bool allValid = true;
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var (success, errorMessage) = await ValidateToolFileAsync(file, fileName, showFullPath: true);

            if (!success)
            {
                allValid = false;
            }
        }

        if (allValid)
        {
            ConsoleUI.WriteStatus(true, "All tool YAML files are valid");
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Some tool YAML files failed validation");
        }

        return allValid;
    }

    private static async Task<bool> ValidateSingleToolAsync(string name)
    {
        var filePath = ExtendedToolHelper.FindToolFile(name);
        if (filePath == null)
        {
            ConsoleUI.WriteStatus(false, $"Tool YAML file not found for tool '{name}'");
            ConsoleUI.WriteBullet($"Searched in tools directory and subdirectories for '{name}.yaml'", ConsoleColor.Yellow);
            return false;
        }

        var (success, _) = await ValidateToolFileAsync(filePath, name, showFullPath: true);
        return success;
    }

    /// <summary>
    /// Validates a single tool file and returns success status with optional error message.
    /// </summary>
    /// <param name="filePath">Path to the tool YAML file</param>
    /// <param name="toolName">Name of the tool for display purposes</param>
    /// <param name="showFullPath">Whether to show full file path in output</param>
    /// <returns>Tuple of (success, errorMessage)</returns>
    private static async Task<(bool success, string? errorMessage)> ValidateToolFileAsync(string filePath, string toolName, bool showFullPath)
    {
        var displayPath = showFullPath ? filePath : toolName;

        try
        {
            // Detect YAML version using ExtendedToolHelper
            var apiVersion = ExtendedToolHelper.DetectVersion(filePath);

            if (apiVersion == null)
            {
                var errorMsg = "Not a valid extended tool YAML";
                DebugLogger.Debug("ToolValidation", $"{errorMsg}: {filePath}");
                ConsoleUI.WriteStatus(false, $"{displayPath}: {errorMsg}");
                ConsoleUI.WriteBullet("File does not appear to be a valid tool configuration", ConsoleColor.Red, showFullPath ? 6 : 0);
                return (false, errorMsg);
            }

            // Check if this is a V1 tool
            if (apiVersion != YamlApiVersion.V2)
            {
                var versionName = apiVersion == YamlApiVersion.V1 ? "V1" : "unknown";
                var errorMsg = $"Tool is using {versionName} format";
                DebugLogger.Debug("ToolValidation", $"Tool '{toolName}' is using {versionName} format from {filePath}");
                ConsoleUI.WriteStatus(false, $"{displayPath}: {errorMsg}");
                ConsoleUI.WriteBullet("V1 tools must be migrated to V2 format", ConsoleColor.Yellow, showFullPath ? 6 : 0);
                ConsoleUI.WriteBullet("Run 'srectl tool migrate --name <toolname>' to migrate (command coming soon)", ConsoleColor.Gray, showFullPath ? 6 : 0);
                return (false, errorMsg);
            }

            // Deserialize as V2 tool
            var tool = await ExtendedToolV2.LoadYamlAsync(filePath);
            var actualToolName = tool?.Metadata?.Name ?? toolName;

            DebugLogger.Debug("ToolValidation", $"Validating tool '{actualToolName}' of type '{tool?.Spec?.Type}' from {filePath}");

            // Validate using ExtendedToolV2.Validate()
            var validationErrors = tool != null ? tool.Validate() : new List<string> { "Tool failed to load" };

            if (validationErrors.Count == 0)
            {
                DebugLogger.LogValidation($"Tool {actualToolName}", true);
                ConsoleUI.WriteStatus(true, showFullPath
                    ? $"{displayPath}: Validation succeeded"
                    : $"Tool validation succeeded for '{toolName}' at {filePath}");

                return (true, null);
            }
            else
            {
                var errorMsg = string.Join("; ", validationErrors);
                DebugLogger.Debug("ToolValidation", $"Validation failed for '{toolName}': {errorMsg}");
                ConsoleUI.WriteStatus(false, showFullPath
                    ? $"{displayPath}: Validation failed"
                    : $"Validation failed for '{toolName}' at {filePath}");
                foreach (var error in validationErrors)
                {
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red, showFullPath ? 6 : 0);
                }
                return (false, errorMsg);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Exception during validation: {ex.Message}";
            DebugLogger.Debug("ToolValidation", $"Exception validating '{toolName}': {ex.Message}");
            ConsoleUI.WriteStatus(false, showFullPath
                ? $"{displayPath}: {errorMsg}"
                : errorMsg);
            return (false, errorMsg);
        }
    }

    private static void ShowAllToolTypes()
    {
        ConsoleUI.WriteSection("Available Tool Types");

        var toolTypes = ExtendedToolHelper.GetAvailableToolTypes();

        if (toolTypes.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No tool types found.");
            return;
        }

        foreach (var toolType in toolTypes)
        {
            ConsoleUI.WriteKeyValue("🔧 " + toolType.Name, toolType.Description, 20, ConsoleColor.Yellow, ConsoleColor.Gray);
            Console.WriteLine();
        }

        ConsoleUI.WriteKeyValue("Total", $"{toolTypes.Count} tool type(s)", 10);
        Console.WriteLine();
        ConsoleUI.WriteInfo("Use 'srectl tool show-types --type <ToolTypeName>' for detailed information");
    }

    private static int ShowSpecificToolTypeDetails(string toolTypeName)
    {
        var toolTypes = ExtendedToolHelper.GetAvailableToolTypes();
        var toolType = toolTypes.FirstOrDefault(t => t.Name.Equals(toolTypeName, StringComparison.OrdinalIgnoreCase));

        if (toolType == null)
        {
            ConsoleUI.WriteStatus(false, $"Tool type '{toolTypeName}' not found.");
            ConsoleUI.WriteInfo("Use 'srectl tool show-types' to see available tool types.");
            return 1;
        }

        ConsoleUI.WriteSection($"🔧 Tool Type Details: {toolType.Name}");
        ConsoleUI.WriteKeyValue("Description", toolType.Description, 12);
        Console.WriteLine();

        // Generate sample YAML using ExtendedToolHelper
        string sampleYaml;
        if (ToolName.KustoTool == toolType.Name)
        {
            var sampleTool = ExtendedToolHelper.CreateKustoTool("MyKustoTool");
            sampleYaml = sampleTool.ToYaml();
        }
        else if (ToolName.LinkTool == toolType.Name)
        {
            var sampleTool = ExtendedToolHelper.CreateLinkTool("MyLinkTool");
            sampleYaml = sampleTool.ToYaml();
        }
        else
        {
            sampleYaml = "# Sample YAML not available for this tool type";
        }

        ConsoleUI.WriteSection("Sample YAML");
        Console.WriteLine(sampleYaml);
        Console.WriteLine();

        ConsoleUI.WriteStatus(true, $"Tool type details displayed for '{toolTypeName}'");
        return 0;
    }

    /// <summary>
    /// Offers to clean up local tool files after successful server deletion.
    /// </summary>
    private static void OfferLocalToolCleanup(string toolName)
    {
        var toolFile = ExtendedToolHelper.FindToolFile(toolName);

        if (toolFile == null)
        {
            return; // No local files to clean up
        }

        var toolDir = Path.GetDirectoryName(toolFile);

        Console.WriteLine();
        ConsoleUI.WriteSection("Local File Cleanup");
        ConsoleUI.WriteInfo("Local configuration files still exist:", ConsoleColor.Yellow);
        ConsoleUI.WriteBullet(toolFile, ConsoleColor.Gray);
        Console.WriteLine();

        if (ConsoleUI.Confirm("Also delete local configuration files?", false))
        {
            try
            {
                // If tool is in its own directory (tools/toolname/toolname.yaml), delete the directory
                // If tool is in flat structure (tools/toolname.yaml), delete just the file
                if (Path.GetFileName(toolDir) == toolName)
                {
                    Directory.Delete(toolDir!, true);
                    ConsoleUI.WriteStatus(true, $"Local tool directory deleted: {toolDir}");
                }
                else
                {
                    File.Delete(toolFile);
                    ConsoleUI.WriteStatus(true, $"Local tool file deleted: {toolFile}");
                }

                Console.WriteLine();
                ConsoleUI.WriteSection("Summary");
                ConsoleUI.WriteBullet($"Tool '{toolName}' deleted from server", ConsoleColor.Green);
                ConsoleUI.WriteBullet("Local configuration files cleaned up", ConsoleColor.Green);

                Console.WriteLine();
                ConsoleUI.WriteInfo($"To recreate: srectl tool create --name {toolName} --type <ToolType>", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Failed to delete local files: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteBullet($"Tool '{toolName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files preserved: {toolFile}", ConsoleColor.Yellow);

            Console.WriteLine();
            ConsoleUI.WriteInfo($"To redeploy: srectl tool apply --name {toolName}", ConsoleColor.Cyan);
            if (Path.GetFileName(toolDir) == toolName)
            {
                ConsoleUI.WriteInfo($"To delete locally: rm -rf {toolDir!.Replace('\\', '/')}", ConsoleColor.Gray);
            }
            else
            {
                ConsoleUI.WriteInfo($"To delete locally: rm {toolFile.Replace('\\', '/')}", ConsoleColor.Gray);
            }
        }
    }

    #region Diff Helper Methods

    private static string NormalizeYaml(string yaml)
    {
        try
        {
            var deserializer = YamlHelper.CreateCamelCaseDeserializer();
            var obj = deserializer.Deserialize<object>(yaml);

            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
                .Build();

            return serializer.Serialize(obj);
        }
        catch
        {
            return yaml; // Return original if normalization fails
        }
    }

    private static void ShowInlineDiff(string local, string remote, string toolName)
    {
        ConsoleUI.WriteSection($"Configuration Diff for '{toolName}'");

        var localLines = local.Split('\n');
        var remoteLines = remote.Split('\n');

        Console.WriteLine();
        Console.WriteLine("Legend: ");
        ConsoleUI.WriteBullet("Local only (will be removed)", ConsoleColor.Red);
        ConsoleUI.WriteBullet("Remote only (will be added)", ConsoleColor.Green);
        ConsoleUI.WriteBullet("Different values", ConsoleColor.Yellow);
        Console.WriteLine();

        // Simple line-by-line comparison
        int maxLines = Math.Max(localLines.Length, remoteLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            var localLine = i < localLines.Length ? localLines[i] : null;
            var remoteLine = i < remoteLines.Length ? remoteLines[i] : null;

            if (localLine == remoteLine)
            {
                // Lines are the same, skip or show context
                continue;
            }
            else if (localLine != null && remoteLine == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"- {localLine}");
                Console.ResetColor();
            }
            else if (localLine == null && remoteLine != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"+ {remoteLine}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"< {localLine}");
                Console.WriteLine($"> {remoteLine}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Summary");
        ConsoleUI.WriteKeyValue("Local lines", localLines.Length.ToString());
        ConsoleUI.WriteKeyValue("Remote lines", remoteLines.Length.ToString());
    }

    private static async Task LaunchDiffTool(string localContent, string remoteContent, string toolName, string tool, string extension)
    {
        // Create temp files
        var tempDir = Path.GetTempPath();
        var localTempFile = Path.Combine(tempDir, $"{toolName}.local{extension}");
        var remoteTempFile = Path.Combine(tempDir, $"{toolName}.remote{extension}");

        try
        {
            await File.WriteAllTextAsync(localTempFile, localContent);
            await File.WriteAllTextAsync(remoteTempFile, remoteContent);

            ConsoleUI.WriteInfo($"Launching {tool} diff tool...", ConsoleColor.Cyan);

            var process = tool.ToLower() switch
            {
                "git" => LaunchGitDiff(localTempFile, remoteTempFile, toolName),
                "vimdiff" => LaunchVimDiff(localTempFile, remoteTempFile),
                "vim" => LaunchVimDiff(localTempFile, remoteTempFile),
                "code" => LaunchVSCode(localTempFile, remoteTempFile),
                "vscode" => LaunchVSCode(localTempFile, remoteTempFile),
                _ => LaunchDefaultDiff(localTempFile, remoteTempFile, toolName)
            };

            if (process != null)
            {
                using (process)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        ConsoleUI.WriteStatus(true, "Diff completed successfully");
                    }
                    else if (process.ExitCode == 1 && tool == "git")
                    {
                        // Git diff returns 1 when files differ, which is expected
                        ConsoleUI.WriteStatus(true, "Files differ (see diff output above)");
                    }
                    else
                    {
                        ConsoleUI.WriteStatus(false, $"Diff tool exited with code {process.ExitCode}");
                    }
                }
            }
        }
        finally
        {
            // Cleanup temp files
            try
            {
                if (File.Exists(localTempFile)) File.Delete(localTempFile);
                if (File.Exists(remoteTempFile)) File.Delete(remoteTempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static System.Diagnostics.Process? LaunchGitDiff(string localFile, string remoteFile, string toolName)
    {
        try
        {
            // Detect if terminal supports color
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            var colorArg = isWindows ? "--color=auto" : "--color=always";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff --no-index {colorArg} \"{localFile}\" \"{remoteFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            // Add labels to make it clearer
            Console.WriteLine($"--- a/{toolName} (local)");
            Console.WriteLine($"+++ b/{toolName} (remote)");
            Console.WriteLine();

            return System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to launch git diff: {ex.Message}");
            ConsoleUI.WriteInfo("Make sure git is installed and in your PATH", ConsoleColor.Yellow);
            return null;
        }
    }

    private static System.Diagnostics.Process? LaunchVimDiff(string localFile, string remoteFile)
    {
        try
        {
            // On Windows, try vimdiff first, then vim with -d flag
            // On Unix-like systems, use vimdiff
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            var vimCommand = isWindows ? "vimdiff" : "vimdiff";
            var arguments = $"\"{localFile}\" \"{remoteFile}\"";

            // If on Windows and vimdiff not found, try vim with -d flag
            if (isWindows)
            {
                try
                {
                    var testProcess = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "vimdiff",
                        Arguments = "--help",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var test = System.Diagnostics.Process.Start(testProcess);
                    // vimdiff exists, use it
                }
                catch
                {
                    // vimdiff not found, fall back to vim with -d
                    vimCommand = "vim";
                    arguments = $"-d \"{localFile}\" \"{remoteFile}\"";
                }
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = vimCommand,
                Arguments = arguments,
                UseShellExecute = false
            };

            return System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to launch vimdiff: {ex.Message}");
            ConsoleUI.WriteInfo("Make sure vim is installed and in your PATH", ConsoleColor.Yellow);
            return null;
        }
    }

    private static System.Diagnostics.Process? LaunchVSCode(string localFile, string remoteFile)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"--diff \"{localFile}\" \"{remoteFile}\"",
                UseShellExecute = false
            };

            return System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to launch VS Code: {ex.Message}");
            ConsoleUI.WriteInfo("Make sure VS Code is installed and 'code' is in your PATH", ConsoleColor.Yellow);
            return null;
        }
    }

    private static System.Diagnostics.Process? LaunchDefaultDiff(string localFile, string remoteFile, string toolName)
    {
        // Try git diff first as it's most commonly available
        var process = LaunchGitDiff(localFile, remoteFile, toolName);
        if (process != null) return process;

        // Fall back to simple inline diff
        ConsoleUI.WriteInfo("No external diff tool available, showing inline diff", ConsoleColor.Yellow);
        var localContent = File.ReadAllText(localFile);
        var remoteContent = File.ReadAllText(remoteFile);
        ShowInlineDiff(localContent, remoteContent, toolName);
        return null;
    }

    #endregion

    /// <summary>
    /// Handles the tool migrate command to migrate V1 tools to V2 format.
    /// </summary>
    public static async Task<int> HandleMigrateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting tool migrate command");

        var toolName = parseResult.GetValue(ToolCommandOptions.Migrate.NameOption);
        var migrateAll = parseResult.GetValue(ToolCommandOptions.Migrate.AllOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.Migrate.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {toolName ?? "none"}, All: {migrateAll}, DryRun: {dryRun}");

        if (!Directory.Exists(ToolsDirectory))
        {
            ConsoleUI.WriteStatus(false, "No tools directory found.");
            return 1;
        }

        List<string> filesToMigrate = [];

        if (migrateAll)
        {
            filesToMigrate = Directory.GetFiles(ToolsDirectory, "*.yaml", SearchOption.AllDirectories).ToList();
        }
        else
        {
            var toolFile = ExtendedToolHelper.FindToolFile(toolName!);
            if (toolFile == null)
            {
                ConsoleUI.WriteStatus(false, $"Tool file not found for '{toolName}'");
                ConsoleUI.WriteInfo($"Expected: tools/{toolName}.yaml or tools/{toolName}/{toolName}.yaml", ConsoleColor.Gray);
                return 1;
            }
            filesToMigrate.Add(toolFile);
        }

        if (filesToMigrate.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No tool YAML files found to migrate.");
            return 1;
        }

        ConsoleUI.WriteSection($"Migrating {filesToMigrate.Count} tool(s) from V1 to V2{(dryRun ? " (DRY RUN)" : "")}");
        Console.WriteLine();

        int migratedCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        foreach (var file in filesToMigrate)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var content = await File.ReadAllTextAsync(file);

                // Step 1: Create List<ExtendedToolV2>
                List<ExtendedToolV2> v2Tools = [];
                bool isToolList = false;

                // Check if this is a ToolList by looking at the kind and api_version fields
                var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlDict = yamlDeserializer.Deserialize<Dictionary<string, object>>(content);
                var kind = yamlDict.TryGetValue("kind", out var kindObj) ? kindObj?.ToString() : null;
                var apiVersion = yamlDict.TryGetValue("api_version", out var apiObj) ? apiObj?.ToString() : null;

                // Step 2: If kind = ToolList and api_version = V1, process as ToolList
                if (string.Equals(kind, "ToolList", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(apiVersion, YamlApiVersion.V1, StringComparison.OrdinalIgnoreCase))
                {
                    isToolList = true;
                    var toolListV1 = ExtendedToolListV1.ParseYaml(content);
                    if (toolListV1?.Spec?.Tools == null || toolListV1.Spec.Tools.Count == 0)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: ToolList is empty", ConsoleColor.Yellow);
                        skippedCount++;
                        continue;
                    }

                    v2Tools = Converters.ExtendedToolConverter.ConvertToV2(toolListV1);
                }
                else
                {
                    // Step 3: Detect version and process as single file (original logic)
                    var detectedVersion = ExtendedToolHelper.DetectVersion(file);

                    // Check if file is already V2
                    if (detectedVersion == YamlApiVersion.V2)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Already V2 format", ConsoleColor.Gray);
                        skippedCount++;
                        continue;
                    }

                    // Check if file is V1
                    if (detectedVersion != YamlApiVersion.V1)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Not a V1 tool file", ConsoleColor.Yellow);
                        skippedCount++;
                        continue;
                    }

                    // Convert single V1 tool to V2
                    var v1Tool = ExtendedToolV1.ParseYaml(content);
                    if (v1Tool == null)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Failed to deserialize V1 format", ConsoleColor.Red);
                        errorCount++;
                        continue;
                    }

                    ExtendedToolV2? v2Tool = null;
                    var toolType = v1Tool.Type;

                    if (ToolName.KustoTool == toolType)
                    {
                        var kustoV1 = KustoToolV1.ParseYaml(content);
                        v2Tool = Converters.ExtendedToolConverter.ConvertToV2(kustoV1);
                    }
                    else if (ToolName.LinkTool == toolType)
                    {
                        var linkV1 = LinkToolV1.ParseYaml(content);
                        v2Tool = Converters.ExtendedToolConverter.ConvertToV2(linkV1);
                    }
                    else
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Unsupported tool type '{v1Tool.Type}'", ConsoleColor.Red);
                        errorCount++;
                        continue;
                    }

                    if (v2Tool == null)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Failed to convert to V2 format", ConsoleColor.Red);
                        errorCount++;
                        continue;
                    }

                    v2Tools.Add(v2Tool);
                }

                // Step 4: Process the v2Tools based on dry-run flag
                if (dryRun)
                {
                    if (isToolList)
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Would migrate {v2Tools.Count} tools to V2", ConsoleColor.Green);
                    }
                    else
                    {
                        ConsoleUI.WriteBullet($"{fileName}: Would migrate to V2", ConsoleColor.Green);
                    }
                }
                else
                {
                    // Perform actual migration
                    var fileDir = Path.GetDirectoryName(file);

                    if (isToolList)
                    {
                        // Save each tool from ToolList as separate V2 file
                        bool originalFileOverwritten = false;
                        foreach (var tool in v2Tools)
                        {
                            var convertedToolName = tool.Metadata?.Name ?? "UnnamedTool";
                            var outputFile = Path.Combine(fileDir!, $"{convertedToolName}.yaml");

                            // Check if we're about to overwrite the original file
                            if (string.Equals(outputFile, file, StringComparison.OrdinalIgnoreCase))
                            {
                                originalFileOverwritten = true;
                            }

                            await tool.SaveYamlAsync(outputFile);
                        }

                        // Step 5: Backup original ToolList file only if it was not overwritten
                        if (!originalFileOverwritten)
                        {
                            var backupFile = file + ".v1.bak";
                            if (File.Exists(backupFile))
                            {
                                File.Delete(backupFile);
                            }
                            File.Move(file, backupFile);
                            ConsoleUI.WriteBullet($"{fileName}: Migrated {v2Tools.Count} tools to V2 (original backed up to {Path.GetFileName(backupFile)})", ConsoleColor.Green);
                        }
                        else
                        {
                            ConsoleUI.WriteBullet($"{fileName}: Migrated {v2Tools.Count} tools to V2 (original file overwritten)", ConsoleColor.Green);
                        }
                    }
                    else
                    {
                        // Save single tool in-place (overwrites original file)
                        await v2Tools[0].SaveYamlAsync(file);
                        ConsoleUI.WriteBullet($"{fileName}: Migrated to V2", ConsoleColor.Green);
                    }
                }

                migratedCount += v2Tools.Count;
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteBullet($"{Path.GetFileName(file)}: Error - {ex.Message}", ConsoleColor.Red);
                errorCount++;
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Migration Summary");
        ConsoleUI.WriteKeyValue("Total files", filesToMigrate.Count.ToString());
        ConsoleUI.WriteKeyValue("Migrated", migratedCount.ToString(), valueColor: ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("Skipped", skippedCount.ToString(), valueColor: ConsoleColor.Gray);
        ConsoleUI.WriteKeyValue("Errors", errorCount.ToString(), valueColor: errorCount > 0 ? ConsoleColor.Red : ConsoleColor.Gray);

        if (dryRun && migratedCount > 0)
        {
            Console.WriteLine();
            ConsoleUI.WriteInfo("This was a dry run. No files were modified.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Run without --dry-run to apply changes.", ConsoleColor.Yellow);
        }

        if (migratedCount > 0 && !dryRun)
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Next Steps");
            ConsoleUI.WriteCommand("Validate migrated tools", "srectl tool validate --all");
            ConsoleUI.WriteCommand("Apply to server", "srectl sync");
        }

        return errorCount > 0 ? 1 : 0;
    }
}
