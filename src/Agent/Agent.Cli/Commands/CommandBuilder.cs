// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Invocation;
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
        var interactiveCommand = CreateInteractiveCommand();

        // ----- Root
        var root = new RootCommand("SRE Agent CLI - Your intelligent assistant for managing SRE agents and automating incident response")
        {
            // Commands
            WelcomeCommand.Build(),
            VersionCommand.Build(),
            InitCommand.Build(),
            StatusCommand.Build(),
            ApplyYamlCommand.Build(),
            
            // Subgroups
            AgentCommand.Build(),
            ToolCommand.Build(),
            CommonPromptCommand.Build(),
            ExtensionCommand.Build(),
            McpCommand.Build(),

            // Other commands            
            interactiveCommand,
            syncCommand,
            ThreadCommand.Build(),
            chatCommand,
            DocumentCommand.Build(),
            ProfileCommand.Build(),
            SkillCommand.Build(),
            IncidentHandlerCommand.Build(),
            ScheduledTaskCommand.Build(),

        };

        // Single root action (runs when no verb provided)
        root.SetAction(HandleRootCommand);

        // Simulate global options on all commands (older System.CommandLine lacks AddGlobalOption)
        root.AddGlobalOptionsCompat(GlobalOptions.DebugOption, GlobalOptions.QuietOption);

        // Customize help and version output for root command
        CustomizeRootCommandOptions(root);

        return root;
    }

    private static Command CreateSyncCommand()
    {
        var cmd = new Command("sync", CommandExamples.General.SyncDescription);

        cmd.SetAction(GeneralCommandHandlers.HandleSyncCommand);
        return cmd;
    }

    private static Command CreateChatCommand()
    {
        var cmd = new Command("chat", CommandExamples.General.ChatDescription)
        {
            ThreadCommandOptions.New.AgentNameOption
        };
        cmd.SetAction(GeneralCommandHandlers.HandleChatCommand);
        return cmd;
    }

    private static Command CreateInteractiveCommand()
    {
        var cmd = new Command("interactive", "Start interactive guided mode for step-by-step assistance");
        cmd.SetAction(InteractiveCommandHandlers.HandleInteractiveMode);
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
        await GeneralCommandHandlers.HandleStatusCommand(null!);
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
    /// Customizes the help and version output for the root command
    /// Reference: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-customize-help
    /// </summary>
    private static void CustomizeRootCommandOptions(RootCommand root)
    {
        for (int i = 0; i < root.Options.Count; i++)
        {
            // Customize HelpOption
            if (root.Options[i] is System.CommandLine.Help.HelpOption defaultHelpOption)
            {
                var defaultHelpAction = (System.CommandLine.Help.HelpAction)defaultHelpOption.Action!;
                defaultHelpOption.Action = new CustomHelpAction(defaultHelpAction);
            }
            // Customize VersionOption
            else if (root.Options[i] is System.CommandLine.VersionOption defaultVersionOption)
            {
                defaultVersionOption.Action = new CustomVersionAction();
            }
        }
    }

    /// <summary>
    /// Custom help action following Microsoft documentation pattern
    /// </summary>
    internal class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly System.CommandLine.Help.HelpAction _defaultHelp;

        public CustomHelpAction(System.CommandLine.Help.HelpAction defaultHelp) => _defaultHelp = defaultHelp;

        public override int Invoke(ParseResult parseResult)
        {
            // Only customize help for root command, use default for others
            if (parseResult.CommandResult.Command is not RootCommand root)
            {
                return _defaultHelp.Invoke(parseResult);
            }

            // Custom help for root command
            ShowCustomRootHelp(root);
            return 0;
        }

        private void ShowCustomRootHelp(RootCommand? root)
        {
            if (root == null) return;

            // Show description
            ConsoleUI.Write("Description:", ConsoleColor.White);
            var description = root.Description ?? "";
            foreach (var line in description.Split('\n'))
            {
                ConsoleUI.Write($"  {line}");
            }
            ConsoleUI.Write("");

            // Show usage
            ConsoleUI.Write("Usage:", ConsoleColor.White);
            ConsoleUI.Write($"  {root.Name} <command> [options]");
            ConsoleUI.Write($"  {root.Name} <subgroup> <command> [options]");
            ConsoleUI.Write("");

            // Show options
            ConsoleUI.Write("Options:", ConsoleColor.White);

            foreach (var option in root.Options)
            {
                // Get aliases or fall back to option name
                var aliases = option.Aliases.Count > 0
                    ? string.Join(", ", option.Aliases)
                    : option.Name;

                var optionDescription = option.Description ?? "";

                // Handle multi-line option descriptions
                var lines = optionDescription.Split('\n');
                if (lines.Length > 0)
                {
                    ConsoleUI.Write($"  {aliases,-15} {lines[0]}");
                    for (int i = 1; i < lines.Length; i++)
                    {
                        ConsoleUI.Write($"                   {lines[i]}");
                    }
                }
                else
                {
                    ConsoleUI.Write($"  {aliases,-15} {optionDescription}");
                }
            }
            ConsoleUI.Write("");

            // Categorize commands
            var subgroups = new List<Command>();
            var commands = new List<Command>();

            foreach (var cmd in root.Children.OfType<Command>())
            {
                // Check if command has subcommands
                if (cmd.Children.OfType<Command>().Any())
                {
                    subgroups.Add(cmd);
                }
                else
                {
                    commands.Add(cmd);
                }
            }

            // Calculate dynamic width based on longest command alias string
            int maxWidth = 15; // Default minimum width
            foreach (var cmd in subgroups.Concat(commands))
            {
                var cmdAliases = cmd.Name;
                if (cmd.Aliases.Count > 0)
                {
                    var otherAliases = cmd.Aliases.Where(a => a != cmd.Name);
                    if (otherAliases.Any())
                    {
                        cmdAliases = $"{cmd.Name}, {string.Join(", ", otherAliases)}";
                    }
                }
                maxWidth = Math.Max(maxWidth, cmdAliases.Length + 1);
            }

            // Show Subgroups (commands with subcommands)
            if (subgroups.Any())
            {
                ConsoleUI.Write("Subgroups:", ConsoleColor.White);
                foreach (var cmd in subgroups)
                {
                    ConsoleUI.WriteCommand(cmd, maxWidth);
                }
                ConsoleUI.Write("");
            }

            // Show Commands (single-layer commands)
            if (commands.Any())
            {
                ConsoleUI.Write("Commands:", ConsoleColor.White);
                foreach (var cmd in commands)
                {
                    ConsoleUI.WriteCommand(cmd, maxWidth);
                }
            }
        }
    }

    /// <summary>
    /// Custom version action that behaves the same as 'srectl version' command
    /// </summary>
    internal class CustomVersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            // Call the same handler as 'srectl version' command
            GeneralCommandHandlers.HandleVersionCommand(parseResult).Wait();
            return 0;
        }
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
