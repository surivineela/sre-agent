// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Core.Validation;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles agent-related command operations.
/// </summary>
public static class AgentCommandHandlers
{
    /// <summary>
    /// Handles the agent create command.
    /// </summary>
    public static async Task<int> HandleCreateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent create command");

        var name = parseResult.GetValue(AgentCommandOptions.Create.NameOption);
        var instructions = parseResult.GetValue(AgentCommandOptions.Create.InstructionsOption);
        var tools = parseResult.GetValue(AgentCommandOptions.Create.ToolsOption);
        var handoffDescription = parseResult.GetValue(AgentCommandOptions.Create.HandoffDescriptionOption);
        var handoffs = parseResult.GetValue(AgentCommandOptions.Create.HandoffsOption);
        var allowParallelToolCalls = parseResult.GetValue(AgentCommandOptions.Create.AllowParallelToolCallsOption);
        var maxReflectionCount = parseResult.GetValue(AgentCommandOptions.Create.MaxReflectionCountOption);
        var criticPromptPath = parseResult.GetValue(AgentCommandOptions.Create.CriticPromptPathOption);
        var criticOnHandoff = parseResult.GetValue(AgentCommandOptions.Create.CriticOnHandoffOption);
        var customReflectionNote = parseResult.GetValue(AgentCommandOptions.Create.CustomReflectionNoteOption);
        var commonPrompts = parseResult.GetValue(AgentCommandOptions.Create.CommonPromptsOption);
        var temperature = parseResult.GetValue(AgentCommandOptions.Create.TemperatureOption);
        var outputType = parseResult.GetValue(AgentCommandOptions.Create.OutputTypeOption);
        var vanillaMode = parseResult.GetValue(AgentCommandOptions.Create.VanillaModeOption);
        var useSmart = parseResult.GetValue(AgentCommandOptions.Create.SmartOption);
        var enableSkills = parseResult.GetValue(AgentCommandOptions.Create.EnableSkillsOption);
        var addSystemSkills = parseResult.GetValue(AgentCommandOptions.Create.AddSystemSkillsOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, Smart: {useSmart}, Tools: {tools?.Length ?? 0} items");

        string finalInstructions;
        List<string> finalTools;
        List<string> finalMcpTools;

        // Initialize progress tracking for all agent creation
        var steps = useSmart
            ? new[] {
                "Analyzing requirements and generating instructions",
                "Recommending appropriate tools",
                "Creating agent configuration",
                "Writing agent files",
                "Validating with server"
              }
            : [
                "Creating agent configuration",
                "Writing agent files",
                "Validating with server"
              ];

        ProgressService.MultiStepProgress.Initialize(steps);

        // Handle smart agent generation
        if (useSmart)
        {
            // Steps already initialized above

            ProgressService.AnimatedSpinner.Start("Generating smart agent with AI");

            using var apiService = new ApiService();
            var (success, generatedInstructions, recommendedTools, recommendedMcpTools, errorMessage) = await apiService.GenerateSmartAgentAsync(name!, instructions);

            ProgressService.AnimatedSpinner.Stop();

            if (!success)
            {
                ProgressService.MultiStepProgress.Fail($"Smart generation failed: {errorMessage}");
                ProgressService.ShowError("Smart generation failed",
                [
                    "Try running without --smart flag",
                    "Check your server connection with --debug",
                    "Verify your server supports AI completion"
                ]);
                return 1;
            }

            ProgressService.MultiStepProgress.NextStep("AI generation completed successfully");

            finalInstructions = generatedInstructions;
            finalTools = recommendedTools;
            finalMcpTools = recommendedMcpTools;

            // TODO: Log mcp tools when supported
            ProgressService.ShowSuccess($"AI generated instructions and {recommendedTools.Count} recommended tools!");
            ConsoleUI.WriteKeyValue("Instructions", generatedInstructions.Length > 100 ? generatedInstructions.Substring(0, 100) + ConsoleUI.Chars.Ellipsis : generatedInstructions, 15);
            ConsoleUI.WriteKeyValue("Tools", string.Join(", ", recommendedTools), 15);
        }
        else
        {
            finalInstructions = instructions ?? $"This is the {name} agent. Please provide specific instructions for what this agent should do.";
            finalTools = tools?.ToList() ?? [];
            finalMcpTools = [];
        }

        // Create ExtendedAgentSpecV2 instance with final values
        var agentSpec = new ExtendedAgentSpecV2
        {
            Instructions = finalInstructions,
            Tools = finalTools,
            Handoffs = handoffs?.ToList() ?? [],
            HandoffDescription = handoffDescription ?? string.Empty,
            AllowParallelToolCalls = allowParallelToolCalls,
            MaxReflectionCount = maxReflectionCount,
            CriticPromptPath = criticPromptPath ?? string.Empty,
            CriticOnHandoff = criticOnHandoff,
            CustomReflectionNote = customReflectionNote ?? string.Empty,
            CommonPrompts = commonPrompts?.ToList() ?? [],
            Temperature = temperature,
            OutputType = outputType,
            EnableVanillaMode = vanillaMode,
            EnableSkills = enableSkills,
            AddSystemSkills = addSystemSkills
        };

        // Validate tool existence before creating the agent
        if (finalTools?.Count > 0)
        {
            ProgressService.MultiStepProgress.NextStep("Validating tool availability");

            try
            {
                var configService = new CliConfigurationService();
                var config = await configService.LoadConfigurationAsync();

                if (config != null && Uri.TryCreate(config.ResourceUrl, UriKind.Absolute, out _))
                {
                    using var validationApiService = new ApiService();
                    var toolAvailabilityService = new ToolAvailabilityService(validationApiService);
                    var (localTools, remoteTools, errors) = await toolAvailabilityService.GetAvailableToolsAsync();
                    var allAvailableTools = new HashSet<string>(localTools.Union(remoteTools));

                    var missingTools = finalTools.Where(tool => !allAvailableTools.Contains(tool)).ToList();

                    if (missingTools.Count != 0)
                    {
                        ProgressService.MultiStepProgress.Fail("Tool validation failed");
                        ConsoleUI.WriteStatus(false, "Agent creation failed: Required tools not found");

                        foreach (var missingTool in missingTools)
                        {
                            ConsoleUI.WriteBullet($"Tool '{missingTool}' is not available locally or on the server", ConsoleColor.Red);
                        }

                        Console.WriteLine();
                        ConsoleUI.WriteSection("Available tools:");
                        var availableToolsList = allAvailableTools.OrderBy(t => t).ToList();
                        if (availableToolsList.Count != 0)
                        {
                            foreach (var tool in availableToolsList.Take(10))
                            {
                                ConsoleUI.WriteBullet(tool, ConsoleColor.Green);
                            }
                            if (availableToolsList.Count > 10)
                            {
                                ConsoleUI.WriteBullet($"... and {availableToolsList.Count - 10} more", ConsoleColor.Gray);
                            }
                        }
                        else
                        {
                            ConsoleUI.WriteBullet("No tools available", ConsoleColor.Yellow);
                        }

                        Console.WriteLine();
                        ConsoleUI.WriteCommand("Create missing tools first", "srectl tool create --name <tool_name> --type <tool_type>");
                        ConsoleUI.WriteCommand("List all available tools", "srectl list tools");
                        return 1;
                    }

                    DebugLogger.Debug("ToolValidation", $"All {finalTools.Count} tools validated successfully");
                }
                else
                {
                    DebugLogger.Debug("ToolValidation", "No valid configuration found, skipping tool validation");
                    ConsoleUI.WriteBullet("Warning: Cannot validate tool existence - no server configuration", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug("ToolValidation", $"Tool validation failed: {ex.Message}");
                ConsoleUI.WriteBullet($"Warning: Unable to validate tool existence: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        // Write the agent YAML file first
        ProgressService.MultiStepProgress.NextStep("Writing agent configuration");

        // Create ExtendedAgentV2 and get YAML string
        var agent = new ExtendedAgentV2
        {
            Metadata = new ResourceMetadataModel { Name = name },
            Spec = agentSpec
        };
        var agentYaml = agent.ToYaml();

        // Write agent to file
        var agentDir = Path.Combine("agents", name!);
        Directory.CreateDirectory(agentDir);
        var yamlPath = Path.Combine(agentDir, $"{name}.yaml");

        DebugLogger.LogFile("WRITE", yamlPath, $"Agent YAML content size: {agentYaml.Length} characters");
        await File.WriteAllTextAsync(yamlPath, agentYaml);

        // Validate using server-side validation (dryRun=true)
        ProgressService.MultiStepProgress.NextStep("Validating agent configuration with server");

        try
        {
            using var apiService = new ApiService();
            var (success, response) = await apiService.ApplyExtendedAgentAsync(name!, dryRun: true);

            if (!success)
            {
                ProgressService.MultiStepProgress.Fail("Server validation failed");
                ConsoleUI.WriteBullet(response, ConsoleColor.Red);
                ConsoleUI.Write(string.Empty);
                ConsoleUI.WriteSection("Troubleshooting");
                ConsoleUI.WriteBullet("Check the agent configuration in the YAML file", ConsoleColor.Yellow);
                ConsoleUI.WriteBullet($"Location: agents/{name}/{name}.yaml", ConsoleColor.Gray);
                return 1;
            }

            ProgressService.MultiStepProgress.NextStep("Validation successful");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteBullet($"Warning: Unable to validate with server: {ex.Message}", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet("Agent created locally but not validated", ConsoleColor.Yellow);
        }

        ConsoleUI.WriteStatus(true, $"Agent '{name}' created successfully!");
        ConsoleUI.WriteKeyValue("Location", $"agents/{name}/{name}.yaml");

        // Show next steps
        ConsoleUI.Write(string.Empty);
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteCommand("Validate the agent", $"srectl agent validate --name {name}");
        ConsoleUI.WriteCommand("Apply to server", $"srectl agent apply --name {name}");
        ConsoleUI.WriteCommand("Test the agent", $"srectl agent test --name {name} --message \"Hello\"");
        ConsoleUI.Write(string.Empty);

        return 0;
    }

    /// <summary>
    /// Handles the agent validate command.
    /// </summary>
    public static async Task HandleValidateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent validate command");

        var validateAll = parseResult.GetValue(AgentCommandOptions.Validate.AllOption);
        var agentName = parseResult.GetValue(AgentCommandOptions.Validate.NameOption);
        var filePath = parseResult.GetValue(AgentCommandOptions.Validate.FileOption);
        var checkTools = parseResult.GetValue(AgentCommandOptions.Validate.CheckToolsOption);

        DebugLogger.Debug("Parameters", $"ValidateAll: {validateAll}, Name: {agentName ?? "none"}, FilePath: {filePath ?? "none"}, CheckTools: {checkTools}");

        if (validateAll)
        {
            await ValidateAllAgentsAsync(checkTools);
        }
        else if (!string.IsNullOrWhiteSpace(agentName))
        {
            var resolvedPath = FindAgentFile(agentName);
            if (resolvedPath == null)
            {
                ConsoleUI.WriteStatus(false, $"Agent file not found for '{agentName}'");
                ConsoleUI.WriteBullet($"Expected: agents/{agentName}.yaml", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }
            await ValidateSingleAgentAsync(resolvedPath, checkTools);
        }
        else if (!string.IsNullOrWhiteSpace(filePath))
        {
            await ValidateSingleAgentAsync(filePath, checkTools);
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Please provide --name, --all, or --file to validate agents.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the agent apply command.
    /// </summary>
    public static async Task<int> HandleApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent apply command");

        var name = parseResult.GetValue(AgentCommandOptions.Apply.NameOption);
        var dryRun = parseResult.GetValue(AgentCommandOptions.Apply.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        if (string.IsNullOrWhiteSpace(name))
        {
            ConsoleUI.WriteStatus(false, "Agent name is required.");
            return 1;
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyExtendedAgentAsync(name, dryRun: dryRun);

        ConsoleUI.WriteStatus(success, response);
        return success ? 0 : 1;
    }

    /// <summary>
    /// Validates all agent YAML files in the agents directory
    /// </summary>
    private static async Task ValidateAllAgentsAsync(bool checkTools = false)
    {
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            ConsoleUI.WriteStatus(false, "No agents directory found.");
            Environment.Exit(1);
        }

        var files = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No agent YAML files found in agents directory.");
            Environment.Exit(1);
        }

        ConsoleUI.WriteSection($"Validating {files.Length} agent(s)");
        Console.WriteLine();

        bool allValid = true;

        foreach (var file in files)
        {
            try
            {
                ConsoleUI.WriteInfo($"Validating {Path.GetFileName(file)}...", ConsoleColor.Cyan);

                // Use ValidateSingleAgentAsync which calls the v2 API with dryRun=true
                await ValidateSingleAgentAsync(file, checkTools);

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                allValid = false;
                ConsoleUI.WriteStatus(false, $"{file}: Exception during validation: {ex.Message}");
                Console.WriteLine();
            }
        }

        if (allValid)
        {
            ConsoleUI.WriteStatus(true, "All agent YAML files are valid");
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Some agent YAML files failed validation");
            Environment.Exit(1);
        }
    }

    private static async Task ValidateSingleAgentAsync(string filePath, bool checkTools = false)
    {
        if (!File.Exists(filePath))
        {
            ConsoleUI.WriteStatus(false, $"Agent YAML file not found: {filePath}");
            Environment.Exit(1);
        }

        // Extract agent name from file path
        var agentName = Path.GetFileNameWithoutExtension(filePath);

        ApiService? apiService = null;
        try
        {
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();

            if (config == null)
            {
                ConsoleUI.WriteStatus(false, "Configuration not found. Run 'srectl init' first.");
                Environment.Exit(1);
                return;
            }

            // Validate URL format
            if (!Uri.TryCreate(config.ResourceUrl, UriKind.Absolute, out _))
            {
                ConsoleUI.WriteStatus(false, $"Invalid resource URL in configuration: {config.ResourceUrl}");
                Environment.Exit(1);
                return;
            }

            apiService = new ApiService();

            // Call server-side validation using v2 API with dryRun=true
            ConsoleUI.WriteInfo($"Validating agent '{agentName}' against server...", ConsoleColor.Yellow);

            var (success, response) = await apiService.ApplyExtendedAgentAsync(agentName, dryRun: true);

            if (success)
            {
                ConsoleUI.WriteStatus(true, response);
            }
            else
            {
                ConsoleUI.WriteStatus(false, response);
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            // Check if this is a configuration corruption issue
            if (ex.Message.Contains("Configuration file") && ex.Message.Contains("corrupted"))
            {
                ConsoleUI.WriteStatus(false, "Configuration corrupted");
                Environment.Exit(1);
                return;
            }
            ConsoleUI.WriteStatus(false, $"Configuration error: {ex.Message}");
            Environment.Exit(1);
            return;
        }
        finally
        {
            // Dispose the API service if we created one
            apiService?.Dispose();
        }
    }

    /// <summary>
    /// Handles the agent delete command.
    /// </summary>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent delete command");

        var agentName = parseResult.GetValue(AgentCommandOptions.Delete.NameOption);

        DebugLogger.Debug("Parameters", $"AgentName: {agentName}");

        if (string.IsNullOrWhiteSpace(agentName))
        {
            ConsoleUI.WriteStatus(false, "Agent name is required.");
            return 1;
        }

        using var apiService = new ApiService();
        ConsoleUI.WriteInfo($"Deleting agent '{agentName}'...", ConsoleColor.Yellow);

        var (success, response) = await apiService.DeleteExtendedAgentAsync(agentName);

        if (success)
        {
            ConsoleUI.WriteStatus(true, response);

            // After successful server deletion, offer to clean up local files
            OfferLocalAgentCleanup(agentName);
            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, response);
            return 1;
        }
    }

    /// <summary>
    /// Handles the agent migrate command to migrate V1 agents to V2 format.
    /// </summary>
    public static async Task<int> HandleMigrateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent migrate command");

        var agentName = parseResult.GetValue(AgentCommandOptions.Migrate.NameOption);
        var migrateAll = parseResult.GetValue(AgentCommandOptions.Migrate.AllOption);
        var dryRun = parseResult.GetValue(AgentCommandOptions.Migrate.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {agentName ?? "none"}, All: {migrateAll}, DryRun: {dryRun}");

        if (!migrateAll && string.IsNullOrWhiteSpace(agentName))
        {
            ConsoleUI.WriteStatus(false, "Please specify --name or --all to migrate agents.");
            return 1;
        }

        if (migrateAll && !string.IsNullOrWhiteSpace(agentName))
        {
            ConsoleUI.WriteStatus(false, "Cannot specify both --name and --all. Choose one.");
            return 1;
        }

        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            ConsoleUI.WriteStatus(false, "No agents directory found.");
            return 1;
        }

        List<string> filesToMigrate = [];

        if (migrateAll)
        {
            filesToMigrate = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories).ToList();
        }
        else
        {
            var agentFile = FindAgentFile(agentName!);
            if (agentFile == null)
            {
                ConsoleUI.WriteStatus(false, $"Agent file not found for '{agentName}'");
                ConsoleUI.WriteInfo($"Expected: agents/{agentName}.yaml", ConsoleColor.Gray);
                return 1;
            }
            filesToMigrate.Add(agentFile);
        }

        if (filesToMigrate.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No agent YAML files found to migrate.");
            return 1;
        }

        ConsoleUI.WriteSection($"Migrating {filesToMigrate.Count} agent(s) from V1 to V2{(dryRun ? " (DRY RUN)" : "")}");
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

                // Check if file is already V2
                if (content.Contains($"api_version: {YamlApiVersion.V2}") || content.Contains($"api_version: \"{YamlApiVersion.V2}\""))
                {
                    ConsoleUI.WriteBullet($"{fileName}: Already V2 format", ConsoleColor.Gray);
                    skippedCount++;
                    continue;
                }

                // Check if file is V1
                if (!content.Contains($"api_version: {YamlApiVersion.V1}") && !content.Contains($"api_version: \"{YamlApiVersion.V1}\""))
                {
                    ConsoleUI.WriteBullet($"{fileName}: Not a V1 agent file", ConsoleColor.Yellow);
                    skippedCount++;
                    continue;
                }

                // Deserialize V1
                var v1Agent = ExtendedAgentV1.ParseYaml(content);
                if (v1Agent == null)
                {
                    ConsoleUI.WriteBullet($"{fileName}: Failed to deserialize V1 format", ConsoleColor.Red);
                    errorCount++;
                    continue;
                }

                var v2Agent = Converters.ExtendedAgentConverter.ConvertToV2(v1Agent);

                if (!dryRun)
                {
                    await v2Agent.SaveYamlAsync(file);
                }

                ConsoleUI.WriteBullet($"{fileName}: Migrated to V2", ConsoleColor.Green);
                migratedCount++;
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
            ConsoleUI.WriteCommand("Validate migrated agents", "srectl agent validate --all");
            ConsoleUI.WriteCommand("Apply to server", "srectl sync");
        }

        return errorCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Handles the list agents command.
    /// </summary>
    public static async Task<int> HandleListCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent list command");

        var search = parseResult.GetValue(AgentCommandOptions.List.SearchOption);
        var name = parseResult.GetValue(AgentCommandOptions.List.NameOption);
        var detail = parseResult.GetValue(AgentCommandOptions.List.DetailOption);

        DebugLogger.Debug("Parameters", $"Search: {search}, Name: {name}, Detail: {detail}");

        using var apiService = new ApiService();

        var (agentsList, error) = await apiService.ListExtendedAgentsAsync(search);

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (agentsList.Count == 0)
        {
            ConsoleUI.WriteInfo("No extended agents found on the server.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Use 'srectl agent apply <agent-name>' to add agents to the server.", ConsoleColor.Gray);
            return 1;
        }

        // Filter by name if specified
        if (!string.IsNullOrWhiteSpace(name))
        {
            var agent = agentsList.FirstOrDefault(a =>
                string.Equals(a.Metadata?.Name, name, StringComparison.OrdinalIgnoreCase));

            if (agent == null)
            {
                ConsoleUI.WriteStatus(false, $"Agent '{name}' not found.");
                return 1;
            }

            ConsoleUI.WriteSection("Remote Extended Agent");
            Console.WriteLine(agent.ToYaml());
            return 0;
        }

        ConsoleUI.WriteSection("Remote Extended Agents");

        for (int i = 0; i < agentsList.Count; i++)
        {
            if (detail)
            {
                var yamlOutput = agentsList[i].ToYaml();
                Console.WriteLine(yamlOutput);
                if (i < agentsList.Count - 1)
                {
                    ConsoleUI.DrawLine();
                }
            }
            else
            {
                var agentName = agentsList[i].Metadata?.Name ?? "Unknown";
                ConsoleUI.WriteBullet(agentName);
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{agentsList.Count} extended agent(s)", 0);
        return 0;
    }

    /// <summary>
    /// Handles the agent test command to test an agent with a specific message.
    /// Delegates to ThreadCommandHandlers.HandleThreadNewCommand for consistency.
    /// </summary>
    public static async Task<int> HandleTestCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting agent test command (delegating to thread new)");

        // Create a modified ParseResult that maps --name to --agent
        // This allows agent test to delegate to thread new while using --name parameter
        var agentName = parseResult.GetValue(AgentCommandOptions.Test.NameOption);

        // Build a new command line with mapped parameters
        var args = new List<string> { "thread", "new" };

        if (!string.IsNullOrEmpty(agentName))
        {
            args.Add("--agent");
            args.Add(agentName);
        }

        var message = parseResult.GetValue(AgentCommandOptions.Test.MessageOption);
        if (!string.IsNullOrEmpty(message))
        {
            args.Add("--message");
            args.Add(message);
        }

        var noWait = parseResult.GetValue(AgentCommandOptions.Test.NoWaitOption);
        if (noWait)
        {
            args.Add("--no-wait");
        }

        // Create a new parse result by parsing the mapped arguments
        var rootCommand = parseResult.CommandResult.Command;
        while (rootCommand.Parents.Any())
        {
            rootCommand = rootCommand.Parents.First() as System.CommandLine.Command ?? rootCommand;
        }

        var newParseResult = rootCommand.Parse(args.ToArray());

        // Delegate to ThreadCommandHandlers.HandleThreadNewCommand
        return await ThreadCommandHandlers.HandleThreadNewCommand(newParseResult, cancellationToken);
    }

    /// <summary>
    /// Handles dry-run for agent apply command.
    /// </summary>
    /// <summary>
    /// Handles the agent diff command to compare local and remote configurations.
    /// </summary>
    public static async Task HandleDiffCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent diff command");

        var agentName = parseResult.GetValue(AgentCommandOptions.Diff.NameOption);
        var diffTool = parseResult.GetValue(AgentCommandOptions.Diff.ToolOption) ?? "git";
        var showRaw = parseResult.GetValue(AgentCommandOptions.Diff.RawOption);

        DebugLogger.Debug("Parameters", $"AgentName: {agentName}, Tool: {diffTool}, Raw: {showRaw}");

        if (string.IsNullOrWhiteSpace(agentName))
        {
            ConsoleUI.WriteStatus(false, "Agent name is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            // Find local agent file
            var localPath = FindAgentFile(agentName);
            if (localPath == null)
            {
                ConsoleUI.WriteStatus(false, $"Local agent file not found for '{agentName}'");
                ConsoleUI.WriteInfo($"Expected: agents/{agentName}.yaml", ConsoleColor.Gray);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteInfo($"Comparing agent '{agentName}'...", ConsoleColor.Cyan);

            // Get remote configuration
            using var apiService = new ApiService();
            var (success, remoteYaml, errorMessage) = await apiService.GetExtendedAgentAsync(agentName);

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

            // Always use YAML format for comparison
            var localContent = NormalizeYaml(localYaml);
            var remoteContent = NormalizeYaml(remoteYaml);

            if (showRaw)
            {
                // Show inline diff
                ShowInlineDiff(localContent, remoteContent, agentName);
            }
            else
            {
                // Use external diff tool
                await LaunchDiffTool(localContent, remoteContent, agentName, diffTool, ".yaml");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Diff failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to compare agent: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Finds an agent YAML file by name, checking both subdirectory and flat structures.
    /// </summary>
    private static string? FindAgentFile(string agentName)
    {
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
            return null;

        // Check subdirectory structure first: agents/{agentName}/{agentName}.yaml
        var subdirPath = Path.Combine(agentsDir, agentName, $"{agentName}.yaml");
        if (File.Exists(subdirPath))
            return subdirPath;

        // Also check flat structure: agents/{agentName}.yaml (matches ApiService behavior)
        var flatPath = Path.Combine(agentsDir, $"{agentName}.yaml");
        if (File.Exists(flatPath))
            return flatPath;

        return null;
    }

    /// <summary>
    /// Offers to clean up local agent files after successful server deletion.
    /// </summary>
    private static void OfferLocalAgentCleanup(string agentName)
    {
        // Check both subdirectory and flat structure (matching FindAgentFile logic)
        var subdirPath = Path.Combine("agents", agentName, $"{agentName}.yaml");
        var flatPath = Path.Combine("agents", $"{agentName}.yaml");

        string? agentFile = null;
        string? agentDir = null;

        if (File.Exists(subdirPath))
        {
            agentFile = subdirPath;
            agentDir = Path.Combine("agents", agentName);
        }
        else if (File.Exists(flatPath))
        {
            agentFile = flatPath;
            agentDir = null; // For flat structure, we only delete the file, not a directory
        }

        if (agentFile == null)
        {
            return; // No local files to clean up
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Local File Cleanup");
        ConsoleUI.WriteInfo("Local configuration files still exist:", ConsoleColor.Yellow);
        ConsoleUI.WriteBullet(agentFile, ConsoleColor.Gray);
        Console.WriteLine();

        if (ConsoleUI.Confirm("Also delete local configuration files?", false))
        {
            try
            {
                if (agentDir != null && Directory.Exists(agentDir))
                {
                    // Subdirectory structure: delete entire directory
                    Directory.Delete(agentDir, true);
                    ConsoleUI.WriteStatus(true, $"Local agent files deleted: {agentDir}");
                }
                else if (File.Exists(agentFile))
                {
                    // Flat structure: delete just the file
                    File.Delete(agentFile);
                    ConsoleUI.WriteStatus(true, $"Local agent file deleted: {agentFile}");
                }

                Console.WriteLine();
                ConsoleUI.WriteSection("Summary");
                ConsoleUI.WriteBullet($"Agent '{agentName}' deleted from server", ConsoleColor.Green);
                ConsoleUI.WriteBullet("Local configuration files cleaned up", ConsoleColor.Green);

                Console.WriteLine();
                ConsoleUI.WriteInfo($"To recreate: srectl agent create --name {agentName}", ConsoleColor.Cyan);
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
            ConsoleUI.WriteBullet($"Agent '{agentName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files preserved: {agentFile}", ConsoleColor.Yellow);

            Console.WriteLine();
            ConsoleUI.WriteInfo($"To redeploy: srectl agent apply --name {agentName}", ConsoleColor.Cyan);

            var deleteCommand = agentDir != null
                ? $"rm -rf {agentDir.Replace('\\', '/')}"
                : $"rm {agentFile.Replace('\\', '/')}";
            ConsoleUI.WriteInfo($"To delete locally: {deleteCommand}", ConsoleColor.Gray);
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

    private static void ShowInlineDiff(string local, string remote, string agentName)
    {
        ConsoleUI.WriteSection($"Configuration Diff for '{agentName}'");

        var localLines = local.Split('\n');
        var remoteLines = remote.Split('\n');

        Console.WriteLine();
        ConsoleUI.WriteInfo("Legend:", ConsoleColor.Gray);
        ConsoleUI.WriteBullet("Local only (will be applied to server)", ConsoleColor.Green);
        ConsoleUI.WriteBullet("Remote only (will be replaced by local)", ConsoleColor.Red);
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"- {localLine}");
                Console.ResetColor();
            }
            else if (localLine == null && remoteLine != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
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

    private static async Task LaunchDiffTool(string localContent, string remoteContent, string agentName, string tool, string extension)
    {
        // Create temp files
        var tempDir = Path.GetTempPath();
        var localTempFile = Path.Combine(tempDir, $"{agentName}.local{extension}");
        var remoteTempFile = Path.Combine(tempDir, $"{agentName}.remote{extension}");

        try
        {
            await File.WriteAllTextAsync(localTempFile, localContent);
            await File.WriteAllTextAsync(remoteTempFile, remoteContent);

            ConsoleUI.WriteInfo($"Launching {tool} diff tool...", ConsoleColor.Cyan);

            var process = tool.ToLower() switch
            {
                "git" => LaunchGitDiff(localTempFile, remoteTempFile, agentName),
                "vimdiff" => LaunchVimDiff(localTempFile, remoteTempFile),
                "vim" => LaunchVimDiff(localTempFile, remoteTempFile),
                "code" => LaunchVSCode(localTempFile, remoteTempFile),
                "vscode" => LaunchVSCode(localTempFile, remoteTempFile),
                _ => LaunchDefaultDiff(localTempFile, remoteTempFile, agentName)
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

    private static System.Diagnostics.Process? LaunchGitDiff(string localFile, string remoteFile, string agentName)
    {
        try
        {
            // Detect if terminal supports color
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            var colorArg = isWindows ? "--color=auto" : "--color=always";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff --no-index {colorArg} \"{remoteFile}\" \"{localFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            // Add labels to make it clearer
            Console.WriteLine($"--- a/{agentName} (remote)");
            Console.WriteLine($"+++ b/{agentName} (local)");
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
            var arguments = $"\"{remoteFile}\" \"{localFile}\"";

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
                    arguments = $"-d \"{remoteFile}\" \"{localFile}\"";
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
                Arguments = $"--diff \"{remoteFile}\" \"{localFile}\"",
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

    private static System.Diagnostics.Process? LaunchDefaultDiff(string localFile, string remoteFile, string agentName)
    {
        // Try git diff first as it's most commonly available
        var process = LaunchGitDiff(localFile, remoteFile, agentName);
        if (process != null) return process;

        // Fall back to simple inline diff
        ConsoleUI.WriteInfo("No external diff tool available, showing inline diff", ConsoleColor.Yellow);
        var localContent = File.ReadAllText(localFile);
        var remoteContent = File.ReadAllText(remoteFile);
        ShowInlineDiff(localContent, remoteContent, agentName);
        return null;
    }

    #endregion

}

/// <summary>
/// CLI adapter for IToolAvailabilityChecker that wraps ToolAvailabilityService
/// </summary>
public class CliToolAvailabilityChecker : IToolAvailabilityChecker
{
    private readonly ToolAvailabilityService _toolService;

    public CliToolAvailabilityChecker(ToolAvailabilityService toolService)
    {
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
    }

    public async Task<ToolAvailabilityResult> CheckToolsAvailabilityAsync(List<string> toolNames)
    {
        var result = new ToolAvailabilityResult();

        try
        {
            var (localTools, remoteTools, errors) = await _toolService.GetAvailableToolsAsync();
            var allAvailableTools = new HashSet<string>(localTools.Union(remoteTools));

            foreach (var toolName in toolNames)
            {
                if (allAvailableTools.Contains(toolName))
                {
                    result.AvailableTools.Add(toolName);
                }
                else
                {
                    result.MissingTools.Add(toolName);
                }
            }

            // Add any errors from the tool service as warnings
            result.Warnings.AddRange(errors);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error checking tool availability: {ex.Message}");
            // Mark all tools as missing if we can't check availability
            result.MissingTools.AddRange(toolNames);
        }

        return result;
    }
}

