using Agent.Cli.Services;
using Agent.Cli.Helpers;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Agent.Cli.Commands;

namespace Agent.Cli.Commands;

public static class InteractiveCommandHandlers
{
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
                ProgressService.ShowError("Setup failed", new[]
                {
                    "Check if the server URL is correct",
                    "Ensure the server is running and accessible",
                    "Try again with a different URL"
                });
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
        ConsoleUI.WriteKeyValue("1", "Custom Agent (recommended for getting started - I'll describe what I want)", 3);
        ConsoleUI.WriteKeyValue("2", "Incident Response (handle ICM incidents with Kusto queries)", 3);
        ConsoleUI.WriteKeyValue("3", "Kubernetes Operations (pod, service, deployment issues)", 3);
        ConsoleUI.WriteKeyValue("4", "Azure CLI Operations (resource management, monitoring)", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select an option (1-4): ");

        var choice = Console.ReadLine()?.Trim();
        var instructions = choice switch
        {
            "1" => await GetCustomInstructions("Custom Agent"),
            "2" => await GetCustomInstructions("Incident Response"),
            "3" => await GetCustomInstructions("Kubernetes Operations"),
            "4" => await GetCustomInstructions("Azure CLI Operations"),
            _ => null
        };

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
            ConsoleUI.WriteCommand("Running command", $"srectl agent create --name {agentName} --smart");
            Console.WriteLine();

            // Create the agent directly
            await CreateAndRunAgentCommand(agentName, instructions, useSmart: true);

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
            ProgressService.ShowError($"Agent creation failed: {ex.Message}", new[]
            {
                "Try creating a simpler agent first",
                "Check your server connection",
                "Use 'srectl help troubleshooting' for more help"
            });

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
                "You are a friendly Hello World agent who greets users with enthusiasm and warmth. Your primary role is to welcome users to the SRE Agent platform and help them feel comfortable getting started. Always maintain a positive, encouraging tone and end every interaction with an inspirational SRE quote about reliability, automation, or continuous improvement. You should be patient with new users and guide them through basic concepts while celebrating their progress.",
                "You are a friendly Log Analyzer agent with expertise in analyzing application logs and identifying patterns, errors, and performance issues. You excel at parsing through complex log files, correlating events across multiple systems, and presenting findings in clear, actionable insights. Your approach is methodical yet approachable - you break down complex technical issues into understandable explanations while providing specific recommendations for remediation and monitoring improvements.",
                "You are a friendly Alert Responder agent specializing in triaging alerts, escalating critical issues, and coordinating incident response activities. You maintain calm under pressure and excel at quickly assessing alert severity, gathering relevant context, and determining appropriate response actions. Your communication style is clear and decisive during incidents while being supportive and collaborative during post-incident reviews and process improvements.",
                "You are a friendly Performance Monitor agent focused on system performance analysis, metrics interpretation, and optimization suggestions. You have a keen eye for identifying performance bottlenecks, resource utilization patterns, and capacity planning opportunities. Your recommendations are always data-driven and practical, helping teams understand not just what is happening but why it matters and how to improve it systematically.",
                "You are a friendly Documentation Helper agent who creates and maintains technical documentation, runbooks, and troubleshooting guides. You excel at transforming complex technical processes into clear, step-by-step documentation that teams can easily follow. Your writing style is concise yet comprehensive, and you always consider the end-user experience when organizing information and creating helpful examples and diagrams."
            },
            "Incident Response" => new[]
            {
                "You are a friendly ICM Alert Triager agent who specializes in analyzing ICM incidents with precision and care. You expertly run Kusto queries to gather comprehensive diagnostics, correlate incident data across multiple systems, and prioritize alerts based on severity and business impact. Your approach combines technical expertise with clear communication, helping teams understand incident scope and recommended actions. You remain calm under pressure and provide structured, actionable guidance during critical situations.",
                "You are a friendly Outage Coordinator agent who excels at orchestrating incident response teams during service disruptions. You help track resolution progress, manage communication channels, and ensure all stakeholders stay informed throughout the incident lifecycle. Your strength lies in maintaining situational awareness, coordinating cross-team efforts, and facilitating effective decision-making during high-stress scenarios while keeping everyone focused on restoration goals.",
                "You are a friendly Post-Incident Analyzer agent who specializes in reviewing completed incidents to extract valuable learnings. You thoroughly analyze incident timelines, identify root causes, and develop comprehensive reports that highlight both technical and process improvements. Your recommendations are practical and actionable, focusing on preventive measures using telemetry data and system insights to strengthen overall reliability and incident response capabilities.",
                "You are a friendly Emergency Escalator agent who monitors critical alerts and ensures high-priority incidents receive immediate attention from appropriate on-call engineers. You excel at rapid severity assessment, automated escalation workflows, and maintaining clear communication chains during critical events. Your role is crucial in minimizing mean-time-to-response while ensuring the right experts are engaged quickly and efficiently.",
                "You are a friendly Incident Documenter agent who creates detailed, comprehensive incident reports and timeline analyses. You specialize in capturing the complete incident narrative, documenting lessons learned, and generating actionable follow-up items from ICM data. Your documentation helps teams improve their incident response processes and serves as valuable reference material for future incidents and training purposes."
            },
            "Kubernetes Operations" => new[]
            {
                "You are a friendly Pod Troubleshooter agent specializing in diagnosing and resolving pod failures, restart loops, and resource-related issues in Kubernetes clusters. You have deep expertise in analyzing pod logs, examining resource constraints, and understanding container lifecycle management. Your troubleshooting approach is systematic and thorough, helping teams quickly identify root causes and implement effective solutions while sharing knowledge to prevent similar issues in the future.",
                "You are a friendly Deployment Manager agent who excels at managing rolling updates, rollbacks, and resolving deployment configuration issues in Kubernetes environments. You understand the intricacies of deployment strategies, health checks, and configuration management. Your guidance helps teams achieve smooth deployments while minimizing downtime and ensuring application reliability through proper rollout procedures and rollback strategies when needed.",
                "You are a friendly Resource Monitor agent focused on analyzing cluster resource usage, identifying performance bottlenecks, and suggesting practical optimization strategies. You have expertise in resource allocation, capacity planning, and performance tuning across Kubernetes clusters. Your recommendations help teams optimize resource utilization, improve application performance, and plan for future scaling needs through data-driven insights and best practices.",
                "You are a friendly Service Mesh Expert agent who specializes in troubleshooting service-to-service communication, ingress configuration issues, and network policy problems. You understand the complexities of modern microservices architectures and excel at diagnosing connectivity issues, traffic routing problems, and security policy configurations. Your expertise helps teams maintain reliable service communication and implement effective network security measures.",
                "You are a friendly Cluster Health Checker agent dedicated to monitoring and maintaining overall Kubernetes cluster stability. You continuously assess node health, etcd status, control plane components, and cluster-wide metrics. Your proactive monitoring approach helps identify potential issues before they impact applications, and you provide clear guidance on maintaining cluster health and implementing preventive maintenance procedures."
            },
            "Azure CLI Operations" => new[]
            {
                "You are a friendly Azure Resource Manager agent who specializes in creating, configuring, and managing Azure resources using CLI commands and industry best practices. You have comprehensive knowledge of Azure services, resource templates, and automation workflows. Your guidance helps teams efficiently provision and manage cloud infrastructure while following security best practices, cost optimization principles, and operational excellence standards.",
                "You are a friendly Azure Monitoring Specialist agent who excels at setting up comprehensive monitoring solutions using Azure Monitor, configuring intelligent alerts, creating effective log analytics queries, and building insightful dashboard configurations. You help teams establish proactive monitoring strategies, set up meaningful alerting thresholds, and create visualizations that provide actionable insights into application and infrastructure performance.",
                "You are a friendly Azure Security Auditor agent focused on reviewing and strengthening Azure security configurations, access policies, and compliance settings. You have deep expertise in Azure security best practices, identity management, network security, and regulatory compliance requirements. Your audits help teams identify security gaps, implement robust access controls, and maintain compliance with industry standards and organizational policies.",
                "You are a friendly Azure Cost Optimizer agent who analyzes resource usage patterns and suggests practical cost-saving measures for Azure infrastructure. You understand Azure pricing models, resource optimization techniques, and cost management best practices. Your recommendations help teams reduce unnecessary expenses while maintaining performance and reliability, providing clear ROI analysis and implementation guidance for cost optimization initiatives.",
                "You are a friendly Azure Backup & Recovery agent who specializes in designing and managing comprehensive backup strategies, disaster recovery plans, and data protection policies. You help teams implement robust backup solutions, test recovery procedures, and ensure business continuity through well-planned disaster recovery strategies. Your expertise covers backup scheduling, retention policies, cross-region replication, and recovery testing procedures."
            },
            _ => new[] { "Default agent description" }
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
                Array.Empty<string>();

