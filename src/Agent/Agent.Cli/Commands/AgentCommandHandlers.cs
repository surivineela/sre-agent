// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Core.Helpers.ExtendedAgents;
using Agent.Core.Validation;
using Agent.Framework;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles agent-related command operations.
/// </summary>
public static class AgentCommandHandlers
{
    /// <summary>
    /// Handles the agent create command.
    /// </summary>
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent create command");

        var name = parseResult.GetValue(AgentCommandOptions.NameOptionCreate);
        var instructions = parseResult.GetValue(AgentCommandOptions.InstructionsOptionCreate);
        var tools = parseResult.GetValue(AgentCommandOptions.ToolsOptionCreate);
        var handoffDescription = parseResult.GetValue(AgentCommandOptions.HandoffDescriptionOption);
        var handoffs = parseResult.GetValue(AgentCommandOptions.HandoffsOption);
        var allowParallelToolCalls = parseResult.GetValue(AgentCommandOptions.AllowParallelToolCallsOption);
        var maxReflectionCount = parseResult.GetValue(AgentCommandOptions.MaxReflectionCountOption);
        var criticPromptPath = parseResult.GetValue(AgentCommandOptions.CriticPromptPathOption);
        var criticOnHandoff = parseResult.GetValue(AgentCommandOptions.CriticOnHandoffOption);
        var customReflectionNote = parseResult.GetValue(AgentCommandOptions.CustomReflectionNoteOption);
        var commonPrompts = parseResult.GetValue(AgentCommandOptions.CommonPromptsOption);
        var temperature = parseResult.GetValue(AgentCommandOptions.TemperatureOption);
        var outputType = parseResult.GetValue(AgentCommandOptions.OutputTypeOption);
        var useSmart = parseResult.GetValue(AgentCommandOptions.SmartOption);

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
                "Validating configuration"
              }
            : new[] {
                "Creating agent configuration",
                "Validating configuration",
                "Writing agent files"
              };

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
                ProgressService.ShowError("Smart generation failed", new[]
                {
                    "Try running without --smart flag",
                    "Check your server connection with --debug",
                    "Verify your server supports AI completion"
                });
                Environment.Exit(1);
                return;
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
            finalTools = tools?.ToList() ?? new List<string>();
            finalMcpTools = new List<string>();
        }

        // Create YamlAgentDescriptor instance with final values
        var agent = new YamlAgentDescriptor
        {
            Name = name!,
            Instructions = finalInstructions,
            Tools = finalTools,
            McpTools = finalMcpTools,
            HandoffDescription = handoffDescription,
            Handoffs = handoffs?.ToList() ?? new List<string>(),
            AllowParallelToolCalls = allowParallelToolCalls,
            MaxReflectionCount = maxReflectionCount,
            CriticPromptPath = criticPromptPath ?? string.Empty,
            CriticOnHandOff = criticOnHandoff,
            CustomReflectionNote = customReflectionNote ?? string.Empty,
            CommonPrompts = commonPrompts?.ToList() ?? new List<string>(),
            Temperature = temperature,
            OutputType = outputType
        };

        // Validate tool existence before creating the agent
        if (finalTools.Any())
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

                    if (missingTools.Any())
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
                        if (availableToolsList.Any())
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
                        Environment.Exit(1);
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

        // Validate the agent using basic validation
        ProgressService.MultiStepProgress.NextStep("Validating agent configuration");

        var validationService = new AgentValidationService();
        var validationResult = await validationService.ValidateAgentAsync(agent, false);
        if (!validationResult.IsValid)
        {
            ProgressService.MultiStepProgress.Fail("Agent validation failed");

            // Show each validation error with red bullet points
            foreach (var error in validationResult.Errors)
            {
                ConsoleUI.WriteBullet(error, ConsoleColor.Red);
            }

            // Show warnings if any
            foreach (var warning in validationResult.Warnings)
            {
                ConsoleUI.WriteBullet($"Warning: {warning}", ConsoleColor.Yellow);
            }
            Console.WriteLine();

            Environment.Exit(1);
        }

        ProgressService.MultiStepProgress.NextStep();

        // Write the agent YAML file
        YamlHelper.WriteAgentYamlFile(Path.Combine("agents", name!), name!, agent);

        // Complete the final step
        ProgressService.MultiStepProgress.NextStep();

        ConsoleUI.WriteStatus(true, $"Agent '{name}' created successfully!");
        ConsoleUI.WriteKeyValue("Location", $"agents/{name}/{name}.yaml");

        // Show next steps
        Console.WriteLine();
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteCommand("Validate the agent", $"srectl agent validate --file agents/{name}/{name}.yaml");
        ConsoleUI.WriteCommand("Apply to server", $"srectl agent apply --name {name}");
        ConsoleUI.WriteCommand("Test the agent", $"srectl agent test --name {name} --message \"Hello\"");
        Console.WriteLine();
    }

    /// <summary>
    /// Handles the agent run command (placeholder).
    /// </summary>
    public static void HandleRunCommand(ParseResult parseResult)
    {
        ConsoleUI.WriteInfo("Not implemented yet.", ConsoleColor.Yellow);
    }

    /// <summary>
    /// Handles the agent validate command.
    /// </summary>
    public static async Task HandleValidateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent validate command");

        var validateAll = parseResult.GetValue(AgentCommandOptions.AllOption);
        var filePath = parseResult.GetValue(AgentCommandOptions.FileOptionValidate);
        var checkTools = parseResult.GetValue(AgentCommandOptions.CheckToolsOption);

        DebugLogger.Debug("Parameters", $"ValidateAll: {validateAll}, FilePath: {filePath ?? "none"}, CheckTools: {checkTools}");

        if (validateAll)
        {
            await ValidateAllAgentsAsync(checkTools);
        }
        else if (!string.IsNullOrWhiteSpace(filePath))
        {
            await ValidateSingleAgentAsync(filePath, checkTools);
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Please provide either --file <path> or --all to validate agents.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the agent apply command.
    /// </summary>
    public static async Task HandleApplyCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent apply command");

        var name = parseResult.GetValue(AgentCommandOptions.ApplyNameOption);
        var dryRun = parseResult.GetValue(AgentCommandOptions.ApplyDryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        if (dryRun)
        {
            await HandleAgentApplyDryRun(name!);
            return;
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyAgentAsync(name!);

        ConsoleUI.WriteInfo(response, success ? ConsoleColor.Green : ConsoleColor.Red);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Validates all agent YAML files in the agents directory.
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

        ToolAvailabilityService? toolService = null;
        ApiService? apiService = null;

        // Always validate configuration first to catch corrupted config files
        try
        {
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();

            if (checkTools)
            {
                if (config == null)
                {
                    ConsoleUI.WriteStatus(false, "Configuration not found. Run 'srectl init' first.");
                    Environment.Exit(1);
                    return;
                }

                // Validate URL format
                if (!Uri.TryCreate(config.ResourceUrl, UriKind.Absolute, out _))
                {
                    ConsoleUI.WriteStatus(false, $"Invalid resource URL format in configuration: {config.ResourceUrl}");
                    Environment.Exit(1);
                    return;
                }

                apiService = new ApiService();
                toolService = new ToolAvailabilityService(apiService);
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

        bool allValid = true;

        // Set up tool availability checker if needed
        IToolAvailabilityChecker? toolChecker = null;
        if (checkTools && toolService != null)
        {
            toolChecker = new CliToolAvailabilityChecker(toolService);
        }

        var validationService = new AgentValidationService(toolChecker);

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file, Encoding.UTF8);

                // Use the unified AgentValidationService
                var validationResult = await validationService.ValidateYamlAsync(yaml, checkTools);

                if (validationResult.IsValid)
                {
                    ConsoleUI.WriteStatus(true, $"{file}: Validation succeeded");
                }
                else
                {
                    allValid = false;
                    ConsoleUI.WriteStatus(false, $"{file}: Validation failed");
                    foreach (var error in validationResult.Errors)
                        ConsoleUI.WriteBullet(error, ConsoleColor.Red);

                    // Also show warnings if any
                    foreach (var warning in validationResult.Warnings)
                        ConsoleUI.WriteBullet($"Warning: {warning}", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                allValid = false;
                ConsoleUI.WriteStatus(false, $"{file}: Exception during validation: {ex.Message}");
            }
        }

        // Dispose the API service if we created one
        apiService?.Dispose();

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

        // Always validate configuration first to catch corrupted config files
        ToolAvailabilityService? toolService = null;
        ApiService? apiService = null;
        try
        {
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();

            if (checkTools)
            {
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
                toolService = new ToolAvailabilityService(apiService);
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

        try
        {
            var yaml = File.ReadAllText(filePath, Encoding.UTF8);

            // Use the unified AgentValidationService instead of AgentDescriptorValidation
            IToolAvailabilityChecker? toolChecker = null;
            if (checkTools && toolService != null)
            {
                toolChecker = new CliToolAvailabilityChecker(toolService);
            }

            var validationService = new AgentValidationService(toolChecker);
            var validationResult = await validationService.ValidateYamlAsync(yaml, checkTools);

            if (validationResult.IsValid)
            {
                ConsoleUI.WriteStatus(true, "Agent validation succeeded");
            }
            else
            {
                ConsoleUI.WriteStatus(false, "Agent validation failed");
                foreach (var error in validationResult.Errors)
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red);

                // Also show warnings if any
                foreach (var warning in validationResult.Warnings)
                    ConsoleUI.WriteBullet($"Warning: {warning}", ConsoleColor.Yellow);

                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Exception during validation: {ex.Message}");
            Environment.Exit(1);
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
    public static async Task HandleDeleteCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent delete command");

        var agentName = parseResult.GetValue(AgentCommandOptions.DeleteNameOption);

        DebugLogger.Debug("Parameters", $"AgentName: {agentName}");

        if (string.IsNullOrWhiteSpace(agentName))
        {
            ConsoleUI.WriteStatus(false, "Agent name is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();
            ConsoleUI.WriteInfo($"Deleting agent '{agentName}'...", ConsoleColor.Yellow);

            var (success, response) = await apiService.DeleteAgentAsync(agentName);

            if (success)
            {
                ConsoleUI.WriteStatus(true, response);

                // After successful server deletion, offer to clean up local files
                OfferLocalAgentCleanup(agentName);
            }
            else
            {
                ConsoleUI.WriteStatus(false, response);
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteAgent failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to delete agent: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the agent test command to test an agent with a specific message.
    /// </summary>
    public static async Task HandleTestCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent test command");

        try
        {
            var agentName = parseResult.GetValue(AgentCommandOptions.TestNameOption);
            var message = parseResult.GetValue(AgentCommandOptions.TestMessageOption);
            var userId = parseResult.GetValue(AgentCommandOptions.TestUserIdOption) ?? Environment.UserName;
            var displayName = parseResult.GetValue(AgentCommandOptions.TestDisplayNameOption) ?? Environment.UserName;
            var noWait = parseResult.GetValue(AgentCommandOptions.TestNoWaitOption);

            // Default behavior is to wait unless --no-wait is specified
            var shouldWait = !noWait;

            DebugLogger.Debug("Parameters", $"AgentName: {agentName}, Message: {message}, UserId: {userId}, Wait: {shouldWait}");

            if (string.IsNullOrWhiteSpace(agentName))
            {
                ConsoleUI.WriteStatus(false, "Agent name is required.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                ConsoleUI.WriteStatus(false, "Test message is required.");
                Environment.Exit(1);
                return;
            }

            // Construct the prefixed message
            var prefixedMessage = $"Use the {agentName} agent for the below user query\n{message}";

            ConsoleUI.WriteInfo($"Testing agent: {agentName}", ConsoleColor.Cyan);
            ConsoleUI.WriteKeyValue("Original message", message);
            ConsoleUI.WriteKeyValue("Full message", prefixedMessage);
            ConsoleUI.WriteKeyValue("User", $"{displayName} ({userId})");
            Console.WriteLine();

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            // Step 1: Create a new thread with the prefixed message
            ConsoleUI.WriteInfo("Creating new test thread...", ConsoleColor.Yellow);
            var (createSuccess, threadId, createResponse) = await apiService.CreateThreadAsync(message, userId, displayName);

            if (!createSuccess)
            {
                ConsoleUI.WriteStatus(false, createResponse);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Test thread created: {threadId}");

            // Store the thread locally
            await threadManager.AddThreadAsync(threadId, $"Agent Test: {agentName}");

            // Step 2: Wait for agent response if requested (default is true unless --no-wait)
            if (shouldWait)
            {
                ConsoleUI.WriteInfo($"Waiting for {agentName} agent response...", ConsoleColor.Yellow);
                Console.WriteLine();

                var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesStreamingAsync(threadId);

                if (!getSuccess)
                {
                    ConsoleUI.WriteStatus(false, getResponse);
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine();
                ConsoleUI.WriteStatus(true, "Test completed successfully!");
                ConsoleUI.WriteKeyValue("Thread ID", threadId);
                ConsoleUI.WriteInfo($"Continue with: srectl thread continue --thread-id {threadId}", ConsoleColor.Cyan);
            }
            else
            {
                ConsoleUI.WriteStatus(true, $"Test message sent successfully! Thread ID: {threadId}");
                ConsoleUI.WriteInfo($"Use 'srectl thread continue --thread-id {threadId}' to see the agent's response.", ConsoleColor.Cyan);
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to test agent: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles dry-run for agent apply command.
    /// </summary>
    private static async Task HandleAgentApplyDryRun(string agentName)
    {
        try
        {
            ConsoleUI.WriteInfo($"DRY RUN: Agent apply for '{agentName}'", ConsoleColor.Cyan);
            ConsoleUI.WriteInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", ConsoleColor.DarkGray);

            // Find and validate agent file exists
            var agentFilePath = FindAgentFile(agentName);
            if (agentFilePath == null)
            {
                ConsoleUI.WriteStatus(false, $"Agent file not found for '{agentName}'");
                ConsoleUI.WriteInfo($"Expected: agents/{agentName}/{agentName}.yaml", ConsoleColor.Gray);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteKeyValue("Agent file", agentFilePath);

            // Read and parse the YAML file
            var yamlContent = await File.ReadAllTextAsync(agentFilePath);
            ConsoleUI.WriteKeyValue("Content size", $"{yamlContent.Length} characters");

            // Validate using unified AgentValidationService
            var validationService = new AgentValidationService();
            var validationResult = await validationService.ValidateYamlAsync(yamlContent, false);

            if (!validationResult.IsValid)
            {
                ConsoleUI.WriteStatus(false, "Agent validation failed");
                foreach (var error in validationResult.Errors)
                    ConsoleUI.WriteBullet(error, ConsoleColor.Red);

                foreach (var warning in validationResult.Warnings)
                    ConsoleUI.WriteBullet($"Warning: {warning}", ConsoleColor.Yellow);

                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, "Agent validation successful");

            // Parse for display purposes
            try
            {
                var agent = AgentYamlParser.ParseAgentYaml(yamlContent);
                if (agent != null)
                {
                    Console.WriteLine();
                    ConsoleUI.WriteSection("Agent Details");
                    ConsoleUI.WriteKeyValue("Name", agent.Name);
                    ConsoleUI.WriteKeyValue("Instructions length", $"{agent.Instructions?.Length ?? 0} characters");

                    if (agent.Tools?.Count > 0)
                    {
                        ConsoleUI.WriteKeyValue("Tools", $"({agent.Tools.Count}): {string.Join(", ", agent.Tools)}");

                        // Check if referenced tools exist locally
                        var missingTools = new List<string>();
                        foreach (var tool in agent.Tools)
                        {
                            var toolPath = ToolCommandHandlers.FindToolFile(tool);
                            if (toolPath == null)
                            {
                                missingTools.Add(tool);
                            }
                        }

                        if (missingTools.Any())
                        {
                            ConsoleUI.WriteInfo($"Missing local tool files: {string.Join(", ", missingTools)}", ConsoleColor.Yellow);
                        }
                        else
                        {
                            ConsoleUI.WriteInfo("All referenced tools found locally", ConsoleColor.Green);
                        }
                    }

                    if (agent.Handoffs?.Count > 0)
                    {
                        ConsoleUI.WriteKeyValue("Handoffs", $"({agent.Handoffs.Count}): {string.Join(", ", agent.Handoffs)}");
                    }

                    if (agent.Temperature.HasValue)
                        ConsoleUI.WriteKeyValue("Temperature", agent.Temperature.Value.ToString());

                    ConsoleUI.WriteKeyValue("Max reflection count", agent.MaxReflectionCount.ToString());
                }
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteInfo($"Could not parse agent details for display: {ex.Message}", ConsoleColor.Yellow);
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
            ConsoleUI.WriteBullet($"Agent '{agentName}' configuration is valid", ConsoleColor.Green);
            ConsoleUI.WriteBullet("YAML file can be parsed successfully", ConsoleColor.Green);
            ConsoleUI.WriteBullet("Server configuration is available", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Would apply to: {config.ResourceUrl}", ConsoleColor.Green);
            Console.WriteLine();
            ConsoleUI.WriteCommand("To apply", $"srectl agent apply --name {agentName}");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"DRY RUN FAILED: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the agent diff command to compare local and remote configurations.
    /// </summary>
    public static async Task HandleDiffCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting agent diff command");

        var agentName = parseResult.GetValue(AgentCommandOptions.DiffNameOption);
        var diffTool = parseResult.GetValue(AgentCommandOptions.DiffToolOption) ?? "git";
        var showRaw = parseResult.GetValue(AgentCommandOptions.DiffRawOption);

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
                ConsoleUI.WriteInfo($"Expected: agents/{agentName}/{agentName}.yaml", ConsoleColor.Gray);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteInfo($"Comparing agent '{agentName}'...", ConsoleColor.Cyan);

            // Get remote configuration
            using var apiService = new ApiService();
            var (success, remoteYaml, errorMessage) = await apiService.GetAgentConfigurationAsync(agentName);

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
    /// Finds an agent YAML file by name.
    /// </summary>
    private static string? FindAgentFile(string agentName)
    {
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
            return null;

        var expectedPath = Path.Combine(agentsDir, agentName, $"{agentName}.yaml");
        return File.Exists(expectedPath) ? expectedPath : null;
    }

    /// <summary>
    /// Offers to clean up local agent files after successful server deletion.
    /// </summary>
    private static void OfferLocalAgentCleanup(string agentName)
    {
        var agentDir = Path.Combine("agents", agentName);
        var agentFile = Path.Combine(agentDir, $"{agentName}.yaml");

        if (!File.Exists(agentFile))
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
                if (Directory.Exists(agentDir))
                {
                    Directory.Delete(agentDir, true);
                    ConsoleUI.WriteStatus(true, $"Local agent files deleted: {agentDir}");
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
            ConsoleUI.WriteInfo($"To delete locally: rm -rf {agentDir.Replace('\\', '/')}", ConsoleColor.Gray);
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
                Arguments = $"diff --no-index {colorArg} \"{localFile}\" \"{remoteFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            // Add labels to make it clearer
            Console.WriteLine($"--- a/{agentName} (local)");
            Console.WriteLine($"+++ b/{agentName} (remote)");
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
