// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Cli.Validations;
using Agent.Data.Tools;
using Agent.Framework;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

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
                ConsoleUI.WriteStatus(false, $"Unknown tool type '{type}'");
                ConsoleUI.WriteSection("Available tool types");
                foreach (var availableType in availableTypes)
                {
                    ConsoleUI.WriteBullet($"{availableType.Name}: {availableType.Description}");
                }
                Console.WriteLine();
                ConsoleUI.WriteInfo("Use 'srectl tool show-types' for more details.");
                Environment.Exit(1);
                return;
            }

            // Tool validation
            if (!ToolValidation.ValidateTool(name!, type!, out var errors))
            {
                DebugLogger.LogValidation($"Tool {name}", false, errors);
                ConsoleUI.WriteStatus(false, "Tool validation failed");
                foreach (var error in errors)
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red);
                Environment.Exit(1);
                return;
            }

            DebugLogger.LogValidation($"Tool {name}", true);

            // Create tool using template based on actual definitions
            var toolYaml = CreateToolFromTemplate(name!, type!, extra);

            // For KustoTool, prepend a helpful header with modification + permissions guidance
            if (!string.IsNullOrWhiteSpace(type) && type.Equals("KustoTool", StringComparison.OrdinalIgnoreCase))
            {
                var header = string.Join('\n', new[]
                {
                    "# NOTE: This is a Kusto tool template. Update it before applying.",
                    "# - Set a meaningful description, database, and query.",
                    "# - Ensure the 'connector' points to a configured Kusto connector.",
                    "# - Verify the connector principal has required ADX permissions (see http://aka.ms/1psreagent).",
                    ""
                });
                toolYaml = header + toolYaml;
            }

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
            ConsoleUI.WriteStatus(true, $"Tool YAML created at {yamlPath}");
            ConsoleUI.WriteKeyValue("Tool type", $"{toolTypeInfo.Name} - {toolTypeInfo.Description}");
            Console.WriteLine();
            ConsoleUI.WriteSection("Next Steps");
            ConsoleUI.WriteCommand("Review and customize", "Edit the generated YAML file");
            ConsoleUI.WriteCommand("Update connector", "Set the correct connector reference");
            ConsoleUI.WriteCommand("Validate tool", $"srectl tool validate --name {name}");
            ConsoleUI.WriteCommand("Apply tool", $"srectl tool apply --name {name}");

            // Kusto-specific post-create reminders
            if (!string.IsNullOrWhiteSpace(type) && type.Equals("KustoTool", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                ConsoleUI.WriteSection("Kusto prerequisites");
                ConsoleUI.WriteBullet("Edit YAML to set database, cluster, data connection and query.", ConsoleColor.Yellow);
                ConsoleUI.WriteBullet("Ensure the connector principal has the required ADX permissions.", ConsoleColor.Yellow);
                ConsoleUI.WriteCommand("Docs", "http://aka.ms/1psreagent");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"CreateTool failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, ex.Message);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool validate command.
    /// </summary>
    public static void HandleValidateCommand(ParseResult parseResult)
    {
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
            ConsoleUI.WriteStatus(false, "Please provide either --name or --all for tool validation.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool apply command.
    /// </summary>
    public static async Task HandleApplyCommand(ParseResult parseResult)
    {
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
    public static async Task HandleShowConnectorsCommand(ParseResult parseResult)
    {
        try
        {
            var verbose = parseResult.GetValue(ToolCommandOptions.VerboseOption);

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

            if (!connectorTypes.Any())
            {
                ConsoleUI.WriteStatus(false, "No connector types found.");
                return;
            }

            foreach (var connector in connectorTypes)
            {
                ConsoleUI.WriteKeyValue(connector.Name, connector.Description, 20, ConsoleColor.Yellow, ConsoleColor.Gray);

                if (verbose)
                {
                    ConsoleUI.WriteBullet($"Type: {connector.TypeName}", ConsoleColor.DarkGray, 4);
                    ConsoleUI.WriteBullet($"Assembly: {connector.Assembly}", ConsoleColor.DarkGray, 4);
                    ConsoleUI.WriteBullet($"Namespace: {connector.Namespace}", ConsoleColor.DarkGray, 4);
                }
                Console.WriteLine();
            }

            ConsoleUI.WriteKeyValue("Total", $"{connectorTypes.Count} connector type(s)", 10);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, ex.Message);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool list command to display available tools.
    /// </summary>
    public static async Task HandleListCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting tool list command");

        var listAll = parseResult.GetValue(ToolCommandOptions.ListAllOption);

        DebugLogger.Debug("Parameters", $"ListAll: {listAll}");

        try
        {
            using var apiService = new ApiService();

            if (listAll)
            {
                // List both extended tools and legacy tools
                ConsoleUI.WriteSection("All Available Tools");
                Console.WriteLine();

                // Get extended tools first
                ConsoleUI.WriteInfo("Extended Tools (recommended):", ConsoleColor.Cyan);
                var (extendedSuccess, extendedResponse) = await apiService.ListExtendedToolsAsync();

                if (extendedSuccess)
                {
                    Console.WriteLine(extendedResponse);
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Failed to retrieve extended tools");
                    Console.WriteLine($"   {extendedResponse}");
                }

                Console.WriteLine();
                ConsoleUI.DrawLine(60);
                Console.WriteLine();

                // Get legacy tools
                ConsoleUI.WriteInfo("Legacy Tools:", ConsoleColor.Yellow);
                var (legacySuccess, legacyResponse) = await apiService.ListToolsAsync();

                if (legacySuccess)
                {
                    Console.WriteLine(legacyResponse);
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Failed to retrieve legacy tools");
                    Console.WriteLine($"   {legacyResponse}");
                }

                // Summary
                Console.WriteLine();
                ConsoleUI.WriteSection("Summary");
                ConsoleUI.WriteBullet("Extended tools support advanced features and are recommended for new developments", ConsoleColor.Green);
                ConsoleUI.WriteBullet("Legacy tools are maintained for backward compatibility", ConsoleColor.Yellow);
                Console.WriteLine();
                ConsoleUI.WriteCommand("Create new tool", "srectl tool create --name <name> --type <type>");
                ConsoleUI.WriteCommand("Apply existing tool", "srectl tool apply --name <name>");

                Environment.Exit((extendedSuccess || legacySuccess) ? 0 : 1);
            }
            else
            {
                // Default behavior: list extended tools only (mirrors list extended-tools)
                var (success, response) = await apiService.ListExtendedToolsAsync();

                if (success)
                {
                    Console.WriteLine(response);
                    Environment.Exit(0);
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Failed to retrieve tools");
                    Console.WriteLine(response);
                    Environment.Exit(1);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListTools failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to list tools: {ex.Message}");
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

    private static string NormalizeNewlines(string s)
    {
        return (s ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static void RemoveKeyIgnoreCase(YamlMappingNode mapping, string keyName)
    {
        YamlNode? toRemove = null;
        foreach (var kv in mapping.Children)
        {
            if (kv.Key is YamlScalarNode ks &&
                string.Equals(ks.Value, keyName, StringComparison.OrdinalIgnoreCase))
            {
                toRemove = kv.Key;
                break;
            }
        }
        if (toRemove != null)
        {
            mapping.Children.Remove(toRemove);
        }
    }

    private static string CreateToolFromTemplate(string name, string type, string[]? extra)
    {
        var details = ToolDefinitionService.GetToolTypeDetails(type)
            ?? throw new InvalidOperationException($"Unable to get details for tool type '{type}'");

        // Base template substitutions
        var yamlContent = details.SampleYaml
            .Replace("MyKustoTool", name)
            .Replace("MyKustoQuery", name)
            .Replace("CheckResourceImpact", name)   // legacy alias if present
            .Replace($"My{type}", name);

        if (extra == null || extra.Length == 0)
        {
            return yamlContent;
        }

        var keyValuePairs = ArgumentParser.ParseKeyValuePairs(extra); // likely Dictionary<string, object>

        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yamlContent));

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                // Fall back to append mode if we can't safely edit the DOM
                var sbNoDom = new StringBuilder(yamlContent.TrimEnd());
                foreach (var kv in keyValuePairs)
                {
                    var raw = kv.Value?.ToString() ?? string.Empty;
                    if (kv.Key.Equals("description", StringComparison.OrdinalIgnoreCase))
                    {
                        sbNoDom.AppendLine()
                               .AppendLine("description: |");
                        foreach (var line in NormalizeNewlines(raw).Split('\n'))
                        {
                            sbNoDom.Append("  ").AppendLine(line);
                        }
                    }
                    else
                    {
                        sbNoDom.AppendLine()
                               .Append(kv.Key).Append(": ").Append(raw);
                    }
                }
                return sbNoDom.ToString();
            }

            // DOM update: remove existing keys (case-insensitive), then add
            foreach (var kv in keyValuePairs)
            {
                var raw = kv.Value?.ToString() ?? string.Empty;

                RemoveKeyIgnoreCase(root, kv.Key);

                var keyNode = new YamlScalarNode(kv.Key);
                YamlScalarNode valueNode;
                if (kv.Key.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    valueNode = new YamlScalarNode(NormalizeNewlines(raw)) { Style = ScalarStyle.Literal }; // |
                }
                else
                {
                    valueNode = new YamlScalarNode(raw) { Style = ScalarStyle.Plain };
                }

                root.Add(keyNode, valueNode);
            }

            var sw = new StringWriter();
            stream.Save(sw, assignAnchors: false);
            return sw.ToString();
        }
        catch
        {
            // Fallback: append keys to the end, with description as a block
            var sb = new StringBuilder(yamlContent.TrimEnd());
            foreach (var kv in keyValuePairs)
            {
                var raw = kv.Value?.ToString() ?? string.Empty;

                if (kv.Key.Equals("description", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine()
                      .AppendLine("description: |");
                    foreach (var line in NormalizeNewlines(raw).Split('\n'))
                    {
                        sb.Append("  ").AppendLine(line);
                    }
                }
                else
                {
                    sb.AppendLine()
                      .Append(kv.Key).Append(": ").Append(raw);
                }
            }
            return sb.ToString();
        }
    }

    private static void ValidateAllTools(YamlDotNet.Serialization.IDeserializer deserializer)
    {
        var toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            ConsoleUI.WriteStatus(false, "No tools directory found.");
            Environment.Exit(1);
        }

        var files = Directory.GetFiles(toolsDir, "*.yaml", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No tool YAML files found in tools directory.");
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

                DebugLogger.Debug("ToolValidation", $"Validating tool '{toolName}' of type '{toolType}' from {file}");

                // Basic validation first
                if (!ToolValidation.ValidateTool(toolName, toolType, out var basicErrors))
                {
                    allValid = false;
                    DebugLogger.LogValidation($"Tool {toolName} (basic)", false, basicErrors);
                    ConsoleUI.WriteStatus(false, $"{file}: Basic validation failed");
                    foreach (var error in basicErrors)
                        ConsoleUI.WriteBullet(error, ConsoleColor.Red, 6);
                    continue;
                }

                // YAML and type-specific validation
                if (!ToolValidation.ValidateToolYaml(yaml, out var yamlErrors))
                {
                    allValid = false;
                    DebugLogger.LogValidation($"Tool {toolName} (YAML)", false, yamlErrors);
                    ConsoleUI.WriteStatus(false, $"{file}: YAML validation failed");
                    foreach (var error in yamlErrors)
                        ConsoleUI.WriteBullet(error, ConsoleColor.Red, 6);
                    continue;
                }

                DebugLogger.LogValidation($"Tool {toolName}", true);
                ConsoleUI.WriteStatus(true, $"{file}: Validation succeeded");
            }
            catch (Exception ex)
            {
                allValid = false;
                DebugLogger.Debug("ToolValidation", $"Exception validating {file}: {ex.Message}");
                ConsoleUI.WriteStatus(false, $"{file}: Exception during validation: {ex.Message}");
            }
        }
        if (allValid)
            ConsoleUI.WriteStatus(true, "All tool YAML files are valid");
        else
        {
            ConsoleUI.WriteStatus(false, "Some tool YAML files failed validation");
            Environment.Exit(1);
        }
    }

    private static void ValidateSingleTool(string name, YamlDotNet.Serialization.IDeserializer deserializer)
    {
        var filePath = FindToolFile(name);
        if (filePath == null)
        {
            ConsoleUI.WriteStatus(false, $"Tool YAML file not found for tool '{name}'");
            ConsoleUI.WriteBullet($"Searched in tools directory and subdirectories for '{name}.yaml'", ConsoleColor.Yellow);
            Environment.Exit(1);
        }

        try
        {
            var yaml = File.ReadAllText(filePath, Encoding.UTF8);
            var doc = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            string toolName = doc.TryGetValue("name", out var n) ? n?.ToString() ?? string.Empty : string.Empty;
            string toolType = doc.TryGetValue("type", out var t) ? t?.ToString() ?? string.Empty : string.Empty;

            DebugLogger.Debug("ToolValidation", $"Validating tool '{toolName}' of type '{toolType}' from {filePath}");

            // First validate basic tool properties
            if (!ToolValidation.ValidateTool(toolName, toolType, out var basicErrors))
            {
                DebugLogger.LogValidation($"Tool {toolName} (basic)", false, basicErrors);
                ConsoleUI.WriteStatus(false, $"Basic tool validation failed for '{name}' at {filePath}");
                foreach (var error in basicErrors)
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red);
                Environment.Exit(1);
                return;
            }

            // Then validate YAML content and type-specific requirements
            if (!ToolValidation.ValidateToolYaml(yaml, out var yamlErrors))
            {
                DebugLogger.LogValidation($"Tool {toolName} (YAML)", false, yamlErrors);
                ConsoleUI.WriteStatus(false, $"Tool YAML validation failed for '{name}' at {filePath}");
                foreach (var error in yamlErrors)
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red);
                Environment.Exit(1);
                return;
            }

            DebugLogger.LogValidation($"Tool {toolName}", true);
            ConsoleUI.WriteStatus(true, $"Tool validation succeeded for '{name}' at {filePath}");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Exception during validation: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void ShowAllToolTypes(bool verbose)
    {
        ConsoleUI.WriteSection("Available Tool Types");

        var toolTypes = ToolDefinitionService.GetAvailableToolTypes();

        if (!toolTypes.Any())
        {
            ConsoleUI.WriteStatus(false, "No tool types found.");
            return;
        }

        foreach (var toolType in toolTypes)
        {
            ConsoleUI.WriteKeyValue("🔧 " + toolType.Name, toolType.Description, 20, ConsoleColor.Yellow, ConsoleColor.Gray);

            if (verbose)
            {
                ConsoleUI.WriteBullet($"Type: {toolType.TypeName}", ConsoleColor.DarkGray, 4);
                ConsoleUI.WriteBullet($"Assembly: {toolType.Assembly}", ConsoleColor.DarkGray, 4);
                ConsoleUI.WriteBullet($"Namespace: {toolType.Namespace}", ConsoleColor.DarkGray, 4);
            }
            Console.WriteLine();
        }

        ConsoleUI.WriteKeyValue("Total", $"{toolTypes.Count} tool type(s)", 10);
        Console.WriteLine();
        ConsoleUI.WriteInfo("Use 'srectl tool show-types --type <ToolTypeName>' for detailed information");
    }

    private static void ShowSpecificToolTypeDetails(string toolTypeName)
    {
        var details = ToolDefinitionService.GetToolTypeDetails(toolTypeName);

        if (details == null)
        {
            ConsoleUI.WriteStatus(false, $"Tool type '{toolTypeName}' not found.");
            ConsoleUI.WriteInfo("Use 'srectl tool show-types' to see available tool types.");
            Environment.Exit(1);
            return;
        }

        ConsoleUI.WriteSection($"🔧 Tool Type Details: {details.Name}");

        ConsoleUI.WriteKeyValue("Description", details.Description, 12);
        ConsoleUI.WriteKeyValue("Type", details.TypeName, 12);
        ConsoleUI.WriteKeyValue("Assembly", details.Assembly, 12);
        ConsoleUI.WriteKeyValue("Namespace", details.Namespace, 12);
        Console.WriteLine();

        if (details.SupportedProperties.Any())
        {
            ConsoleUI.WriteSection("Supported Properties");
            foreach (var prop in details.SupportedProperties)
            {
                ConsoleUI.WriteBullet(prop);
            }
            Console.WriteLine();
        }

        ConsoleUI.WriteSection("Sample YAML");
        ConsoleUI.DrawLine();
        Console.WriteLine(details.SampleYaml);
        Console.WriteLine();

        ConsoleUI.WriteStatus(true, $"Tool type details displayed for '{toolTypeName}'");
    }

    /// <summary>
    /// Handles the tool delete command.
    /// </summary>
    public static async Task HandleDeleteCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting tool delete command");

        var toolName = parseResult.GetValue(ToolCommandOptions.DeleteNameOption);
        var dryRun = parseResult.GetValue(ToolCommandOptions.DeleteDryRunOption);

        DebugLogger.Debug("Parameters", $"ToolName: {toolName}, DryRun: {dryRun}");

        if (string.IsNullOrWhiteSpace(toolName))
        {
            ConsoleUI.WriteStatus(false, "Tool name is required.");
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
            ConsoleUI.WriteInfo($"Deleting tool '{toolName}'...", ConsoleColor.Yellow);

            var (success, response) = await apiService.DeleteToolAsync(toolName);

            if (success)
            {
                ConsoleUI.WriteStatus(true, response);

                // After successful server deletion, offer to clean up local files
                OfferLocalToolCleanup(toolName);
            }
            else
            {
                ConsoleUI.WriteStatus(false, response);
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteTool failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to delete tool: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the tool diff command to compare local and remote configurations.
    /// </summary>
    public static async Task HandleDiffCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting tool diff command");

        var toolName = parseResult.GetValue(ToolCommandOptions.DiffNameOption);
        var diffTool = parseResult.GetValue(ToolCommandOptions.DiffToolOption) ?? "git";
        var showRaw = parseResult.GetValue(ToolCommandOptions.DiffRawOption);

        DebugLogger.Debug("Parameters", $"ToolName: {toolName}, Tool: {diffTool}, Raw: {showRaw}");

        if (string.IsNullOrWhiteSpace(toolName))
        {
            ConsoleUI.WriteStatus(false, "Tool name is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            // Find local tool file
            var localPath = FindToolFile(toolName);
            if (localPath == null)
            {
                ConsoleUI.WriteStatus(false, $"Local tool file not found for '{toolName}'");
                ConsoleUI.WriteBullet($"Expected: tools/{toolName}.yaml or tools/{toolName}/{toolName}.yaml", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteInfo($"Comparing tool '{toolName}'...", ConsoleColor.Cyan);

            // Get remote configuration
            using var apiService = new ApiService();
            var (success, remoteYaml, errorMessage) = await apiService.GetToolConfigurationAsync(toolName);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to get remote configuration: {errorMessage}");
                Environment.Exit(1);
                return;
            }



            // Read local configuration
            var localYaml = await File.ReadAllTextAsync(localPath);

            // If both are identical, no need to diff
            if (string.Equals(localYaml.Trim(), remoteYaml.Trim(), StringComparison.Ordinal))
            {
                ConsoleUI.WriteStatus(true, "Local and remote configurations are identical");
                Environment.Exit(0);
                return;
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
                    "KustoTool" => remoteDeserializer.Deserialize<KustoToolDefinition>(remoteYaml),
                    // "LinkTool" => remoteDeserializer.Deserialize<LinkToolDefinition>(remoteYaml),
                    _ => throw new NotSupportedException($"Unknown tool type: {toolType}")
                };

                // Also deserialize local YAML using the underscored tool type
                YamlToolDefinitionBase localTool = toolType switch
                {
                    "KustoTool" => localDeserializer.Deserialize<KustoToolDefinition>(localYaml),
                    // "LinkTool" => localDeserializer.Deserialize<LinkToolDefinition>(localYaml),
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
                await LaunchDiffTool(localContent, remoteContent, toolName, diffTool, ".yaml");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Diff failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to compare tool: {ex.Message}");
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
            ConsoleUI.WriteSection($"DRY RUN: Tool apply for '{toolName}'");
            ConsoleUI.DrawLine();

            // Find and validate tool file exists
            var toolFilePath = FindToolFile(toolName);
            if (toolFilePath == null)
            {
                ConsoleUI.WriteStatus(false, $"Tool file not found for '{toolName}'");
                ConsoleUI.WriteBullet($"Searched in tools directory and subdirectories for '{toolName}.yaml'", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteKeyValue("Tool file found", toolFilePath);

            // Read and parse the YAML file
            var yamlContent = await File.ReadAllTextAsync(toolFilePath);
            ConsoleUI.WriteKeyValue("Content size", $"{yamlContent.Length} characters");

            // Validate YAML structure
            var deserializer = YamlHelper.CreateCamelCaseDeserializer();
            try
            {
                var toolConfig = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                ConsoleUI.WriteStatus(true, "YAML structure is valid");
                Console.WriteLine();
                ConsoleUI.WriteSection("Tool Details");

                if (toolConfig.TryGetValue("name", out var nameValue))
                    ConsoleUI.WriteBullet($"Name: {nameValue}");
                if (toolConfig.TryGetValue("type", out var typeValue))
                    ConsoleUI.WriteBullet($"Type: {typeValue}");
                if (toolConfig.TryGetValue("description", out var descValue))
                    ConsoleUI.WriteBullet($"Description: {descValue}");
                if (toolConfig.TryGetValue("connector", out var connectorValue))
                    ConsoleUI.WriteBullet($"Connector: {connectorValue}");
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"YAML parsing failed: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Check server connectivity
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                ConsoleUI.WriteStatus(false, "Configuration not found. Run 'srectl init' first.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteKeyValue("Target server", config.ResourceUrl);
            ConsoleUI.WriteKeyValue("Auth required", config.AuthRequired.ToString());

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, "DRY RUN COMPLETE");
            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteBullet($"Tool '{toolName}' configuration is valid", ConsoleColor.Green);
            ConsoleUI.WriteBullet("YAML file can be parsed successfully", ConsoleColor.Green);
            ConsoleUI.WriteBullet("Server configuration is available", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Would apply to: {config.ResourceUrl}", ConsoleColor.Green);
            Console.WriteLine();
            ConsoleUI.WriteCommand("To apply", $"srectl tool apply --name {toolName}");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"DRY RUN FAILED: {ex.Message}");
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
            ConsoleUI.WriteSection($"DRY RUN: Tool delete for '{toolName}'");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Check if tool exists locally
            var toolFilePath = FindToolFile(toolName);
            if (toolFilePath != null)
            {
                ConsoleUI.WriteInfo($"Local tool file found: {toolFilePath}", ConsoleColor.Green);
            }
            else
            {
                ConsoleUI.WriteInfo($"No local tool file found for '{toolName}'", ConsoleColor.Yellow);
            }

            // Check server connectivity
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                ConsoleUI.WriteStatus(false, "Configuration not found. Run 'srectl init' first.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteKeyValue("Target server", config.ResourceUrl);
            ConsoleUI.WriteKeyValue("Authentication required", config.AuthRequired.ToString());

            // Check for dependencies by searching for tool references in agent files
            var dependencies = FindToolDependencies(toolName);
            if (dependencies.Any())
            {
                ConsoleUI.WriteInfo($"Found {dependencies.Count} potential dependencies:", ConsoleColor.Yellow);
                foreach (var dep in dependencies)
                {
                    Console.WriteLine($"   • {dep}");
                }
                Console.WriteLine("   Delete might fail if these agents are deployed and reference this tool");
            }
            else
            {
                ConsoleUI.WriteStatus(true, "No local dependencies found");
            }

            ConsoleUI.WriteStatus(true, "DRY RUN COMPLETE");
            ConsoleUI.WriteSection("Summary");
            Console.WriteLine($"   • Tool '{toolName}' would be deleted from server");
            Console.WriteLine($"   • Target server: {config.ResourceUrl}");
            if (dependencies.Any())
            {
                ConsoleUI.WriteBullet($"{dependencies.Count} potential dependencies found", ConsoleColor.Yellow);
            }
            ConsoleUI.WriteCommand("To actually delete the tool", $"srectl tool delete --name {toolName}");
            if (dependencies.Any())
            {
                ConsoleUI.WriteInfo("Consider updating dependent agents first to avoid deployment issues", ConsoleColor.Yellow);
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"DRY RUN FAILED: {ex.Message}");
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

    /// <summary>
    /// Offers to clean up local tool files after successful server deletion.
    /// </summary>
    private static void OfferLocalToolCleanup(string toolName)
    {
        var toolFile = FindToolFile(toolName);

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
}
