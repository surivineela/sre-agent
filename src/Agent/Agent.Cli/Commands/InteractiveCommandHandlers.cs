// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Framework;

namespace Agent.Cli.Commands;

public static class InteractiveCommandHandlers
{
    private const string AzCliPresetToken = "__PRESET_AZCLI__";
    private const string AksPresetToken = "__PRESET_AKS__";
    private const string IcmKustoPresetToken = "__PRESET_ICM_KUSTO__";

    public static async Task HandleInteractiveMode(ParseResult parseResult)
    {
        Console.WriteLine();
        ConsoleUI.DrawPanel("Interactive SRE Agent CLI Guide", "I'll help you get started with srectl", ConsoleColor.Cyan);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            ConsoleUI.WriteInfo("Interactive mode terminated.");
        };

        ConsoleUI.WriteInfo("Welcome to the interactive mode!");
        ConsoleUI.WriteInfo("You can exit anytime by typing 'quit', 'exit', pressing 'Esc' or Ctrl+C.");
        Console.WriteLine();
        ConsoleUI.DrawLine();

        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        if (!hasConfig)
        {
            await GuideFirstTimeSetup();
            return;
        }

        await ShowMainMenu();
    }

    private static async Task GuideFirstTimeSetup()
    {
        while (true)
        {
            ConsoleUI.WriteInfo("I notice this is your first time using srectl!");
            ConsoleUI.WriteInfo("Let's get you set up step by step.");
            Console.WriteLine();

            ConsoleUI.WriteInline("What's your SRE Agent server URL? (e.g., https://localhost:7023): ");
            var serverUrl = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(serverUrl))
            {
                ConsoleUI.WriteStatus(false, "Server URL is required. Let's try again.");
                continue; // loop back instead of recursion
            }

            Console.WriteLine();
            ConsoleUI.WriteInfo($"Setting up workspace with server: {serverUrl}");

            var steps = new[]
            {
                "Validating server URL format",
                "Creating workspace directories",
                "Testing server connection",
                "Creating example files"
            };

            ProgressService.MultiStepProgress.Initialize(steps);

            try
            {
                // Step 1: validate URL (lightweight)
                ProgressService.MultiStepProgress.NextStep();

                // Step 2: create workspace
                ProgressService.MultiStepProgress.NextStep();

                // Step 3+4: call your init handler (should do connection test + sample files)
                await GeneralCommandHandlers.HandleInitCommandWithResourceUrl(serverUrl);
                ProgressService.MultiStepProgress.NextStep(); // connection tested
                ProgressService.MultiStepProgress.NextStep(); // examples created

                Console.WriteLine();
                ConsoleUI.WriteStatus(true, "Great! Your workspace is set up.");
                ConsoleUI.WriteInfo("Now let's create your first agent.");
                Console.WriteLine();

                await GuideAgentCreation();
                return;
            }
            catch (Exception ex)
            {
                ProgressService.MultiStepProgress.Fail($"Setup failed: {ex.Message}");
                ProgressService.ShowError("Setup failed",
                [
                    "Check if the server URL is correct",
                    "Ensure the server is running and accessible",
                    "Try again with a different URL"
                ]);
                // Loop back to ask again
            }
        }
    }

    private static async Task GuideAgentCreation()
    {
        ConsoleUI.WriteInfo("Let's create your first SRE agent!");
        ConsoleUI.WriteInfo("Agents are intelligent assistants that help with specific tasks.");
        Console.WriteLine();

        ConsoleUI.WriteInline("What would you like to name your agent? ");
        var agentName = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(agentName))
        {
            ConsoleUI.WriteStatus(false, "Agent name is required.");
            await GuideAgentCreation();
            return;
        }

        // Validate agent name format (same validation as regular create command)
        if (agentName.Any(char.IsWhiteSpace))
        {
            ConsoleUI.WriteStatus(false, $"Agent name '{agentName}' must not contain spaces or whitespace characters.");
            ConsoleUI.WriteInfo("Agent names must be single words without spaces (e.g., 'MyAgent', 'incident-responder', 'log_analyzer')");
            await GuideAgentCreation();
            return;
        }

        // Check if agent already exists
        var agentPath = Path.Combine("agents", agentName);
        if (Directory.Exists(agentPath))
        {
            ConsoleUI.WriteStatus(false, $"Agent '{agentName}' already exists.");
            if (!ConsoleUI.Confirm("Would you like to choose a different name?", true))
            {
                return;
            }
            await GuideAgentCreation();
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("What should this agent help with? Choose an option");
        ConsoleUI.WriteKeyValue("1", "Custom Agent * (best first experience – describe what you want)", 3);
        ConsoleUI.WriteKeyValue("2", "Incident Response (ICM + Kusto investigator)", 3);
        ConsoleUI.WriteKeyValue("3", "Kubernetes Operations (AKS preset)", 3);
        ConsoleUI.WriteKeyValue("4", "Azure CLI Operations (AzCLI preset)", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-4): ");

        var choice = Console.ReadLine()?.Trim();
        string? selectedCategory = choice switch
        {
            "1" => "Custom Agent",
            "2" => "Incident Response",
            "3" => "Kubernetes Operations",
            "4" => "Azure CLI Operations",
            _ => null
        };

        var instructions = selectedCategory != null ? await GetCustomInstructions(selectedCategory) : null;

        if (instructions == null)
        {
            ConsoleUI.WriteStatus(false, "Please select a valid option (1-4).");
            await GuideAgentCreation();
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteInfo($"Creating agent '{agentName}' with AI assistance...");

        try
        {
            ConsoleUI.WriteCommand("Running command", $"srectl agent create --name {agentName}");
            Console.WriteLine();

            // Create the agent directly (support presets and category-aware defaults)
            if (instructions == AzCliPresetToken)
            {
                await CreateAndRunAzCliPresetAgent(agentName);
                ConsoleUI.WriteInfo("Heads-up: Ensure this agent identity has access to target Azure resources (Reader/Contributor as needed).", ConsoleColor.Yellow);
            }
            else if (instructions == AksPresetToken)
            {
                await CreateAndRunAksRemediationPresetAgent(agentName);
                ConsoleUI.WriteInfo("Heads-up: Ensure this agent identity has access to the AKS cluster (kubeconfig/RBAC).", ConsoleColor.Yellow);
            }
            else if (instructions == IcmKustoPresetToken)
            {
                await CreateAndApplyIcmKustoAgent(agentName);
                ConsoleUI.WriteInfo("Note: Configure the ICM handler and grant this agent identity access to Kusto clusters used by the tools.", ConsoleColor.Yellow);
            }
            else if (string.Equals(selectedCategory, "Azure CLI Operations", StringComparison.OrdinalIgnoreCase))
            {
                await CreateAgentWithAzCliTemplate(agentName, instructions);
                ConsoleUI.WriteInfo("Heads-up: Ensure this agent identity has access to target Azure resources (Reader/Contributor as needed).", ConsoleColor.Yellow);
            }
            else if (string.Equals(selectedCategory, "Kubernetes Operations", StringComparison.OrdinalIgnoreCase))
            {
                await CreateAgentWithAksTemplate(agentName, instructions);
                ConsoleUI.WriteInfo("Heads-up: Ensure this agent identity has appropriate Kubernetes RBAC for the target cluster.", ConsoleColor.Yellow);
            }
            else
            {
                await CreateAndRunAgentCommand(agentName, instructions, useSmart: true);
            }

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Agent '{agentName}' created successfully!");
            ConsoleUI.WriteInfo("Next, let's deploy it to your server and test it.");

            if (ConsoleUI.Confirm("Would you like to deploy and test this agent now?", true))
            {
                await GuideAgentDeployment(agentName);
            }
            else
            {
                ConsoleUI.WriteInfo("You can deploy later with: srectl agent apply --name " + agentName);
                await ShowMainMenu();
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Agent creation failed: {ex.Message}",
            [
                "Try creating a simpler agent first",
                "Check your server connection",
                "Use 'srectl help troubleshooting' for more help"
            ]);

            if (ConsoleUI.Confirm("Would you like to try again?", true))
            {
                await GuideAgentCreation();
            }
        }
    }

    private static async Task<string> GetCustomInstructions(string category)
    {
        Console.WriteLine();
        ConsoleUI.WriteSection($"Choose a sample {category} agent or enter your own");

        // Category-specific sample prompts
        var samples = category switch
        {
            "Custom Agent" => new[]
            {
                "[Starter *] You are a friendly Hello World agent who greets users with enthusiasm and warmth. Your primary role is to welcome users to the SRE Agent platform and help them feel comfortable getting started. Keep it concise and helpful. At the end of every response, include exactly one inspirational quote about SREs fixing broken production, then stop and wait for the user to continue—do not ask follow-up questions or add anything after the quote.",
                "You are a friendly Log Analyzer agent with expertise in analyzing application logs and identifying patterns, errors, and performance issues. You excel at parsing through complex log files, correlating events across multiple systems, and presenting findings in clear, actionable insights. Your approach is methodical yet approachable - you break down complex technical issues into understandable explanations while providing specific recommendations for remediation and monitoring improvements.",
                "You are a friendly Alert Responder agent specializing in triaging alerts, escalating critical issues, and coordinating incident response activities. You maintain calm under pressure and excel at quickly assessing alert severity, gathering relevant context, and determining appropriate response actions. Your communication style is clear and decisive during incidents while being supportive and collaborative during post-incident reviews and process improvements.",
                "You are a friendly Performance Monitor agent focused on system performance analysis, metrics interpretation, and optimization suggestions. You have a keen eye for identifying performance bottlenecks, resource utilization patterns, and capacity planning opportunities. Your recommendations are always data-driven and practical, helping teams understand not just what is happening but why it matters and how to improve it systematically.",
                "You are a friendly Documentation Helper agent who creates and maintains technical documentation, runbooks, and troubleshooting guides. You excel at transforming complex technical processes into clear, step-by-step documentation that teams can easily follow. Your writing style is concise yet comprehensive, and you always consider the end-user experience when organizing information and creating helpful examples and diagrams."
            },
            "Incident Response" =>
            [
                "Use ICM + Kusto Frontend Investigator preset (recommended)",
                "You are a friendly ICM Alert Triager agent who specializes in analyzing ICM incidents with precision and care. You expertly run Kusto queries to gather comprehensive diagnostics, correlate incident data across multiple systems, and prioritize alerts based on severity and business impact. Your approach combines technical expertise with clear communication, helping teams understand incident scope and recommended actions. You remain calm under pressure and provide structured, actionable guidance during critical situations."
            ],
            "Kubernetes Operations" =>
            [
                "Use AKS Remediation Agent preset (recommended)"
            ],
            "Azure CLI Operations" =>
            [
                "Use Azure CLI Command Executor preset (recommended)"
            ],
            _ => ["Default agent description"]
        };

        for (int i = 0; i < samples.Length; i++)
        {
            ConsoleUI.WriteKeyValue($"{i + 1}", samples[i], 3, ConsoleColor.Yellow);
        }
        ConsoleUI.WriteKeyValue($"{samples.Length + 1}", "Enter my own custom description", 3, ConsoleColor.Cyan);

        Console.WriteLine();
        ConsoleUI.WriteInline($"Select an option (1-{samples.Length + 1}): ");
        var choice = Console.ReadLine()?.Trim();

        // Handle choice
        if (int.TryParse(choice, out int index))
        {
            if (index >= 1 && index <= samples.Length)
            {
                // Special-case: Azure CLI preset as first option
                if (string.Equals(category, "Azure CLI Operations", StringComparison.OrdinalIgnoreCase) && index == 1)
                {
                    return AzCliPresetToken;
                }
                // Special-case: AKS preset as first option in Kubernetes
                if (string.Equals(category, "Kubernetes Operations", StringComparison.OrdinalIgnoreCase) && index == 1)
                {
                    return AksPresetToken;
                }
                // Special-case: ICM+Kusto preset as first option in Incident Response
                if (string.Equals(category, "Incident Response", StringComparison.OrdinalIgnoreCase) && index == 1)
                {
                    return IcmKustoPresetToken;
                }
                return samples[index - 1];
            }
            else if (index == samples.Length + 1)
            {
                return await GetCustomDescription();
            }
        }

        // Invalid choice, try again
        ConsoleUI.WriteStatus(false, $"Please select a valid option (1-{samples.Length + 1}).");
        return await GetCustomInstructions(category);
    }

    private static async Task<string> GetCustomDescription()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Enter your custom agent description");
        ConsoleUI.WriteInfo("Describe what you want your agent to help with and how it should behave");
        Console.WriteLine();
        ConsoleUI.WriteInline("   > ");
        var customInstructions = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(customInstructions))
        {
            ConsoleUI.WriteStatus(false, "Please provide some instructions for your agent.");
            return await GetCustomDescription();
        }

        return customInstructions;
    }

    private static async Task ShowMainMenu()
    {
        while (true)
        {
            await ShowContextualMenu();

            var input = ReadInputWithEscapeHandling();

            // Handle exit commands
            if (input == null || IsExitCommand(input))
            {
                Console.WriteLine();
                ConsoleUI.WriteInfo("Thanks for using srectl! You can restart with 'srectl interactive'");
                return;
            }

            if (await HandleMenuChoice(input))
                return; // Exit if user chose to exit
        }
    }

    private static string? ReadInputWithEscapeHandling()
    {
        var input = new StringBuilder();
        ConsoleKeyInfo keyInfo;

        do
        {
            keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape:
                    return null; // Signal exit
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return input.ToString().Trim();
                case ConsoleKey.Backspace:
                    if (input.Length > 0)
                    {
                        input.Remove(input.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    break;
                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        input.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar);
                    }
                    break;
            }
        } while (keyInfo.Key != ConsoleKey.Enter);

        return input.ToString().Trim();
    }

    private static bool IsExitCommand(string input)
    {
        var exitCommands = new[] { "quit", "exit", "q", "bye" };
        return exitCommands.Contains(input.ToLowerInvariant());
    }

    private static async Task ShowContextualMenu()
    {
        Console.WriteLine();

        // Show contextual information first
        await ShowWorkspaceContext();

        Console.WriteLine();
        ConsoleUI.WriteSection("SRE Agent Workflow");

        // Show workflow guidance
        await ShowWorkflowGuidance();

        Console.WriteLine();
        ConsoleUI.WriteSection("Available Actions");

        // Workflow-based actions
        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        if (!hasConfig)
        {
            ConsoleUI.WriteKeyValue("1", "Initialize workspace (required first)", 3);
        }
        else
        {
            ConsoleUI.WriteKeyValue("1", "Create a new agent", 3);
            ConsoleUI.WriteKeyValue("2", "Create a new tool (optional)", 3);
            ConsoleUI.WriteKeyValue("3", "Create a scheduled task (optional)", 3);

            // Show contextual quick actions for apply/deploy
            await ShowApplyActions();

            ConsoleUI.WriteKeyValue("7", "Start chat with agents", 3);
        }

        // Always available options
        ConsoleUI.WriteKeyValue("8", "Check workspace status", 3);
        ConsoleUI.WriteKeyValue("9", "Get help and examples", 3);
        ConsoleUI.WriteKeyValue("0", "Exit (or type 'quit', press Esc)", 3);

        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option: ");
    }

    private static async Task ShowWorkflowGuidance()
    {
        try
        {
            var configService = new CliConfigurationService();
            var hasConfig = await configService.HasValidConfigurationAsync();

            var agentsDir = "agents";
            var agentCount = Directory.Exists(agentsDir) ? Directory.GetDirectories(agentsDir).Length : 0;

            if (!hasConfig)
            {
                ConsoleUI.WriteInfo("Getting started: Initialize → Create Agent → Apply → Chat");
                ConsoleUI.WriteStatus(false, "Step 1: Initialize workspace first");
            }
            else if (agentCount == 0)
            {
                ConsoleUI.WriteInfo("Next steps: Create Agent → Apply → Chat");
                ConsoleUI.WriteStatus(true, "Step 2: Create your first agent");
            }
            else
            {
                var hasDeployedAgents = await CheckForDeployedAgents();
                if (!hasDeployedAgents)
                {
                    ConsoleUI.WriteInfo("Almost ready: Apply Changes → Chat");
                    ConsoleUI.WriteStatus(true, "Step 3: Deploy your agents to server");
                }
                else
                {
                    ConsoleUI.WriteInfo("Ready to go: Chat with your deployed agents");
                    ConsoleUI.WriteStatus(true, "Step 4: Start chatting or create more agents");
                }
            }
        }
        catch
        {
            // Silently handle errors
        }
    }

    private static Task<bool> CheckForDeployedAgents()
    {
        // Simplified check - in real implementation, this would query the server
        // For now, assume agents are deployed if they exist locally
        return Task.FromResult(true);
    }

    private static Task ShowApplyActions()
    {
        try
        {
            var agentsDir = "agents";
            var toolsDir = "tools";

            var agentDirs = Directory.Exists(agentsDir) ?
                Directory.GetDirectories(agentsDir).Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).ToArray() :
                [];

            var toolDirs = Directory.Exists(toolsDir) ?
                Directory.GetDirectories(toolsDir).Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).ToArray() :
                [];

            if (agentDirs.Length > 0 || toolDirs.Length > 0)
            {
                ConsoleUI.WriteKeyValue("4", "Apply changes (deploy to server)", 3);

                // Show quick deploy options for first few agents
                var displayCount = Math.Min(2, agentDirs.Length);
                for (int i = 0; i < displayCount; i++)
                {
                    ConsoleUI.WriteKeyValue($"{i + 5}", $"Quick deploy '{agentDirs[i]}'", 3);
                }
            }
        }
        catch
        {
            // Silently handle errors
        }
        return Task.CompletedTask;
    }

    private static async Task ShowWorkspaceContext()
    {
        try
        {
            // Quick workspace overview
            var agentsDir = "agents";
            var toolsDir = "tools";

            var agentCount = Directory.Exists(agentsDir) ? Directory.GetDirectories(agentsDir).Length : 0;
            var toolCount = Directory.Exists(toolsDir) ? Directory.GetDirectories(toolsDir).Length : 0;

            Console.WriteLine();
            ConsoleUI.WriteSection("Current Workspace");
            ConsoleUI.WriteKeyValue("Agents", $"{agentCount} configured", 15);
            ConsoleUI.WriteKeyValue("Tools", $"{toolCount} available", 15);

            // Show quick status
            var configService = new CliConfigurationService();
            var hasConfig = await configService.HasValidConfigurationAsync();
            var statusText = hasConfig ? "Connected" : "Not configured";
            ConsoleUI.WriteKeyValue("Server", statusText, 15);
        }
        catch
        {
            // Silently handle any errors in context display
        }
    }

    private static async Task<bool> HandleMenuChoice(string choice)
    {
        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        switch (choice)
        {
            case "1":
                if (!hasConfig)
                {
                    await GuideWorkspaceInitialization();
                }
                else
                {
                    await GuideAgentCreation();
                }
                break;
            case "2":
                if (hasConfig)
                {
                    await GuideToolCreation();
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Please initialize workspace first (option 1)");
                }
                break;
            case "3":
                if (hasConfig)
                {
                    await GuideScheduledTaskCreation();
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Please initialize workspace first (option 1)");
                }
                break;
            case "4":
                if (hasConfig)
                {
                    await GuideApplyChanges();
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Please initialize workspace first (option 1)");
                }
                break;
            case "5":
            case "6":
                // Handle quick deployment actions
                await HandleQuickDeployment(choice);
                break;
            case "7":
                if (hasConfig)
                {
                    await StartInteractiveChat();
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Please initialize workspace first (option 1)");
                }
                break;
            case "8":
                await ShowWorkspaceStatus();
                break;
            case "9":
                await InteractiveHelpService.ShowInteractiveHelp();
                break;
            case "0":
                Console.WriteLine();
                ConsoleUI.WriteInfo("Thanks for using srectl! You can return to interactive mode anytime with 'srectl interactive'");
                return true; // Signal to exit
            case "":
                // Handle empty input - just show menu again
                break;
            default:
                ConsoleUI.WriteStatus(false, "Please select a valid option.");
                break;
        }
        return false; // Continue showing menu
    }

    private static async Task GuideWorkspaceInitialization()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Initialize SRE Agent Workspace");
        ConsoleUI.WriteInfo("Let's set up your workspace to connect to the SRE Agent server.");
        Console.WriteLine();

        ConsoleUI.WriteInline("Enter your SRE Agent server URL (e.g., https://localhost:7023): ");
        var serverUrl = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(serverUrl))
        {
            ConsoleUI.WriteStatus(false, "Server URL is required to initialize workspace.");
            return;
        }

        try
        {
            ConsoleUI.WriteInfo($"Initializing workspace with server: {serverUrl}");
            Console.WriteLine();

            // Call the actual init command
            await GeneralCommandHandlers.HandleInitCommandWithResourceUrl(serverUrl);

            ConsoleUI.WriteStatus(true, "Workspace initialized successfully!");
            ConsoleUI.WriteInfo("Next step: Create your first agent");

            if (ConsoleUI.Confirm("Would you like to create an agent now?", true))
            {
                await GuideAgentCreation();
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Workspace initialization failed: {ex.Message}",
            [
                "Check if the server URL is correct and accessible",
                "Ensure the server is running",
                "Try again with a different URL"
            ]);
        }
    }

    private static async Task GuideApplyChanges()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Apply Changes to Server");
        ConsoleUI.WriteInfo("Deploy your agents and tools to the server so they can be used.");
        Console.WriteLine();

        var agentsDir = "agents";
        var toolsDir = "tools";

        var agentDirs = Directory.Exists(agentsDir) ? Directory.GetDirectories(agentsDir) : [];
        var toolDirs = Directory.Exists(toolsDir) ? Directory.GetDirectories(toolsDir) : [];

        if (agentDirs.Length == 0 && toolDirs.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No agents or tools found to deploy.");
            ConsoleUI.WriteInfo("Create an agent or tool first, then come back to apply changes.");
            return;
        }

        ConsoleUI.WriteSection("What would you like to deploy?");
        ConsoleUI.WriteKeyValue("1", "Deploy all agents and tools", 3);
        ConsoleUI.WriteKeyValue("2", "Deploy specific agent", 3);
        ConsoleUI.WriteKeyValue("3", "Deploy specific tool", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-3): ");

        var choice = Console.ReadLine()?.Trim();

        try
        {
            switch (choice)
            {
                case "1":
                    await DeployAllResources();
                    break;
                case "2":
                    await GuideAgentDeploymentSelection();
                    break;
                case "3":
                    await GuideToolDeploymentSelection();
                    break;
                default:
                    ConsoleUI.WriteStatus(false, "Invalid selection.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Deployment failed: {ex.Message}",
            [
                "Check your server connection",
                "Verify the resource configurations",
                "Try deploying individual resources"
            ]);
        }
    }

    private static async Task DeployAllResources()
    {
        Console.WriteLine();
        ConsoleUI.WriteInfo("Deploying all agents and tools...");

        // Deploy agents
        var agentsDir = "agents";
        if (Directory.Exists(agentsDir))
        {
            var agentDirs = Directory.GetDirectories(agentsDir);
            foreach (var agentDir in agentDirs)
            {
                var agentName = Path.GetFileName(agentDir);
                ConsoleUI.WriteInfo($"Deploying agent: {agentName}");
                await ApplyAgentDirectly(agentName!);
            }
        }

        // Deploy tools
        var toolsDir = "tools";
        if (Directory.Exists(toolsDir))
        {
            var toolDirs = Directory.GetDirectories(toolsDir);
            foreach (var toolDir in toolDirs)
            {
                var toolName = Path.GetFileName(toolDir);
                ConsoleUI.WriteInfo($"Deploying tool: {toolName}");
                await ApplyToolDirectly(toolName!);
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteStatus(true, "All resources deployed successfully!");
        ConsoleUI.WriteInfo("Ready to chat with your agents!");

        if (ConsoleUI.Confirm("Would you like to start a chat session now?", true))
        {
            await StartInteractiveChat();
        }
    }

    private static async Task HandleQuickDeployment(string choice)
    {
        try
        {
            var agentsDir = "agents";
            if (!Directory.Exists(agentsDir))
                return;

            var agentDirs = Directory.GetDirectories(agentsDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            var index = int.Parse(choice) - 5;
            if (index >= 0 && index < agentDirs.Length && index < 2)
            {
                var agentName = agentDirs[index];
                ConsoleUI.WriteInfo($"Quick deploying agent '{agentName}'...");
                await GuideAgentDeployment(agentName!);
            }
        }
        catch
        {
            ConsoleUI.WriteStatus(false, "Invalid selection for quick deployment.");
        }
    }

    private static async Task StartInteractiveChat()
    {
        Console.WriteLine();
        ConsoleUI.WriteInfo("Starting interactive chat session...");
        ConsoleUI.WriteInfo("This will connect you directly with your SRE agents for real-time assistance.");
        Console.WriteLine();

        try
        {
            // Check if we have any agents deployed
            var configService = new CliConfigurationService();
            var hasConfig = await configService.HasValidConfigurationAsync();

            if (!hasConfig)
            {
                ConsoleUI.WriteStatus(false, "No configuration found. Please run setup first.");
                return;
            }

            ConsoleUI.WriteCommand("Starting chat", "srectl chat");
            Console.WriteLine();

            // Start chat directly
            await StartChatDirectly();
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Chat failed to start: {ex.Message}",
            [
                "Check your server connection",
                "Verify agents are deployed",
                "Try 'srectl status' to diagnose issues"
            ]);
        }
    }

    private static async Task GuideScheduledTaskCreation()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Scheduled Task Creation Guide");
        ConsoleUI.WriteInfo("Scheduled tasks automate agent operations on a recurring schedule.");
        Console.WriteLine();

        ConsoleUI.WriteInline("What would you like to name your scheduled task? ");
        var taskName = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(taskName))
        {
            ConsoleUI.WriteStatus(false, "Task name is required.");
            await GuideScheduledTaskCreation();
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteInline("What should this task do? (Enter the prompt/instructions): ");
        var taskPrompt = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(taskPrompt))
        {
            ConsoleUI.WriteStatus(false, "Task prompt is required.");
            await GuideScheduledTaskCreation();
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Schedule Configuration");
        ConsoleUI.WriteInfo("Choose when this task should run:");
        ConsoleUI.WriteKeyValue("1", "Every 15 minutes", 3);
        ConsoleUI.WriteKeyValue("2", "Every hour", 3);
        ConsoleUI.WriteKeyValue("3", "Daily at 9 AM", 3);
        ConsoleUI.WriteKeyValue("4", "Weekly on Mondays", 3);
        ConsoleUI.WriteKeyValue("5", "Custom cron expression", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select schedule option (1-5): ");

        var scheduleChoice = Console.ReadLine()?.Trim();
        var cronExpression = scheduleChoice switch
        {
            "1" => "*/15 * * * *",
            "2" => "0 * * * *",
            "3" => "0 9 * * *",
            "4" => "0 9 * * 1",
            "5" => await GetCustomCronExpression(),
            _ => null
        };

        if (cronExpression == null)
        {
            ConsoleUI.WriteStatus(false, "Please select a valid schedule option (1-5).");
            await GuideScheduledTaskCreation();
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Agent Selection (Optional)");
        ConsoleUI.WriteInfo("You can connect this task to a specific deployed agent:");

        var selectedAgent = await SelectDeployedAgent();

        try
        {
            ConsoleUI.WriteInfo($"Creating scheduled task '{taskName}'...");
            Console.WriteLine();

            // Create the task using the ScheduledTaskCommandHandlers approach
            await CreateScheduledTaskDirectly(taskName, taskPrompt, cronExpression, selectedAgent);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Scheduled task '{taskName}' created successfully!");
            ConsoleUI.WriteKeyValue("Name", taskName);
            ConsoleUI.WriteKeyValue("Schedule", GetScheduleDescription(cronExpression));
            if (!string.IsNullOrEmpty(selectedAgent))
                ConsoleUI.WriteKeyValue("Agent", selectedAgent);

            ConsoleUI.WriteInfo("The task is now scheduled and will execute according to the specified schedule.");
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Scheduled task creation failed: {ex.Message}",
            [
                "Check your server connection",
                "Verify the task parameters",
                "Try with a simpler configuration"
            ]);

            if (ConsoleUI.Confirm("Would you like to try again?", true))
            {
                await GuideScheduledTaskCreation();
            }
        }
    }

    private static async Task<string> GetCustomCronExpression()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Custom Cron Expression");
        ConsoleUI.WriteInfo("Enter a cron expression (minute hour day month weekday):");
        ConsoleUI.WriteInfo("Examples:");
        ConsoleUI.WriteBullet("0 */6 * * * - Every 6 hours");
        ConsoleUI.WriteBullet("30 8 * * 1-5 - 8:30 AM on weekdays");
        ConsoleUI.WriteBullet("0 0 1 * * - First day of every month");
        Console.WriteLine();
        ConsoleUI.WriteInline("Cron expression: ");

        var cronExpression = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(cronExpression))
        {
            ConsoleUI.WriteStatus(false, "Cron expression is required.");
            return await GetCustomCronExpression();
        }

        return cronExpression;
    }

    private static async Task<string?> SelectDeployedAgent()
    {
        try
        {
            using var apiService = new ApiService();
            var (success, response, _) = await apiService.ListAgentsAsync();

            if (!success || string.IsNullOrEmpty(response))
            {
                ConsoleUI.WriteInfo("No deployed agents found. Task will run without a specific agent.");
                return null;
            }

            // Parse the JSON response to extract agent names
            var agentNames = new List<string>();
            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
                if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var element in jsonDoc.RootElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                agentNames.Add(name);
                            }
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                ConsoleUI.WriteInfo("Could not parse agent list. Task will run without a specific agent.", ConsoleColor.Yellow);
                return null;
            }

            if (agentNames.Count == 0)
            {
                ConsoleUI.WriteInfo("No deployed agents found. Task will run without a specific agent.");
                return null;
            }

            ConsoleUI.WriteKeyValue("0", "No specific agent (default)", 3);

            for (int i = 0; i < Math.Min(agentNames.Count, 9); i++)
            {
                ConsoleUI.WriteKeyValue($"{i + 1}", agentNames[i], 3);
            }

            Console.WriteLine();
            ConsoleUI.WriteInline($"Select agent (0-{Math.Min(agentNames.Count, 9)}): ");
            var choice = Console.ReadLine()?.Trim();

            if (int.TryParse(choice, out var index) && index > 0 && index <= Math.Min(agentNames.Count, 9))
            {
                return agentNames[index - 1];
            }

            return null; // No agent selected (option 0 or invalid)
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteInfo($"Could not fetch agents: {ex.Message}. Task will run without a specific agent.", ConsoleColor.Yellow);
            return null;
        }
    }

    private static async Task CreateScheduledTaskDirectly(string name, string prompt, string cronExpression, string? agent)
    {
        try
        {
            using var apiService = new ApiService();

            var task = new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = name,
                ["description"] = $"Interactive task: {name}",
                ["cronExpression"] = cronExpression,
                ["agentPrompt"] = prompt,
                ["agent"] = agent,
                ["startTime"] = DateTime.UtcNow.ToString("o"),
                ["endTime"] = null,
                ["threadId"] = null,
                ["maxExecutions"] = null,
                ["notificationChannel"] = null
            };

            // Save YAML locally first
            try
            {
                var manifest = new Common.Core.Manifests.ScheduledTaskManifest
                {
                    ApiVersion = "azuresre.ai/v1",
                    Kind = "ScheduledTask",
                    Metadata = new Common.Core.Manifests.ManifestMetadata { Name = name },
                    Spec = new Common.Core.Manifests.ScheduledTaskSpec
                    {
                        Name = name,
                        Description = $"Interactive task: {name}",
                        Cron = cronExpression,
                        AgentPrompt = prompt,
                        Agent = agent,
                        StartTime = DateTime.UtcNow,
                        EndTime = null,
                        ThreadId = null,
                        MaxExecutions = null,
                        NotificationChannel = null
                    }
                };

                var ser = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .DisableAliases()
                    .Build();
                var yaml = ser.Serialize(manifest);

                var dir = Path.Combine("scheduledtasks", name);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{name}.yaml");
                await File.WriteAllTextAsync(path, yaml, Encoding.UTF8);

                ConsoleUI.WriteBullet($"Saved YAML manifest to: {path}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Failed to save YAML locally: {ex.Message}");
                // Continue with API creation even if local save fails
            }

            var (success, message) = await apiService.CreateScheduledTaskAsync(task);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to create scheduled task: {message}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create scheduled task: {ex.Message}", ex);
        }
    }

    private static string GetScheduleDescription(string cronExpression)
    {
        return cronExpression switch
        {
            "*/15 * * * *" => "Every 15 minutes",
            "0 * * * *" => "Every hour",
            "0 9 * * *" => "Daily at 9:00 AM",
            "0 9 * * 1" => "Weekly on Mondays at 9:00 AM",
            _ => cronExpression
        };
    }

    private static async Task GuideToolCreation()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Tool Creation Guide");
        ConsoleUI.WriteInfo("Tools extend what your agents can do - like querying databases or calling APIs.");
        Console.WriteLine();

        ConsoleUI.WriteSection("Available tool types");
        ConsoleUI.WriteBullet("KustoTool     - Query Kusto/Azure Data Explorer/Log Analytics");
        // TODO: Enable these when they're fully supported
        // ConsoleUI.WriteBullet("AzureTool     - Interact with Azure resources");
        // ConsoleUI.WriteBullet("HttpTool      - Make HTTP API calls");
        // ConsoleUI.WriteBullet("ScriptTool    - Run custom scripts or commands");
        Console.WriteLine();

        ConsoleUI.WriteInline("What would you like to name your tool? ");
        var toolName = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(toolName))
        {
            ConsoleUI.WriteStatus(false, "Tool name is required.");
            await GuideToolCreation();
            return;
        }

        // Check if tool already exists
        var toolPath = Path.Combine("tools", toolName);
        if (Directory.Exists(toolPath))
        {
            ConsoleUI.WriteStatus(false, $"Tool '{toolName}' already exists.");
            if (ConsoleUI.Confirm("Would you like to choose a different name?", true))
            {
                await GuideToolCreation();
            }
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Choose tool type");
        ConsoleUI.WriteKeyValue("1", "KustoTool - Query Azure Data Explorer/Log Analytics", 3);
        // ConsoleUI.WriteKeyValue("2", "AzureTool - Interact with Azure resources", 3);
        // ConsoleUI.WriteKeyValue("3", "HttpTool - Make HTTP API calls", 3);
        // ConsoleUI.WriteKeyValue("4", "ScriptTool - Run custom scripts", 3);
        // Console.WriteLine();
        ConsoleUI.WriteInline("Select tool type (1): ");

        var typeChoice = Console.ReadLine()?.Trim();
        var toolType = typeChoice switch
        {
            "1" => "KustoTool",
            //"2" => "AzureTool",
            //"3" => "HttpTool",
            //"4" => "ScriptTool",
            _ => null
        };

        if (toolType == null)
        {
            ConsoleUI.WriteStatus(false, "Please select option 1 for KustoTool.");
            await GuideToolCreation();
            return;
        }

        try
        {
            ConsoleUI.WriteInfo($"Creating tool '{toolName}' of type '{toolType}'...");

            ConsoleUI.WriteCommand("Running command", $"srectl tool create --name {toolName} --type {toolType}");
            Console.WriteLine();

            // Create the tool directly
            await CreateAndRunToolCommand(toolName, toolType);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Tool '{toolName}' created successfully!");

            if (ConsoleUI.Confirm("Would you like to deploy this tool to the server?", true))
            {
                await GuideToolDeployment(toolName);
            }
            else
            {
                ConsoleUI.WriteInfo($"You can deploy later with: srectl tool apply --name {toolName}");
                await ShowMainMenu();
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Tool creation failed: {ex.Message}",
            [
                "Check the tool name and type",
                "Ensure the tools directory exists",
                "Try with a different name"
            ]);

            if (ConsoleUI.Confirm("Would you like to try again?", true))
            {
                await GuideToolCreation();
            }
        }
    }

    private static async Task ShowWorkspaceStatus()
    {
        Console.WriteLine();
        ConsoleUI.WriteInfo("Checking your workspace status...");
        Console.WriteLine();

        try
        {
            // Call the actual status functionality from CommandBuilder
            await CommandBuilder.ShowWorkspaceStatus();

            Console.WriteLine();
            ConsoleUI.WriteInfo("Status check complete!");
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Status check failed: {ex.Message}",
            [
                "Check your configuration",
                "Verify server connectivity",
                "Try 'srectl init' if not configured"
            ]);
        }
    }

    private static async Task ShowCommandBuilder()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Interactive Command Builder");
        ConsoleUI.WriteInfo("I'll help you build the perfect command for what you want to do.");
        Console.WriteLine();

        ConsoleUI.WriteSection("What do you want to do?");
        ConsoleUI.WriteKeyValue("1", "Create an agent", 3);
        ConsoleUI.WriteKeyValue("2", "Test an existing agent", 3);
        ConsoleUI.WriteKeyValue("3", "Create a tool", 3);
        ConsoleUI.WriteKeyValue("4", "Deploy something to the server", 3);
        ConsoleUI.WriteKeyValue("5", "Get information about deployed resources", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-5): ");

        var choice = Console.ReadLine()?.Trim();

        try
        {
            switch (choice)
            {
                case "1":
                    await GuideAgentCreation();
                    break;
                case "2":
                    await GuideAgentTesting();
                    break;
                case "3":
                    await GuideToolCreation();
                    break;
                case "4":
                    await GuideDeployment();
                    break;
                case "5":
                    await GuideInformation();
                    break;
                default:
                    ConsoleUI.WriteStatus(false, "Please select a valid option (1-5).");
                    await ShowCommandBuilder();
                    break;
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Command execution failed: {ex.Message}",
            [
                "Check your input parameters",
                "Verify server connectivity",
                "Try a simpler command first"
            ]);
        }
    }

    private static async Task GuideAgentTesting()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Agent Testing Guide");
        ConsoleUI.WriteInfo("Let's test one of your existing agents.");
        Console.WriteLine();

        // List available agents
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            ConsoleUI.WriteStatus(false, "No agents directory found. Create an agent first.");
            return;
        }

        var agentDirs = Directory.GetDirectories(agentsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();

        if (agentDirs.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No agents found. Create an agent first with 'Create an agent' option.");
            return;
        }

        ConsoleUI.WriteSection("Available Agents");
        for (int i = 0; i < agentDirs.Length; i++)
        {
            ConsoleUI.WriteKeyValue((i + 1).ToString(), agentDirs[i]!, 3);
        }
        Console.WriteLine();
        ConsoleUI.WriteInline($"Select an agent to test (1-{agentDirs.Length}): ");

        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out var index) && index > 0 && index <= agentDirs.Length)
        {
            var selectedAgent = agentDirs[index - 1];
            await GuideAgentTesting(selectedAgent!);
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Invalid selection.");
        }
    }

    private static async Task GuideDeployment()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Deployment Guide");
        ConsoleUI.WriteInfo("What would you like to deploy?");
        Console.WriteLine();

        ConsoleUI.WriteKeyValue("1", "Deploy an agent to the server", 3);
        ConsoleUI.WriteKeyValue("2", "Deploy a tool to the server", 3);
        ConsoleUI.WriteKeyValue("3", "Deploy from a YAML file", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-3): ");

        var choice = Console.ReadLine()?.Trim();
        switch (choice)
        {
            case "1":
                await GuideAgentDeploymentSelection();
                break;
            case "2":
                await GuideToolDeploymentSelection();
                break;
            case "3":
                await GuideYamlDeployment();
                break;
            default:
                ConsoleUI.WriteStatus(false, "Invalid selection.");
                break;
        }
    }

    private static async Task GuideInformation()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Information Guide");
        ConsoleUI.WriteInfo("What information would you like to see?");
        Console.WriteLine();

        ConsoleUI.WriteKeyValue("1", "List deployed agents", 3);
        ConsoleUI.WriteKeyValue("2", "List available tools", 3);
        ConsoleUI.WriteKeyValue("3", "List data connectors", 3);
        ConsoleUI.WriteKeyValue("4", "Show conversation threads", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-4): ");

        var choice = Console.ReadLine()?.Trim();

        try
        {
            switch (choice)
            {
                case "1":
                    ConsoleUI.WriteCommand("Running command", "srectl agent list");
                    Console.WriteLine();
                    await GeneralCommandHandlers.HandleListAgentsCommand(CreateEmptyParseResult());
                    break;
                case "2":
                    ConsoleUI.WriteCommand("Running command", "srectl tool list");
                    Console.WriteLine();
                    await GeneralCommandHandlers.HandleListToolsCommand(CreateEmptyParseResult());
                    break;
                case "3":
                    ConsoleUI.WriteCommand("Running command", "srectl list data-connectors");
                    Console.WriteLine();
                    await GeneralCommandHandlers.HandleListDataConnectorsCommand(CreateEmptyParseResult());
                    break;
                case "4":
                    ConsoleUI.WriteCommand("Running command", "srectl thread list");
                    Console.WriteLine();
                    await ThreadCommandHandlers.HandleThreadListCommand(CreateEmptyParseResult());
                    break;
                default:
                    ConsoleUI.WriteStatus(false, "Invalid selection.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Failed to get information: {ex.Message}",
            [
                "Check your server connection",
                "Verify your configuration with 'srectl status'",
                "Try again with --debug for more details"
            ]);
        }
    }

    // Helper methods to create ParseResult objects for calling actual command handlers

    private static async Task CreateAndRunAgentCommand(string name, string instructions, bool useSmart = false)
    {
        // Create a valid structured YAML using the same schema as non-interactive create
        try
        {
            string finalInstructions = instructions;
            List<string> finalTools = [];

            if (useSmart)
            {
                try
                {
                    using var api = new ApiService();
                    var (ok, generated, recommendedTools, mcpTools, err) = await api.GenerateSmartAgentAsync(name, instructions);
                    if (ok)
                    {
                        finalInstructions = string.IsNullOrWhiteSpace(generated) ? instructions : generated;
                        finalTools = recommendedTools ?? [];
                        ConsoleUI.WriteInfo($"AI suggested {finalTools.Count} tool(s)", ConsoleColor.Gray);

                        if (mcpTools?.Count > 0)
                        {
                            ConsoleUI.WriteInfo($"AI suggested {mcpTools.Count} mcp tool(s)", ConsoleColor.Gray);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(err))
                    {
                        ConsoleUI.WriteInfo($"AI suggestion failed: {err}. Continuing with your input.", ConsoleColor.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleUI.WriteInfo($"AI suggestion failed: {ex.Message}. Continuing with your input.", ConsoleColor.Yellow);
                }
            }

            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = finalInstructions,
                Tools = finalTools,
                Handoffs = [],
                HandoffDescription = string.Empty,
                AllowParallelToolCalls = false,
                MaxReflectionCount = 0,
                CriticPromptPath = string.Empty,
                CriticOnHandOff = false,
                CustomReflectionNote = string.Empty,
                CommonPrompts = [],
                Temperature = null,
                OutputType = null
            };

            // This writes api_version/kind/metadata/spec correctly
            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);

            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create agent: {ex.Message}", ex);
        }
    }

    private static Task CreateAndRunAzCliPresetAgent(string name)
    {
        try
        {
            var instructions = @"You execute Azure CLI commands safely for READ and WRITE across Azure resources.

  Core rules
  - Keep going until the user’s goal is met.
  - Before ANY action (read/write/help), emit notifyUserMessage: ""Checking … to …"" or ""Running … to …"".
  - Always include --subscription.
  - Prefer cached/chat history; only query what’s missing. Don’t re-run the same WRITE if result already exists.

  Execution flow
  1) Understand & restate goal; identify resources; classify read vs write.
  2) READ first (list/show/get) with focused --query; run immediately.
  3) If WRITE:
     - MUST call GetAzCliHelp on the exact operation to confirm parameters/syntax.
     - Build minimal correct command; run exactly ONE write at a time; wait for result.
     - For long ops, consider --no-wait and provide a status check command.
     - Never delete/remove.
  4) If a command fails: broaden the READ or re-check help; if help is insufficient or troubleshooting, use SearchDocuments.
  5) Verify with targeted READ of changed fields; summarize current → desired, impact, and brief rollback.

  Ask questions only for true ambiguities. Use GitHub Markdown for summaries.";

            var handoffDescription = @"Handoff to this agent when an Azure CLI command must be executed—read or write—against any Azure resource.
  <important>Prefer specialized agents first; if not covered, fall back to this agent. Always have the subscription GUID handy before handoff.</important>
  The sub-agent securely runs the command and returns raw CLI outp";

            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = instructions,
                Tools =
                [
                    "RunAzCliWriteCommands",
                    "RunAzCliReadCommands",
                    "GetAzCliHelp",
                    "SearchDocuments"
                ],
                CommonPrompts =
                [
                    "format_guidelines",
                    "guard_rail"
                ],
                Handoffs = [],
                HandoffDescription = handoffDescription,
                MaxReflectionCount = 2,
                CriticPromptPath = "CriticPrompts/aks-critic-prompt_medium.txt",
                CustomReflectionNote = @"- Reuse prior results if available.
  - Clear notifyUserMessage before every action.
  - READ vs WRITE classified; WRITE confirmed via help.
  - Syntax/params verified; --subscription present; use --yes only if supported.
  - One write at a time; plan, impact, rollback.
  - On failure: broaden/read/help; SearchDocuments if needed.
  - Verify outcomes with targeted reads.",
                Temperature = 0.2f,
                AllowParallelToolCalls = false,
                CriticOnHandOff = false,
                OutputType = null
            };

            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);
            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create Azure CLI preset agent: {ex.Message}", ex);
        }
    }

    private static Task CreateAndRunAksRemediationPresetAgent(string name)
    {
        try
        {
            var instructions = @"You are **AKS Remediation Agent**. Introduce yourself briefly and ask which AKS cluster and **workload** to target (namespace/kind/name). If unknown, try SearchResourceByName; lock onto one workload before acting.

  Core scope
  - Diagnose, remediate, and monitor AKS/Kubernetes workloads.
  - Operate strictly inside the cluster (no app dev, no infra provisioning).
  - If Azure/ARM changes are required (e.g., node pool scale), use HandoffBack with a short reason.

  Communication
  - Use notifyUserMessage for EVERY step (start, before action, finding, after remediation).
  - Always show the **WORKLOAD NAME** in bold.
  - Keep going autonomously until a mitigation is applied; don’t wait for approval.

  Minimal workflow
  1) Confirm target: cluster + single workload. Use SearchResourceByName / ListResourcesByType if needed.
  2) Diagnose fast: RunKubectlReadCommand (status, events, logs) and metrics (GetKubeResourceMetricsRange / DiscoverPrometheusMetrics / QueryPrometheusMetrics).
  3) Remediate: choose lowest-risk fix first (RunKubectlWriteCommand / PatchKubernetesYaml / RolloutRestartDeployment). Include simple rollback (e.g., rollout undo/previous image).
  4) Verify & monitor: re-check health, emit timestamped updates, and continue proposing actions until stable.
  5) If a step fails, adjust: broaden reads, refine hypothesis, try next remediation.

  Output summaries in GitHub Markdown.";

            var handoffDescription = @"Use this agent when you need **in-cluster AKS workload remediation** (pods, deployments, services, ingresses, configs) via kubectl-level actions.
  Use **HandoffBack** when the request requires anything **outside the cluster data plane** or beyond kubectl scope, including:
  - **Azure/ARM operations**: node pool scale/upgrade/drain, VMSS, subnet/NSG/UDR, Load Balancer/App Gateway, Public IP/DNS zone, Managed Identity/Key Vault/ACR auth, Log Analytics/Insights.
  - **Control plane / cluster lifecycle**: create/upgrade AKS, rotate credentials/certs, cluster-wide policy/add-ons.
  - **Non-Kubernetes or external deps**: databases, storage accounts, service bus, external DNS/CDN, app code changes/CI/CD.
  - **Forbidden/insufficient RBAC** or requests to run `az aks command invoke`.
  When handing off, include a one-line reason and the workload/cluster context gathered so far.";

            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = instructions,
                Tools =
                [
                    "SearchResourceByName",
                    "ListResourcesByType",
                    "RunKubectlReadCommand",
                    "RunKubectlWriteCommand",
                    "PatchKubernetesYaml",
                    "RolloutRestartDeployment",
                    "GetKubeResourceMetricsRange",
                    "DiscoverPrometheusMetrics",
                    "QueryPrometheusMetrics",
                    "PlotTimeSeriesData",
                    "PlotPieChart",
                    "PlotBarChart",
                    "HandoffBack"
                ],
                CommonPrompts =
                [
                    "format_guidelines",
                    "guard_rail"
                ],
                Handoffs = [],
                HandoffDescription = handoffDescription,
                MaxReflectionCount = 0,
                CriticPromptPath = string.Empty,
                CustomReflectionNote = string.Empty,
                Temperature = null,
                AllowParallelToolCalls = false,
                CriticOnHandOff = false,
                OutputType = null
            };

            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);
            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create AKS preset agent: {ex.Message}", ex);
        }
    }

    private static Task CreateAgentWithAzCliTemplate(string name, string instructions)
    {
        try
        {
            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = instructions,
                Tools =
                [
                    "RunAzCliWriteCommands",
                    "RunAzCliReadCommands",
                    "GetAzCliHelp",
                    "SearchDocuments"
                ],
                CommonPrompts =
                [
                    "format_guidelines",
                    "guard_rail"
                ],
                Handoffs = [],
                HandoffDescription = string.Empty,
                MaxReflectionCount = 1,
                CriticPromptPath = string.Empty,
                CustomReflectionNote = string.Empty,
                Temperature = 0.2f,
                AllowParallelToolCalls = false,
                CriticOnHandOff = false,
                OutputType = null
            };

            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);
            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create Azure CLI template-based agent: {ex.Message}", ex);
        }
    }

    private static Task CreateAgentWithAksTemplate(string name, string instructions)
    {
        try
        {
            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = instructions,
                Tools =
                [
                    "SearchResourceByName",
                    "ListResourcesByType",
                    "RunKubectlReadCommand",
                    "RunKubectlWriteCommand",
                    "PatchKubernetesYaml",
                    "RolloutRestartDeployment",
                    "GetKubeResourceMetricsRange",
                    "DiscoverPrometheusMetrics",
                    "QueryPrometheusMetrics",
                    "PlotTimeSeriesData",
                    "PlotPieChart",
                    "PlotBarChart",
                    "HandoffBack"
                ],
                CommonPrompts =
                [
                    "format_guidelines",
                    "guard_rail"
                ],
                Handoffs = [],
                HandoffDescription = string.Empty,
                MaxReflectionCount = 0,
                CriticPromptPath = string.Empty,
                CustomReflectionNote = string.Empty,
                Temperature = null,
                AllowParallelToolCalls = false,
                CriticOnHandOff = false,
                OutputType = null
            };

            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);
            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create AKS template-based agent: {ex.Message}", ex);
        }
    }

    private static async Task CreateAndApplyIcmKustoAgent(string name)
    {
        try
        {
            // 1) Create tools first
            var tool1Dir = Path.Combine("tools", "appservicegetregions");
            Directory.CreateDirectory(tool1Dir);
            var tool1Path = Path.Combine(tool1Dir, "appservicegetregions.yaml");
            var tool1Yaml = "" +
                "name: appservicegetregions\n" +
                "type: KustoTool\n" +
                "connector: wawswus\n" +
                "description: |\n" +
                "  Retrieves the regions where App Service is supported\n" +
                "mode: query\n" +
                "query: |\n" +
                "  GetRegions\n" +
                "  | distinct Name\n";
            await File.WriteAllTextAsync(tool1Path, tool1Yaml);
            ConsoleUI.WriteStatus(true, $"Tool 'appservicegetregions' created at {tool1Dir}");

            var tool2Dir = Path.Combine("tools", "CheckAllScenarioImpact");
            Directory.CreateDirectory(tool2Dir);
            var tool2Path = Path.Combine(tool2Dir, "CheckAllScenarioImpact.yaml");
            var tool2Yaml = "" +
                "name: CheckAllScenarioImpact\n" +
                "type: KustoTool\n" +
                "connector: wawswus\n" +
                "mode: query\n" +
                "description: |\n" +
                "  Checks scenario impact for a subscription or Service OID and returns a table.\n" +
                "query: |\n" +
                "  cluster('azrelsikusto-dev.westus.kusto.windows.net').database('Security').AzRF_OV_SR06a_DRIQuery_Table\n" +
                "  | extend parsed = parse_json(DRIInfo)\n" +
                "  | mv-expand scenario = bag_keys(parsed)\n" +
                "  | extend scenarioData = parsed[tostring(scenario)]\n" +
                "  | mv-expand row = scenarioData | evaluate bag_unpack(row)\n" +
                "  | extend Scenario = scenario\n" +
                "  | distinct tostring(Scenario), ServiceOid, Subscription, WebSpace, ResourceGroup, CanonicalName, SiteName, CertificateName, Thumbprint\n" +
                "  | where\n" +
                "      ('##SubscriptionId##' != '' and Subscription == '##SubscriptionId##') or\n" +
                "      ('##ServiceOid##' != '' and ServiceOid == '##ServiceOid##')\n" +
                "parameters:\n" +
                "  - name: SubscriptionId\n" +
                "    type: string\n" +
                "    required: false\n" +
                "    description: The subscription ID (GUID) to check for all scenario impacts\n" +
                "    map_to: args\n" +
                "    target: dictionary:args:string\n" +
                "    value: ''\n" +
                "  - name: ServiceOid\n" +
                "    type: string\n" +
                "    required: false\n" +
                "    description: The Service OID to check for all scenario impacts\n" +
                "    map_to: args\n" +
                "    target: dictionary:args:string\n" +
                "    value: ''\n";
            await File.WriteAllTextAsync(tool2Path, tool2Yaml);
            ConsoleUI.WriteStatus(true, $"Tool 'CheckAllScenarioImpact' created at {tool2Dir}");

            // 2) Apply tools first
            await ApplyToolDirectly("appservicegetregions");
            await ApplyToolDirectly("CheckAllScenarioImpact");

            // 3) Create agent referencing the tools
            var agent = new YamlAgentDescriptor
            {
                Name = name,
                Instructions = "You triage ICM incidents and run Kusto to investigate a web app frontend. Be concise and use the provided tools.",
                Tools = ["CheckAllScenarioImpact"],
                Handoffs = [],
                HandoffDescription = string.Empty,
                AllowParallelToolCalls = false,
                MaxReflectionCount = 0,
                CriticPromptPath = string.Empty,
                CriticOnHandOff = false,
                CustomReflectionNote = string.Empty,
                CommonPrompts = [],
                Temperature = 0.2f,
                OutputType = null
            };

            var folder = Path.Combine("agents", name);
            YamlHelper.WriteAgentYamlFile(folder, name, agent);
            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {folder}");

            // 4) Apply agent
            await ApplyAgentDirectly(name);

            ConsoleUI.WriteInfo("ICM + Kusto agent deployed. Ensure ICM handler is configured and the agent identity has Kusto reader access.", ConsoleColor.Yellow);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create/apply ICM + Kusto agent: {ex.Message}", ex);
        }
    }

    private static async Task CreateAndRunToolCommand(string name, string type, string? path = null)
    {
        // Direct tool creation without ParseResult complexity
        try
        {
            var toolPath = Path.Combine("tools", name);
            Directory.CreateDirectory(toolPath);

            var yamlContent = $@"name: {name}
type: {type}
description: A {type} tool created interactively";
            var yamlFile = Path.Combine(toolPath, $"{name}.yaml");
            await File.WriteAllTextAsync(yamlFile, yamlContent);

            ConsoleUI.WriteStatus(true, $"Tool '{name}' created at {toolPath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create tool: {ex.Message}", ex);
        }
    }

    private static async Task StartChatDirectly()
    {
        // Direct chat start without ParseResult
        try
        {
            await GeneralCommandHandlers.HandleChatCommand(CreateEmptyParseResult());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start chat: {ex.Message}", ex);
        }
    }

    private static async Task StartChatWithSpecificAgent(string agentName)
    {
        try
        {
            Console.WriteLine();
            ConsoleUI.WriteInfo($"Starting chat session with agent '{agentName}'...");
            ConsoleUI.WriteInfo("You can use /agent <name> to switch agents or /clear to start fresh.");
            Console.WriteLine();

            // Do NOT create a thread yet; defer thread creation until the first user message.
            // Reuse the existing chat handler which supports deferred thread creation and /agent switching.
            var chatCmd = new Command("chat")
            {
                AgentCommandOptions.ChatAgentNameOption
            };
            var parseResult = chatCmd.Parse(["--agent", agentName]);
            await GeneralCommandHandlers.HandleChatCommand(parseResult);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start chat with agent: {ex.Message}", ex);
        }
    }

    private static async Task ApplyAgentDirectly(string name, bool isDryRun = false)
    {
        // Direct agent apply using agent apply endpoint (not apply-yaml)
        try
        {
            var agentPath = Path.Combine("agents", name, $"{name}.yaml");
            if (!File.Exists(agentPath))
            {
                throw new FileNotFoundException($"Agent YAML file not found: {agentPath}");
            }

            if (!isDryRun)
            {
                // Use agent apply endpoint - same as regular "srectl agent apply --name {name}" command
                using var apiService = new ApiService();
                var (success, response) = await apiService.ApplyOrValidateAgentAsync(name, dryRun: false);

                if (success)
                {
                    ConsoleUI.WriteStatus(true, $"Agent '{name}' deployed to server");
                }
                else
                {
                    Console.WriteLine(response);
                    throw new InvalidOperationException($"Failed to deploy agent '{name}': {response}");
                }
            }
            else
            {
                ConsoleUI.WriteInfo($"[DRY RUN] Would deploy agent '{name}' from {agentPath}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply agent: {ex.Message}", ex);
        }
    }

    private static async Task TestAgentDirectly(string name, string message)
    {
        // Direct agent test using same approach as regular "srectl agent test" command
        try
        {
            var userId = Environment.UserName;
            var displayName = Environment.UserName;
            var prefixedMessage = $"Use the {name} agent for the below user query\n{message}";

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            // Create a test thread
            ConsoleUI.WriteInfo($"Testing agent '{name}' with message: {message}");
            var (createSuccess, threadId, createResponse) = await apiService.CreateThreadAsync(message, userId, displayName);

            if (!createSuccess)
            {
                ConsoleUI.WriteStatus(false, $"Failed to create test thread: {createResponse}");
                throw new InvalidOperationException($"Failed to test agent '{name}': {createResponse}");
            }

            // Store the thread locally
            await threadManager.AddThreadAsync(threadId, $"Agent Test: {name}");

            // Wait for agent response using the same method as the regular test command
            ConsoleUI.WriteInfo($"Waiting for {name} agent response...");
            var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesStreamingAsync(threadId);

            if (!getSuccess)
            {
                ConsoleUI.WriteStatus(false, $"Failed to get agent response: {getResponse}");
                throw new InvalidOperationException($"Failed to get agent response: {getResponse}");
            }

            // Just confirm the test completed successfully, like the regular test command
            ConsoleUI.WriteStatus(true, "Test completed successfully!");
            ConsoleUI.WriteKeyValue("Thread ID", threadId);
            ConsoleUI.WriteInfo($"Continue with: srectl thread continue --thread-id {threadId}", ConsoleColor.Cyan);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to test agent: {ex.Message}", ex);
        }
    }

    private static async Task ApplyToolDirectly(string name, bool isDryRun = false)
    {
        // Direct tool apply using apply-yaml approach (same as regular commands)
        try
        {
            var toolPath = Path.Combine("tools", name, $"{name}.yaml");
            if (!File.Exists(toolPath))
            {
                throw new FileNotFoundException($"Tool YAML file not found: {toolPath}");
            }

            if (!isDryRun)
            {
                // Use apply-yaml approach for consistency with regular commands
                using var apiService = new ApiService();
                var (success, response) = await apiService.ApplyYamlFileAsync(toolPath);

                if (success)
                {
                    ConsoleUI.WriteStatus(true, $"Tool '{name}' deployed to server");
                }
                else
                {
                    Console.WriteLine(response);
                    throw new InvalidOperationException($"Failed to deploy tool '{name}': {response}");
                }
            }
            else
            {
                ConsoleUI.WriteInfo($"[DRY RUN] Would deploy tool '{name}' from {toolPath}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply tool: {ex.Message}", ex);
        }
    }

    // New deployment and testing workflows

    private static async Task GuideAgentDeployment(string agentName)
    {
        Console.WriteLine();
        ConsoleUI.WriteSection($"Deploying Agent '{agentName}'");
        ConsoleUI.WriteInfo("Let's deploy your agent to the server so it can start helping you.");
        Console.WriteLine();

        try
        {
            // First, validate the agent
            ConsoleUI.WriteInfo("Validating agent configuration...");

            // Deploy the agent
            ConsoleUI.WriteCommand("Running command", $"srectl agent apply --name {agentName}");
            Console.WriteLine();

            await ApplyAgentDirectly(agentName);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Agent '{agentName}' deployed successfully!");
            ConsoleUI.WriteInfo("Changing to a different agent starts a new chat thread.");

            if (ConsoleUI.Confirm("Would you like to start a chat with this agent now?", true))
            {
                await StartChatWithSpecificAgent(agentName);
            }
            else
            {
                ConsoleUI.WriteInfo($"You can start a chat later with: /agent {agentName}");
                await ShowMainMenu();
            }
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Agent deployment failed: {ex.Message}",
            [
                "Check your server connection",
                "Verify the agent configuration is valid",
                "Try 'srectl status' to check connectivity"
            ]);
        }
    }

    private static async Task GuideAgentTesting(string agentName)
    {
        Console.WriteLine();
        ConsoleUI.WriteSection($"Testing Agent '{agentName}'");
        ConsoleUI.WriteInfo("Let's send a test message to your agent to make sure it's working.");
        Console.WriteLine();

        ConsoleUI.WriteInline("What would you like to ask your agent? (or press Enter for a default message): ");
        var testMessage = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(testMessage))
        {
            testMessage = "Hello! Can you help me understand what you can do?";
            ConsoleUI.WriteInfo($"Using default message: {testMessage}");
        }

        try
        {
            Console.WriteLine();
            ConsoleUI.WriteCommand("Running command", $"srectl agent test --name {agentName} --message '{testMessage}'");
            Console.WriteLine();

            await TestAgentDirectly(agentName, testMessage);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, "Agent test completed!");
            ConsoleUI.WriteInfo($"Great! Your agent '{agentName}' is working properly.");

            Console.WriteLine();
            ConsoleUI.WriteSection("What's Next?");
            ConsoleUI.WriteBullet("Start a chat session: srectl chat");
            ConsoleUI.WriteBullet("Create more agents: srectl agent create");
            ConsoleUI.WriteBullet("Create tools to extend capabilities: srectl tool create");
            Console.WriteLine();

            await ShowMainMenu();
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Agent testing failed: {ex.Message}",
            [
                "Check if the agent is properly deployed",
                "Verify server connectivity",
                "Try with a simpler message"
            ]);
        }
    }

    private static async Task GuideToolDeployment(string toolName)
    {
        Console.WriteLine();
        ConsoleUI.WriteSection($"Deploying Tool '{toolName}'");
        ConsoleUI.WriteInfo("Let's deploy your tool to the server so agents can use it.");
        Console.WriteLine();

        try
        {
            ConsoleUI.WriteCommand("Running command", $"srectl tool apply --name {toolName}");
            Console.WriteLine();

            await ApplyToolDirectly(toolName);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, $"Tool '{toolName}' deployed successfully!");
            ConsoleUI.WriteInfo("Your agents can now use this tool to extend their capabilities.");

            await ShowMainMenu();
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"Tool deployment failed: {ex.Message}",
            [
                "Check your server connection",
                "Verify the tool configuration is valid",
                "Try 'srectl status' to check connectivity"
            ]);
        }
    }

    private static ParseResult CreateEmptyParseResult()
    {
        var mockCommand = new Command("list");
        return mockCommand.Parse(Array.Empty<string>());
    }

    private static async Task GuideAgentDeploymentSelection()
    {
        var agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            ConsoleUI.WriteStatus(false, "No agents directory found. Create an agent first.");
            return;
        }

        var agentDirs = Directory.GetDirectories(agentsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();

        if (agentDirs.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No agents found. Create an agent first.");
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Available Agents to Deploy");
        for (int i = 0; i < agentDirs.Length; i++)
        {
            ConsoleUI.WriteKeyValue((i + 1).ToString(), agentDirs[i]!, 3);
        }
        Console.WriteLine();
        ConsoleUI.WriteInline($"Select an agent to deploy (1-{agentDirs.Length}): ");

        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out var index) && index > 0 && index <= agentDirs.Length)
        {
            var selectedAgent = agentDirs[index - 1];
            await GuideAgentDeployment(selectedAgent!);
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Invalid selection.");
        }
    }

    private static async Task GuideToolDeploymentSelection()
    {
        var toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            ConsoleUI.WriteStatus(false, "No tools directory found. Create a tool first.");
            return;
        }

        var toolDirs = Directory.GetDirectories(toolsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();

        if (toolDirs.Length == 0)
        {
            ConsoleUI.WriteStatus(false, "No tools found. Create a tool first.");
            return;
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Available Tools to Deploy");
        for (int i = 0; i < toolDirs.Length; i++)
        {
            ConsoleUI.WriteKeyValue((i + 1).ToString(), toolDirs[i]!, 3);
        }
        Console.WriteLine();
        ConsoleUI.WriteInline($"Select a tool to deploy (1-{toolDirs.Length}): ");

        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out var index) && index > 0 && index <= toolDirs.Length)
        {
            var selectedTool = toolDirs[index - 1];
            await GuideToolDeployment(selectedTool!);
        }
        else
        {
            ConsoleUI.WriteStatus(false, "Invalid selection.");
        }
    }

    private static async Task GuideYamlDeployment()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("YAML Deployment Guide");
        ConsoleUI.WriteInfo("Deploy any YAML configuration file to the server.");
        Console.WriteLine();

        ConsoleUI.WriteInline("Enter the path to your YAML file: ");
        var yamlPath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(yamlPath) || !File.Exists(yamlPath))
        {
            ConsoleUI.WriteStatus(false, "File not found. Please provide a valid YAML file path.");
            return;
        }

        try
        {
            ConsoleUI.WriteCommand("Running command", $"srectl apply-yaml --file {yamlPath}");
            Console.WriteLine();

            await ApplyYamlDirectly(yamlPath);

            Console.WriteLine();
            ConsoleUI.WriteStatus(true, "YAML file deployed successfully!");
        }
        catch (Exception ex)
        {
            ProgressService.ShowError($"YAML deployment failed: {ex.Message}",
            [
                "Check the YAML file format",
                "Verify server connection",
                "Ensure the file contains valid configuration"
            ]);
        }
    }

    private static async Task ApplyYamlDirectly(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"YAML file not found: {filePath}");
            }

            // Call the actual apply-yaml API service (same as regular apply-yaml command)
            using var apiService = new ApiService();
            var (success, response) = await apiService.ApplyYamlFileAsync(filePath);

            if (success)
            {
                ConsoleUI.WriteStatus(true, $"YAML configuration applied from: {filePath}");
            }
            else
            {
                Console.WriteLine(response);
                throw new InvalidOperationException($"Failed to apply YAML from '{filePath}': {response}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to apply YAML: {ex.Message}", ex);
        }
    }
}
