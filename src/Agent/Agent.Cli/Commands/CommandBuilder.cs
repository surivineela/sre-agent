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

        // Add global options (recursive = true)
        root.Add(GlobalOptions.DebugOption);
        root.Add(GlobalOptions.QuietOption);

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
