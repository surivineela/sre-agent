// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq;
using Agent.Cli.Commands;
using Agent.Cli.Services;
using Agent.Cli.Helpers;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // First-run UX when user calls just `srectl`
            if (args.Length == 0)
            {
                var configService = new CliConfigurationService();
                var hasConfig = await configService.HasValidConfigurationAsync();
                ShowCustomHelp(isFirstTime: !hasConfig);
                return 0;
            }

            var root = CommandBuilder.BuildCommands();
            var parseResult = root.Parse(args);

            // Check for parsing errors (like unrecognized commands) and show custom help instead
            if (parseResult.Errors.Count > 0)
            {
                // Check if it's an unrecognized command error
                var hasUnrecognizedCommand = parseResult.Errors.Any(error =>
                    error.Message.Contains("Unrecognized command or argument") ||
                    error.Message.Contains("Required command was not provided"));

                // Generic handling for common command mistakes - when user provides positional args instead of named options
                if (args.Length >= 3 && hasUnrecognizedCommand)
                {
                    var commandGroup = args[0]; // e.g., "profile", "agent", "tool"
                    var subCommand = args[1];   // e.g., "set", "create", "delete", "list"
                    var possibleValue = args[2]; // e.g., "local", "myagent", "mytool"

                    // Don't show this for commands that start with dashes (already proper options)
                    if (!possibleValue.StartsWith("-"))
                    {
                        var suggestion = GetCommonCommandSuggestion(commandGroup, subCommand, possibleValue);
                        if (!string.IsNullOrEmpty(suggestion))
                        {
                            StandardHelpFormatter.ShowSrectlHeader();
                            ConsoleUI.WriteStatus(false, $"Invalid syntax for '{commandGroup} {subCommand}' command");
                            Console.WriteLine();
                            ConsoleUI.WriteBullet($"Try: {suggestion}");
                            Console.WriteLine();
                            ConsoleUI.WriteBullet($"Use 'srectl {commandGroup} {subCommand} --help' for more options");
                            Console.WriteLine();
                            return 1;
                        }
                    }
                }

                if (hasUnrecognizedCommand)
                {
                    // Show our custom help instead of the default error
                    var configService = new CliConfigurationService();
                    var hasConfig = await configService.HasValidConfigurationAsync();

                    // Show error message first
                    if (args.Length > 0)
                    {
                        ConsoleUI.WriteStatus(false, $"Unrecognized command or argument '{args[0]}'");
                        Console.WriteLine();
                    }

                    ShowCustomHelp(isFirstTime: !hasConfig);
                    return 1;
                }
            }

            return await parseResult.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            var handled = await SmartErrorHandler.HandleException(ex, "CLI execution", string.Join(" ", args));
            if (!handled)
            {
                Console.WriteLine("If this problem persists:");
                ConsoleUI.WriteBullet("Try 'srectl status' to check your setup");
                ConsoleUI.WriteBullet("Use 'srectl help troubleshooting' for common solutions");
                ConsoleUI.WriteBullet("Run with --debug for detailed error information");
            }
            return 1;
        }
    }

    private static void ShowCustomHelp(bool isFirstTime = false)
    {
        StandardHelpFormatter.ShowSrectlHeader();

        ConsoleUI.WriteSection("Usage");
        Console.WriteLine("  srectl [command] [options]");
        Console.WriteLine();

        ConsoleUI.WriteSection("Global Options");
        ConsoleUI.WriteKeyValue("-?, -h, --help", "Show help and usage information", 20, ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("--debug", "Enable debug logging", 20, ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("--quiet", "Minimize output", 20, ConsoleColor.Green);
        Console.WriteLine();

        ConsoleUI.WriteCommandGroup("Core Commands", new[] {
            ("welcome", "Show welcome screen and getting started guide"),
            ("help", "Interactive help system with examples and troubleshooting"),
            ("suggest", "Get intelligent command suggestions based on workspace"),
            ("status", "Show workspace status and health check"),
            ("interactive", "Start interactive guided mode for step-by-step assistance"),
            ("version", "Show version information and build details")
        });

        ConsoleUI.WriteCommandGroup("Setup & Management", new[] {
            ("init", "Initialize SREAgent CLI configuration and workspace"),
            ("list", "List various resources from the remote server"),
            ("apply-yaml", "Apply any YAML configuration file to the server")
        });

        ConsoleUI.WriteCommandGroup("Interaction & Workflows", new[] {
            ("chat", "Start an interactive chat session with the SRE Agent"),
            ("thread", "Thread management commands")
        });

        ConsoleUI.WriteCommandGroup("Resource Management", new[] {
            ("agent", "Agent commands"),
            ("tool", "Tool commands"),
            ("doc", "Upload and manage documents like TSGs, architecture docs, and runbooks"),
            ("profile", "Switch between different SRE Agent instances (local or remote)")
        });

        if (isFirstTime)
        {
            ConsoleUI.WriteSection("First-Time Setup");
            ConsoleUI.WriteCommand("Quick setup", "srectl interactive");
            ConsoleUI.WriteCommand("Manual setup", "srectl init --resource-url <your-server>");
            ConsoleUI.WriteCommand("Getting started", "srectl help quickstart");
            Console.WriteLine();
        }
        else
        {
            ConsoleUI.WriteSection("Quick Actions");
            ConsoleUI.WriteCommand("Start chatting", "srectl chat");
            ConsoleUI.WriteCommand("Check status", "srectl status");
            ConsoleUI.WriteCommand("Get suggestions", "srectl suggest");
            Console.WriteLine();
        }

        ConsoleUI.WriteSection("More Information");
        ConsoleUI.WriteCommand("Command examples", "srectl help <command>");
        ConsoleUI.WriteCommand("Interactive mode", "srectl interactive");
        ConsoleUI.WriteCommand("Workspace status", "srectl status");
        Console.WriteLine();
    }

    /// <summary>
    /// Provides intelligent suggestions for common command syntax mistakes
    /// </summary>
    private static string GetCommonCommandSuggestion(string commandGroup, string subCommand, string possibleValue)
    {
        return (commandGroup.ToLower(), subCommand.ToLower()) switch
        {
            // Profile commands
            ("profile", "set") => $"srectl profile set --name {possibleValue}",
            ("profile", "create") => $"srectl profile create --name {possibleValue} --url <server-url>",
            ("profile", "delete") => $"srectl profile delete --name {possibleValue}",
            ("profile", "get") => $"srectl profile get --name {possibleValue}",

            // Agent commands
            ("agent", "create") => $"srectl agent create --name {possibleValue}",
            ("agent", "apply") => $"srectl agent apply --name {possibleValue}",
            ("agent", "delete") => $"srectl agent delete --name {possibleValue}",
            ("agent", "test") => $"srectl agent test --name {possibleValue}",

            // Tool commands
            ("tool", "create") => $"srectl tool create --name {possibleValue}",
            ("tool", "apply") => $"srectl tool apply --name {possibleValue}",
            ("tool", "delete") => $"srectl tool delete --name {possibleValue}",

            // Doc commands
            ("doc", "upload") => $"srectl doc upload --file {possibleValue}",

            // Thread commands
            ("thread", "track") => $"srectl thread track --id {possibleValue}",

            // Default - return empty string for no suggestion
            _ => ""
        };
    }

}
