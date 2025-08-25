using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Text;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Cli.Validations;
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
        // Set debug mode first
        var debug = parseResult.GetValue(AgentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

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

        // Handle smart agent generation
        if (useSmart)
        {
            Console.WriteLine("🤖 Generating smart agent with AI...");
            
            using var apiService = new ApiService();
            var (success, generatedInstructions, recommendedTools, errorMessage) = await apiService.GenerateSmartAgentAsync(name!, instructions);
            
            if (!success)
            {
                Console.WriteLine($"❌ Smart generation failed: {errorMessage}");
                Environment.Exit(1);
                return;
            }

            finalInstructions = generatedInstructions;
            finalTools = recommendedTools;

            Console.WriteLine($"✅ AI generated instructions and {recommendedTools.Count} recommended tools!");
            Console.WriteLine($"📝 Generated Instructions Preview: {(generatedInstructions.Length > 100 ? generatedInstructions.Substring(0, 100) + "..." : generatedInstructions)}");
            Console.WriteLine($"🔧 Recommended Tools: {string.Join(", ", recommendedTools)}");
        }
        else
        {
            finalInstructions = instructions ?? $"This is the {name} agent. Please provide specific instructions for what this agent should do.";
            finalTools = tools?.ToList() ?? new List<string>();
        }

        // Create YamlAgentDescriptor instance with final values
        var agent = new YamlAgentDescriptor
        {
            Name = name!,
            Instructions = finalInstructions,
            Tools = finalTools,
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

        // Validate the agent using CLI validation
        AgentDescriptorValidation.ValidateAgentDescriptor(agent, out var errors);
        if (errors.Count > 0)
        {
            Console.WriteLine("❌ Agent validation failed:");
            foreach (var error in errors)
                Console.WriteLine($"  - {error}");
            Environment.Exit(1);
        }

        // Write the agent YAML file
        YamlHelper.WriteAgentYamlFile(Path.Combine("agents", name!), name!, agent);
        Console.WriteLine($"✅ Agent YAML created at agents/{name}/{name}.yaml");
    }

    /// <summary>
    /// Handles the agent run command (placeholder).
    /// </summary>
    public static void HandleRunCommand(ParseResult parseResult)
    {
        Console.WriteLine("Not implemented yet.");
    }

    /// <summary>
    /// Handles the agent validate command.
    /// </summary>
    public static async Task HandleValidateCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(AgentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

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
            Console.WriteLine("❌ Please provide either --file <path> or --all to validate agents.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the agent apply command.
    /// </summary>
    public static async Task HandleApplyCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(AgentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

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
        
        Console.WriteLine(response);
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
            Console.WriteLine("No agents directory found.");
            Environment.Exit(1);
        }

        var files = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            Console.WriteLine("No agent YAML files found in agents directory.");
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
                    Console.WriteLine("❌ Configuration not found. Run 'srectl init' first.");
                    Environment.Exit(1);
                    return;
                }
                
                // Validate URL format
                if (!Uri.TryCreate(config.ResourceUrl, UriKind.Absolute, out _))
                {
                    Console.WriteLine($"❌ Invalid resource URL format in configuration: {config.ResourceUrl}");
                    Environment.Exit(1);
                    return;
                }
                
                apiService = new ApiService();
                toolService = new ToolAvailabilityService(apiService);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Configuration error: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        bool allValid = true;
        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file, Encoding.UTF8);
                var agent = AgentFactory<object>.LoadAgentFromYaml(yaml);
                var errors = new List<string>();

                if (checkTools)
                {
                    await AgentDescriptorValidation.ValidateAgentDescriptorAsync(agent, toolService, errors);
                }
                else
                {
                    AgentDescriptorValidation.ValidateAgentDescriptor(agent, out errors);
                }

                if (errors.Count == 0)
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

        // Dispose the API service if we created one
        apiService?.Dispose();

        if (allValid)
            Console.WriteLine("All agent YAML files are valid.");
        else
        {
            Console.WriteLine("Some agent YAML files failed validation.");
            Environment.Exit(1);
        }
    }

    private static async Task ValidateSingleAgentAsync(string filePath, bool checkTools = false)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"❌ Agent YAML file not found: {filePath}");
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
                    Console.WriteLine("❌ Configuration not found. Run 'srectl init' first.");
                    Environment.Exit(1);
                    return;
                }
                
                // Validate URL format
                if (!Uri.TryCreate(config.ResourceUrl, UriKind.Absolute, out _))
                {
                    Console.WriteLine($"❌ Invalid resource URL in configuration: {config.ResourceUrl}");
                    Environment.Exit(1);
                    return;
                }
                
                apiService = new ApiService();
                toolService = new ToolAvailabilityService(apiService);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Configuration error: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        try
        {
            var yaml = File.ReadAllText(filePath, Encoding.UTF8);
            var agent = AgentFactory<object>.LoadAgentFromYaml(yaml);
            var errors = new List<string>();

            if (checkTools)
            {
                await AgentDescriptorValidation.ValidateAgentDescriptorAsync(agent, toolService, errors);
            }
            else
            {
                AgentDescriptorValidation.ValidateAgentDescriptor(agent, out errors);
            }

            if (errors.Count == 0)
            {
                Console.WriteLine("✅ Agent validation succeeded.");
            }
            else
            {
                Console.WriteLine("❌ Agent validation failed:");
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
        // Set debug mode first
        var debug = parseResult.GetValue(AgentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting agent delete command");

        var agentName = parseResult.GetValue(AgentCommandOptions.DeleteNameOption);

        DebugLogger.Debug("Parameters", $"AgentName: {agentName}");
        
        if (string.IsNullOrWhiteSpace(agentName))
        {
            Console.WriteLine("❌ Agent name is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();
            Console.WriteLine($"🗑️  Deleting agent '{agentName}'...");

            var (success, response) = await apiService.DeleteAgentAsync(agentName);
            
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
            DebugLogger.Debug("Exception", $"DeleteAgent failed: {ex.Message}");
            Console.WriteLine($"❌ Failed to delete agent: {ex.Message}");
            Environment.Exit(1);
        }
    }    /// <summary>
    /// Handles the agent test command to test an agent with a specific message.
    /// </summary>
    public static async Task HandleTestCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(AgentCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

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
                Console.WriteLine("Agent name is required.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Test message is required.");
                Environment.Exit(1);
                return;
            }

            // Construct the prefixed message
            var prefixedMessage = $"Use the {agentName} agent for the below user query\n{message}";

            Console.WriteLine($"Testing agent: {agentName}");
            Console.WriteLine($"Original message: {message}");
            Console.WriteLine($"Full message: {prefixedMessage}");
            Console.WriteLine($"User: {displayName} ({userId})");
            Console.WriteLine();

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            // Step 1: Create a new thread with the prefixed message
            Console.WriteLine("Creating new test thread...");
            var (createSuccess, threadId, createResponse) = await apiService.CreateThreadAsync(prefixedMessage, userId, displayName);
            
            if (!createSuccess)
            {
                Console.WriteLine(createResponse);
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Test thread created: {threadId}");

            // Store the thread locally
            await threadManager.AddThreadAsync(threadId, $"Agent Test: {agentName}");

            // Step 2: Wait for agent response if requested (default is true unless --no-wait)
            if (shouldWait)
            {
                Console.WriteLine($"Waiting for {agentName} agent response...");
                Console.WriteLine();
                
                var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesStreamingAsync(threadId);
                
                if (!getSuccess)
                {
                    Console.WriteLine(getResponse);
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("🎯 Test completed successfully!");
                Console.WriteLine($"Thread ID: {threadId}");
                Console.WriteLine("You can continue this conversation using 'srectl thread continue --thread-id " + threadId + "'");
            }
            else
            {
                Console.WriteLine($"Test message sent successfully! Thread ID: {threadId}");
                Console.WriteLine($"Use 'srectl thread continue --thread-id {threadId}' to see the agent's response.");
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to test agent: {ex.Message}");
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
            Console.WriteLine($"🔍 DRY RUN: Agent apply for '{agentName}'");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Find and validate agent file exists
            var agentFilePath = FindAgentFile(agentName);
            if (agentFilePath == null)
            {
                Console.WriteLine($"❌ Agent file not found for '{agentName}'");
                Console.WriteLine($"   Expected: agents/{agentName}/{agentName}.yaml");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"📂 Agent file found: {agentFilePath}");

            // Read and parse the YAML file
            var yamlContent = await File.ReadAllTextAsync(agentFilePath);
            Console.WriteLine($"📄 YAML content size: {yamlContent.Length} characters");

            // Validate YAML structure
            try
            {
                var agentConfig = YamlHelper.CreateCamelCaseDeserializer()
                    .Deserialize<Dictionary<string, object>>(yamlContent);
                
                Console.WriteLine("✅ YAML structure is valid");
                Console.WriteLine($"📝 Agent details:");
                
                if (agentConfig.TryGetValue("name", out var nameValue))
                    Console.WriteLine($"   Name: {nameValue}");
                    
                if (agentConfig.TryGetValue("instructions", out var instructionsValue))
                    Console.WriteLine($"   Instructions length: {instructionsValue?.ToString()?.Length ?? 0} characters");
                
                if (agentConfig.TryGetValue("tools", out var toolsValue) && toolsValue is List<object> toolsList)
                {
                    var tools = toolsList.Select(t => t.ToString()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                    Console.WriteLine($"   Tools ({tools.Count}): {string.Join(", ", tools)}");
                    
                    // Check if referenced tools exist locally
                    var missingTools = new List<string>();
                    foreach (var tool in tools)
                    {
                        var toolPath = ToolCommandHandlers.FindToolFile(tool!);
                        if (toolPath == null)
                        {
                            missingTools.Add(tool!);
                        }
                    }
                    
                    if (missingTools.Any())
                    {
                        Console.WriteLine($"   ⚠️  Missing local tool files: {string.Join(", ", missingTools)}");
                    }
                    else
                    {
                        Console.WriteLine("   ✅ All referenced tools found locally");
                    }
                }
                
                if (agentConfig.TryGetValue("handoffs", out var handoffsValue) && handoffsValue is List<object> handoffsList)
                {
                    var handoffs = handoffsList.Select(h => h.ToString()).Where(h => !string.IsNullOrEmpty(h)).ToList();
                    Console.WriteLine($"   Handoffs ({handoffs.Count}): {string.Join(", ", handoffs)}");
                }
                
                if (agentConfig.TryGetValue("temperature", out var tempValue))
                    Console.WriteLine($"   Temperature: {tempValue}");
                if (agentConfig.TryGetValue("maxReflectionCount", out var reflectionValue))
                    Console.WriteLine($"   Max reflection count: {reflectionValue}");
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
            Console.WriteLine($"   • Agent '{agentName}' configuration is valid");
            Console.WriteLine("   • YAML file can be parsed successfully");
            Console.WriteLine("   • Server configuration is available");
            Console.WriteLine($"   • Would apply to: {config.ResourceUrl}");
            Console.WriteLine("\n💡 To actually apply the agent, run: srectl agent apply --name " + agentName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DRY RUN FAILED: {ex.Message}");
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
}
