// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    public static RootCommand BuildCommands()
    {
        // ----- Build subcommand trees

        var syncCommand = CreateSyncCommand();
        //var listCommand = CreateListCommand();

        // Add default action for list command to show formatted help
        //listCommand.SetAction(pr => ShowFormattedListHelp(listCommand));

        var chatCommand = CreateChatCommand();
        var welcomeCommand = CreateWelcomeCommand();
        var helpCommand = CreateEnhancedHelpCommand();
        var statusCommand = CreateStatusCommand();
        var interactiveCommand = CreateInteractiveCommand();
        var versionCommand = CreateVersionCommand();

        // ----- Root
        var root = new RootCommand(
            "SRE Agent CLI - Your intelligent assistant for managing SRE agents and automating incident response\n\n" +
            "Incident Handler Commands: create, map-agent, list (run 'srectl incidenthandler --help' for details)")
        {
            welcomeCommand,
            helpCommand,
            statusCommand,
            interactiveCommand,
            versionCommand,
            InitCommand.Build(),
            syncCommand,
            ApplyYamlCommand.Build(),
            ApplyCommand.Build(),
            ThreadCommand.Build(),
            chatCommand,
            AgentCommand.Build(),
            ToolCommand.Build(),
            CommonPromptCommand.Build(),
            DocumentCommand.Build(),
            ProfileCommand.Build(),
            SkillCommand.Build(),
            IncidentHandlerCommand.Build(),
            ScheduledTaskCommand.Build(),
            ExtensionCommand.Build(),
            McpCommand.Build(),
        };

        // Single root action (runs when no verb provided)
        root.SetAction(HandleRootCommand);

        // Simulate global options on all commands (older System.CommandLine lacks AddGlobalOption)
        root.AddGlobalOptionsCompat(GlobalOptions.DebugOption, GlobalOptions.QuietOption);

        return root;
    }

    private static Command CreateSyncCommand()
    {
        var cmd = new Command("sync", CommandExamples.General.SyncDescription);

        cmd.SetAction(GeneralCommandHandlers.HandleSyncCommand);
        return cmd;
    }

    //private static Command CreateListCommand()
    //{
    //    var listAgents = new Command("agents", "List remote extended agents from the server")
    //    {
    //        AgentCommandOptions.List.SearchOption,
    //        AgentCommandOptions.List.NameOption,
    //        AgentCommandOptions.List.DetailOption
    //    };
    //    listAgents.SetAction(AgentCommandHandlers.HandleListCommand);

    //    var listExtendedTools = new Command("extended-tools", "List all extended tools added to the server through apply command");
    //    listExtendedTools.SetAction(GeneralCommandHandlers.HandleListExtendedToolsCommand);

    //    var listDataConnectors = new Command("data-connectors", "List all data connectors configured on the server");
    //    listDataConnectors.SetAction(GeneralCommandHandlers.HandleListDataConnectorsCommand);

    //    // List incident handlers subcommand
    //    var listIncidentHandlersCommand = new Command("incidenthandlers", "List all incident handlers from the remote server")
    //    {
    //        IncidentHandlerCommandOptions.List.VerboseOption
    //    };

    //    listIncidentHandlersCommand.SetAction(IncidentHandlerCommandHandlers.HandleListCommand);

    //    var cmd = new Command("list", CommandExamples.General.ListDescription)
    //    {
    //        listAgents, listExtendedTools, listDataConnectors, listIncidentHandlersCommand
    //    };
    //    return cmd;
    //}

    private static Command CreateChatCommand()
    {
        var cmd = new Command("chat", CommandExamples.General.ChatDescription)
        {
            ThreadCommandOptions.New.AgentNameOption
        };
        cmd.SetAction(GeneralCommandHandlers.HandleChatCommand);
        return cmd;
    }

    private static Command CreateWelcomeCommand()
    {
        var cmd = new Command("welcome", "Show welcome screen and getting started guide");
        cmd.SetAction(async _ =>

        {
            WelcomeService.ShowWelcomeBanner();
            await WelcomeService.ShowContextualGuidance();
        });
        return cmd;
    }

    private static Command CreateEnhancedHelpCommand()
    {
        var topic = new Argument<string>("topic")
        {
            Description = "Help topic to display",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("help", "Interactive help system with examples and troubleshooting") { topic };

        // Bind the positional arg via ParseResult
        cmd.SetAction(async pr =>

        {
            string? t = pr.GetValue(topic);
            await InteractiveHelpService.ShowInteractiveHelp(t);
        });
        return cmd;
    }

    private static Command CreateStatusCommand()
    {
        var cmd = new Command("status", "Show workspace status and health check");
        cmd.SetAction(GeneralCommandHandlers.HandleStatusCommand);
        return cmd;
    }

    private static Command CreateInteractiveCommand()
    {
        var cmd = new Command("interactive", "Start interactive guided mode for step-by-step assistance");
        cmd.SetAction(InteractiveCommandHandlers.HandleInteractiveMode);
        return cmd;
    }

    private static Command CreateVersionCommand()
    {
        var cmd = new Command("version", "Show version information and build details");
        cmd.SetAction(GeneralCommandHandlers.HandleVersionCommand);
        return cmd;
    }

    // -------- Root action & status --------

    private static Task HandleRootCommand(ParseResult parseResult)
    {
        ConsoleUI.WriteSection("SRE Agent CLI (srectl)");
        return ShowRootAfterWelcome();
    }

    private static async Task ShowRootAfterWelcome()
    {
        await WelcomeService.ShowContextualGuidance();
        ConsoleUI.WriteSection("Quick commands");
        ConsoleUI.WriteCommand("Interactive conversation", "srectl chat");
        ConsoleUI.WriteCommand("Comprehensive help", "srectl help");
        ConsoleUI.WriteCommand("Workspace status", "srectl status");
        Console.WriteLine();
    }

    public static async Task ShowWorkspaceStatus()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ConsoleUI.WriteSection("Workspace Status Check");
        ConsoleUI.DrawLine();

        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        var configStatus = hasConfig ? "Configured" : "Not configured";
        ConsoleUI.WriteStatus(hasConfig, $"Configuration: {configStatus}");

        if (hasConfig)
        {
            try
            {
                var config = await configService.LoadConfigurationAsync();
                ConsoleUI.WriteBullet($"Server URL: {config?.ResourceUrl ?? "Unknown"}");
                ConsoleUI.WriteBullet($"Auth Required: {config?.AuthRequired ?? false}");
            }
            catch
            {
                ConsoleUI.WriteStatus(false, "Configuration file corrupted");
            }
        }

        var agentCount = Directory.Exists("agents") ? Directory.GetDirectories("agents").Length : 0;
        var toolCount = Directory.Exists("tools") ? Directory.GetDirectories("tools").Length : 0;

        ConsoleUI.WriteInfo($"Agents: {agentCount} configured");
        ConsoleUI.WriteInfo($"Tools: {toolCount} configured");

        bool serverConnected = false;
        bool remoteAgentsAvailable = false;
        bool remoteToolsAvailable = false;

        if (hasConfig)
        {
            ProgressService.AnimatedSpinner.Start("Testing server connection");

            try
            {
                var config = await configService.LoadConfigurationAsync();
                if (config == null)
                {
                    ProgressService.AnimatedSpinner.Stop();
                    ProgressService.ShowError("No configuration found. Please run 'srectl init' first.");
                    return;
                }

                using var apiService = new ApiService();
                var (success, response) = await apiService.TestConnectionAsync(config.ResourceUrl);

                ProgressService.AnimatedSpinner.Stop();
                if (success)
                {
                    serverConnected = true;
                    ConsoleUI.WriteStatus(true, "Server Connection: Connected");

                    var (agents, agentError) = await apiService.ListExtendedAgentsAsync();
                    var (toolsSuccess, _) = await apiService.ListToolsAsync();

                    var agentsSuccess = agentError == null;
                    remoteAgentsAvailable = agentsSuccess;
                    remoteToolsAvailable = toolsSuccess;

                    if (agentsSuccess) ConsoleUI.WriteBullet("Remote Agents: Available");
                    if (toolsSuccess) ConsoleUI.WriteBullet("Remote Tools: Available");
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                    ConsoleUI.WriteBullet($"Error: {response}");
                }
            }
            catch (Exception ex)
            {
                ProgressService.AnimatedSpinner.Stop();
                ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                ConsoleUI.WriteBullet($"Error: {ex.Message}");
            }
        }

        stopwatch.Stop();
        ProgressService.ShowTiming("Status check", stopwatch.Elapsed);

        Console.WriteLine();
        ConsoleUI.WriteSection("Suggested next steps");

        if (!hasConfig)
        {
            ConsoleUI.WriteBullet("srectl init --resource-url <your-server>");
        }
        else if (agentCount == 0)
        {
            ConsoleUI.WriteBullet("srectl agent create --smart");
            ConsoleUI.WriteBullet("srectl help quickstart");
        }
        else
        {
            ConsoleUI.WriteBullet("srectl chat");
            ConsoleUI.WriteBullet("srectl agent test --name <agent>");

            // Provide appropriate list commands based on connection status
            if (serverConnected && remoteAgentsAvailable)
            {
                ConsoleUI.WriteBullet("srectl list agents  # List remote agents from server");
            }
            else if (serverConnected)
            {
                ConsoleUI.WriteBullet("srectl list agents  # Check remote agents (server connected)");
            }
            else
            {
                ConsoleUI.WriteBullet("srectl agent list  # List local agent configurations");
                if (hasConfig)
                {
                    ConsoleUI.WriteBullet("srectl list agents  # List remote agents (requires server connection)");
                }
            }
        }
    }

    private static Task ShowFormattedAgentHelp(Command agentCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Agent Lifecycle"] = ["create", "validate", "apply", "delete"],
            ["Agent Testing"] = ["test"],
            ["Agent Management"] = ["list", "diff"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Agent Lifecycle"] = "Create, validate, deploy, and remove agents",
            ["Agent Testing"] = "Test and validate agent functionality",
            ["Agent Management"] = "List, discover, and compare deployed agents"
        };

        var examples = new[]
        {
            "srectl agent create --name DevOpsAgent --smart",
            "srectl agent validate --all",
            "srectl agent test --name MyAgent --message \"Hello\"",
            "srectl agent diff --name MyAgent",
            "srectl agent list"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Agent Commands",
            "Manage SRE automation agents",
            ConsoleColor.Green,
            agentCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedToolHelp(Command toolCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Tool Lifecycle"] = ["create", "validate", "apply", "delete"],
            ["Tool Discovery"] = ["show-types", "show-connectors"],
            ["Tool Management"] = ["list", "diff"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Tool Lifecycle"] = "Create, validate, deploy, and remove automation tools",
            ["Tool Discovery"] = "Explore available tool types and data connectors",
            ["Tool Management"] = "List, discover, and compare deployed tools"
        };

        var examples = new[]
        {
            "srectl tool create --name QueryMetrics --type KustoTool --connector my-connector --database MyDB --parameter limit",
            "srectl tool create --name GenerateLink --type LinkTool --template \"https://example.com/{id}\" --parameter id",
            "srectl tool validate --all",
            "srectl tool diff --name QueryMetrics",
            "srectl tool show-types",
            "srectl tool list"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Tool Commands",
            "Manage SRE automation tools",
            ConsoleColor.Blue,
            toolCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedDocHelp(Command docCommand)
    {
        var examples = new[]
        {
            "srectl doc upload --file runbook.md",
            "srectl doc search --query \"restart web services\"",
            "srectl doc reindex"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Document Commands",
            "Manage documentation and knowledge base",
            ConsoleColor.Magenta,
            docCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedProfileHelp(Command profileCommand)
    {
        var examples = new[]
        {
            "srectl profile list",
            "srectl profile create --name prod --resource-url https://prod.example.com",
            "srectl profile set --name local-dev"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Profile Commands",
            "Manage connection profiles for different SRE Agent instances",
            ConsoleColor.Cyan,
            profileCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedSkillHelp(Command skillCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Skill Creation"] = ["create", "convert"],
            ["Skill Lifecycle"] = ["upload", "download", "delete"],
            ["Skill Discovery"] = ["list"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Skill Creation"] = "Create new skills or convert existing agents to skills",
            ["Skill Lifecycle"] = "Upload, download, and remove custom skills",
            ["Skill Discovery"] = "List and discover available skills"
        };

        var examples = new[]
        {
            "srectl skill create --name my-skill",
            "srectl skill convert --agent-name my-agent",
            "srectl skill upload --path skills/my-skill",
            "srectl skill upload --folder skills",
            "srectl skill list",
            "srectl skill list --search database",
            "srectl skill download --name my-skill",
            "srectl skill delete --name my-skill"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Skill Commands",
            "Manage custom skills for agents",
            ConsoleColor.Magenta,
            skillCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedListHelp(Command listCommand)
    {
        var examples = new[]
        {
            "srectl list agents",
            "srectl list extended-tools",
            "srectl list data-connectors",
            "srectl list incidenthandlers",
            "srectl agent list  # Alternative agent listing",
            "srectl tool list   # Alternative tool listing"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "List Commands",
            "List resources from the remote server",
            ConsoleColor.DarkYellow,
            listCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedThreadHelp(Command threadCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Thread Management"] = ["new", "continue", "list", "delete", "track"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Thread Management"] = "Create, manage, and track conversation threads with agents"
        };

        var examples = new[]
        {
            "srectl thread new --message \"Help me debug an issue\"",
            "srectl thread continue --thread-id abc123 --message \"More details\"",
            "srectl thread list",
            "srectl thread track --thread-id abc123"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Thread Commands",
            "Manage conversation threads with SRE Agent",
            ConsoleColor.DarkCyan,
            threadCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedIncidentHandlerHelp(Command incidentHandlerCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Incident Management"] = ["create", "map-agent", "list"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Incident Management"] = "Create filters, map agents, and manage incident handlers"
        };

        var examples = new[]
        {
            "srectl incidenthandler create --id MyFilter --name \"Production Issues\" --title-contains \"production\"",
            "srectl incidenthandler map-agent --name MyFilter --handling-agent ProductionAgent",
            "srectl incidenthandler list --verbose"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Incident Handler Commands",
            "Manage incident handlers and filters for automated incident response",
            ConsoleColor.Red,
            incidentHandlerCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }


    private static Task ShowFormattedScheduledTaskHelp(Command scheduledTaskCommand)
    {
        var commandGroups = new Dictionary<string, string[]>
        {
            ["Task Management"] = ["create", "list", "get"],
            ["Task Control"] = ["pause", "resume", "delete"]
        };

        var groupDescriptions = new Dictionary<string, string>
        {
            ["Task Management"] = "Create and view scheduled tasks",
            ["Task Control"] = "Control execution and lifecycle of scheduled tasks"
        };

        var examples = new[]
        {
            "srectl scheduledtask create --name \"Daily Check\" --cron \"0 9 * * *\" --prompt \"Check system status\"",
            "srectl scheduledtask create --name \"Agent Check\" --cron \"0 10 * * *\" --prompt \"Run checks\" --agent \"MyAgent\"",
            "srectl scheduledtask list --verbose",
            "srectl scheduledtask pause --id task-123",
            "srectl scheduledtask resume --id task-123"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Scheduled Task Commands",
            "Manage scheduled tasks for automated agent operations and monitoring",
            ConsoleColor.Blue,
            scheduledTaskCommand,
            commandGroups,
            groupDescriptions,
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedApplyYamlHelp(Command applyYamlCommand)
    {
        var examples = new[]
        {
            "srectl apply-yaml --file agents/MyAgent/MyAgent.yaml",
            "srectl apply-yaml --file tools/CustomTool/CustomTool.yaml",
            "srectl apply-yaml --file configs/my-config.yaml"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Apply YAML Commands",
            "Apply YAML configuration files to the server",
            ConsoleColor.Green,
            applyYamlCommand,
            null, // Single command
            null, // No group descriptions for single command
            examples);

        return Task.CompletedTask;
    }

    private static Task ShowFormattedExtensionHelp(Command extensionCommand)
    {
        var examples = new[]
        {
            "srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./ev2-output",
            "srectl extension generate-ev2 --tools-folder ./tools --agent-folder ./agents --output ./deployment --debug"
        };

        StandardHelpFormatter.ShowCommandGroupHelp(
            "Extension Commands",
            "Generate deployment files and configurations for SRE Agent deployments",
            ConsoleColor.DarkGreen,
            extensionCommand,
            null, // Single group for all commands
            null, // No group descriptions for single group
            examples);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a validator to the command with exception handling.
    /// Wraps the validator in a try-catch to handle any exceptions during validation,
    /// logging debug information but swallow the exception.
    /// </summary>
    /// <param name="command">The command to add the validator to.</param>
    /// <param name="validator">The validation logic to execute.</param>
    public static void AddValidator(this Command command, Action<System.CommandLine.Parsing.CommandResult> validator)
    {
        command.Validators.Add(result =>
        {
            try
            {
                validator(result);
            }
            catch (Exception ex)
            {
                // Log the exception details in debug mode
                DebugLogger.Debug("Validation", $"Exception: {ex.GetType().Name}: {ex.Message}");
                DebugLogger.Debug("Validation", $"Stack trace: {ex.StackTrace}");
            }
        });
    }
}