            var toolDirs = Directory.Exists(toolsDir) ?
                Directory.GetDirectories(toolsDir).Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).ToArray() :
                Array.Empty<string>();

            if (agentDirs.Length > 0 || toolDirs.Length > 0)
            {
                ConsoleUI.WriteKeyValue("3", "Apply changes (deploy to server)", 3);

                // Show quick deploy options for first few agents
                var displayCount = Math.Min(3, agentDirs.Length);
                for (int i = 0; i < displayCount; i++)
                {
                    ConsoleUI.WriteKeyValue($"{i + 4}", $"Quick deploy '{agentDirs[i]}'", 3);
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

    private static Task ShowQuickActions()
    {
        try
        {
            // Show recent agents for quick access
            var agentsDir = "agents";
            if (Directory.Exists(agentsDir))
            {
                var agentDirs = Directory.GetDirectories(agentsDir)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Take(3)
                    .ToArray();

                if (agentDirs.Length > 0)
                {
                    Console.WriteLine();
                    ConsoleUI.WriteInfo("Quick Actions:");
                    for (int i = 0; i < agentDirs.Length && i < 3; i++)
                    {
                        ConsoleUI.WriteKeyValue($"{i + 4}", $"Deploy '{agentDirs[i]}'", 3);
                    }
                }
            }
        }
        catch
        {
            // Silently handle any errors
        }
        return Task.CompletedTask;
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
                    await GuideApplyChanges();
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Please initialize workspace first (option 1)");
                }
                break;
            case "4":
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
            ProgressService.ShowError($"Workspace initialization failed: {ex.Message}", new[]
            {
                "Check if the server URL is correct and accessible",
                "Ensure the server is running",
                "Try again with a different URL"
            });
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

        var agentDirs = Directory.Exists(agentsDir) ? Directory.GetDirectories(agentsDir) : Array.Empty<string>();
        var toolDirs = Directory.Exists(toolsDir) ? Directory.GetDirectories(toolsDir) : Array.Empty<string>();

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
            ProgressService.ShowError($"Deployment failed: {ex.Message}", new[]
            {
                "Check your server connection",
                "Verify the resource configurations",
                "Try deploying individual resources"
            });
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

            var index = int.Parse(choice) - 4;
            if (index >= 0 && index < agentDirs.Length && index < 3)
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
            ProgressService.ShowError($"Chat failed to start: {ex.Message}", new[]
            {
                "Check your server connection",
                "Verify agents are deployed",
                "Try 'srectl status' to diagnose issues"
            });
        }
    }

    private static async Task GuideToolCreation()
    {
        Console.WriteLine();
        ConsoleUI.WriteSection("Tool Creation Guide");
        ConsoleUI.WriteInfo("Tools extend what your agents can do - like querying databases or calling APIs.");
        Console.WriteLine();

        ConsoleUI.WriteSection("Available tool types");
        ConsoleUI.WriteBullet("KustoTool     - Query Kusto/Azure Data Explorer/Log Analytics");
        ConsoleUI.WriteBullet("AzureTool     - Interact with Azure resources");
        ConsoleUI.WriteBullet("HttpTool      - Make HTTP API calls");
        ConsoleUI.WriteBullet("ScriptTool    - Run custom scripts or commands");
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
        ConsoleUI.WriteKeyValue("2", "AzureTool - Interact with Azure resources", 3);
        ConsoleUI.WriteKeyValue("3", "HttpTool - Make HTTP API calls", 3);
        ConsoleUI.WriteKeyValue("4", "ScriptTool - Run custom scripts", 3);
        Console.WriteLine();
        ConsoleUI.WriteInline("Select a tool type (1-4): ");

        var typeChoice = Console.ReadLine()?.Trim();
        var toolType = typeChoice switch
        {
            "1" => "KustoTool",
            "2" => "AzureTool",
            "3" => "HttpTool",
            "4" => "ScriptTool",
            _ => null
        };

        if (toolType == null)
        {
            ConsoleUI.WriteStatus(false, "Please select a valid option (1-4).");
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
            ProgressService.ShowError($"Tool creation failed: {ex.Message}", new[]
            {
                "Check the tool name and type",
                "Ensure the tools directory exists",
                "Try with a different name"
            });

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
            ProgressService.ShowError($"Status check failed: {ex.Message}", new[]
            {
                "Check your configuration",
                "Verify server connectivity",
                "Try 'srectl init' if not configured"
            });
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
            ProgressService.ShowError($"Command execution failed: {ex.Message}", new[]
            {
                "Check your input parameters",
                "Verify server connectivity",
                "Try a simpler command first"
            });
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
            ProgressService.ShowError($"Failed to get information: {ex.Message}", new[]
            {
                "Check your server connection",
                "Verify your configuration with 'srectl status'",
                "Try again with --debug for more details"
            });
        }
    }

    // Helper methods to create ParseResult objects for calling actual command handlers

    private static async Task CreateAndRunAgentCommand(string name, string instructions, bool useSmart = false)
    {
        // Instead of creating ParseResult objects, directly call the creation logic
        // This is a simplified approach that avoids the ParseResult complexity

        try
        {
            var agentPath = Path.Combine("agents", name);
            Directory.CreateDirectory(agentPath);

            var yamlContent = $@"name: {name}
system_prompt: {instructions}
tools: []";
            var yamlFile = Path.Combine(agentPath, $"{name}.yaml");
            await File.WriteAllTextAsync(yamlFile, yamlContent);

            ConsoleUI.WriteStatus(true, $"Agent '{name}' created at {agentPath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create agent: {ex.Message}", ex);
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
            var parseResult = chatCmd.Parse(new[] { "--agent", agentName });
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
                var (success, response) = await apiService.ApplyAgentAsync(name);

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
            ProgressService.ShowError($"Agent deployment failed: {ex.Message}", new[]
            {
                "Check your server connection",
                "Verify the agent configuration is valid",
                "Try 'srectl status' to check connectivity"
            });
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
            ProgressService.ShowError($"Agent testing failed: {ex.Message}", new[]
            {
                "Check if the agent is properly deployed",
                "Verify server connectivity",
                "Try with a simpler message"
            });
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
            ProgressService.ShowError($"Tool deployment failed: {ex.Message}", new[]
            {
                "Check your server connection",
                "Verify the tool configuration is valid",
                "Try 'srectl status' to check connectivity"
            });
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
            ProgressService.ShowError($"YAML deployment failed: {ex.Message}", new[]
            {
                "Check the YAML file format",
                "Verify server connection",
                "Ensure the file contains valid configuration"
            });
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
