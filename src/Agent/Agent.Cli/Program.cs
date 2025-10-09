// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Parsing;
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
                ShowCustomHelp(isFirstTime: !hasConfig, includeHeader: true);
                return 0;
            }

            var root = CommandBuilder.BuildCommands();
            var parseResult = root.Parse(args);

            // Initialize debug mode globally before any command execution
            CommandExecutionContext.Initialize(parseResult);

            // Parse errors (unrecognized, missing subcommand, missing required options, etc.)
            if (parseResult.Errors.Count > 0)
            {
                var unmatched = parseResult.UnmatchedTokens;

                bool hasRequiredMissingSubcommand = parseResult.Errors.Any(e =>
                    e.Message.Contains("Required command was not provided", StringComparison.OrdinalIgnoreCase));

                bool hasUnrecognized = parseResult.Errors.Any(e =>
                    e.Message.Contains("Unrecognized command or argument", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("Required command was not provided", StringComparison.OrdinalIgnoreCase));

                bool hasMissingOrInvalidOptions = parseResult.Errors.Any(e =>
                    e.Message.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("requires", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("expects", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("Option", StringComparison.OrdinalIgnoreCase) && e.Message.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                    e.Message.Contains("argument", StringComparison.OrdinalIgnoreCase) && e.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));

                // Help the common "positional instead of named" mistake
                if (args.Length >= 3 && hasUnrecognized)
                {
                    var commandGroup = args[0]; // e.g., "profile", "agent", "tool"
                    var subCommand = args[1];  // e.g., "set", "create", "delete", "list"
                    var possibleVal = args[2];  // e.g., "local", "myagent", "mytool"

                    if (!possibleVal.StartsWith("-", StringComparison.Ordinal))
                    {
                        var suggestion = GetCommonCommandSuggestion(commandGroup, subCommand, possibleVal);
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

                // Unrecognized tokens: show which ones, offer a close match, then scoped suggestions if possible
                if (unmatched is { Count: > 0 })
                {
                    StandardHelpFormatter.ShowSrectlHeader();
                    var joined = string.Join(", ", unmatched.Select(t => $"'{t}'"));
                    ConsoleUI.WriteStatus(false, $"Unrecognized command or argument{(unmatched.Count > 1 ? "s" : "")}: {joined}");
                    Console.WriteLine();

                    // Try single-token “plural” correction (e.g., 'agents' -> 'agent')
                    if (unmatched.Count == 1)
                    {
                        var maybe = TrySingularize(unmatched[0]);
                        var close = SuggestClosestTopLevel(root, unmatched[0]) ?? SuggestClosestTopLevel(root, maybe);
                        if (!string.IsNullOrWhiteSpace(close))
                        {
                            ConsoleUI.WriteBullet($"Did you mean: srectl {close} … ?");
                            Console.WriteLine();
                        }
                    }

                    var deepest = GetDeepestMatchedCommandResult(parseResult);
                    if (deepest?.Command is Command matched && matched is not RootCommand)
                    {
                        ShowScopedSuggestions(matched);
                        Console.WriteLine();
                        ConsoleUI.WriteBullet($"Use 'srectl {GetCommandPath(deepest)} --help' for details");
                        Console.WriteLine();
                        return 1;
                    }

                    // Fall back to top-level help without re-printing header twice
                    var configService = new CliConfigurationService();
                    var hasConfig = await configService.HasValidConfigurationAsync();
                    ShowCustomHelp(isFirstTime: !hasConfig, includeHeader: false);
                    return 1;
                }

                // Subcommand required but missing (e.g., `srectl agent`)
                if (hasRequiredMissingSubcommand)
                {
                    var deepest = GetDeepestMatchedCommandResult(parseResult);
                    if (deepest?.Command is Command needSub && needSub.Subcommands.Any())
                    {
                        StandardHelpFormatter.ShowSrectlHeader();
                        ConsoleUI.WriteStatus(false, $"'{GetCommandPath(deepest)}' expects a subcommand.");
                        Console.WriteLine();
                        ShowScopedSuggestions(needSub);
                        Console.WriteLine();
                        ConsoleUI.WriteBullet($"Use 'srectl {GetCommandPath(deepest)} --help' for details");
                        Console.WriteLine();
                        return 1;
                    }
                }

                // We matched a specific command but options are missing/invalid (e.g., `srectl agent test`)
                if (hasMissingOrInvalidOptions)
                {
                    var deepest = GetDeepestMatchedCommandResult(parseResult);
                    if (deepest?.Command is Command cmd && cmd is not RootCommand)
                    {
                        StandardHelpFormatter.ShowSrectlHeader();
                        ConsoleUI.WriteStatus(false, $"Missing or invalid options for '{GetCommandPath(deepest)}'.");
                        Console.WriteLine();
                        ShowScopedSuggestions(cmd);
                        Console.WriteLine();
                        ConsoleUI.WriteBullet($"Use 'srectl {GetCommandPath(deepest)} --help' to see all options and examples");
                        Console.WriteLine();
                        return 1;
                    }
                }

                // Generic fallback (rare)
                {
                    var configService = new CliConfigurationService();
                    var hasConfig = await configService.HasValidConfigurationAsync();
                    ShowCustomHelp(isFirstTime: !hasConfig, includeHeader: true);
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

    private static void ShowCustomHelp(bool isFirstTime = false, bool includeHeader = true)
    {
        if (includeHeader) StandardHelpFormatter.ShowSrectlHeader();

        ConsoleUI.WriteSection("Usage", bottomMargin: true);
        Console.WriteLine("  srectl [command] [options]");
        Console.WriteLine();

        ConsoleUI.WriteSection("Global Options");
        ConsoleUI.WriteKeyValue("-?, -h, --help", "Show help and usage information", 20, ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("--debug", "Enable debug logging", 20, ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("--quiet", "Minimize output", 20, ConsoleColor.Green);
        Console.WriteLine();

        ConsoleUI.WriteCommandGroup("Core Commands", new[] {
            ("help", "Interactive help with examples and troubleshooting"),
            ("status", "Connection and workspace status"),
            ("interactive", "Interactive guided mode (recommended to get started)"),
            ("version", "Version and build details"),
            ("welcome", "Welcome screen and getting-started guide"),
        });

        ConsoleUI.WriteCommandGroup("Setup & Management", new[] {
            ("init", "Initialize SREAgent CLI configuration and workspace"),
            ("sync", "Sync agents and tools YAML from remote into local workspace"),
            ("list", "List various resources from the remote server"),
            ("apply-yaml", "Apply any YAML configuration file to the server")
        });

        ConsoleUI.WriteCommandGroup("Interaction & Workflows", new[] {
            ("chat", "Start an interactive chat session with the SRE Agent"),
            ("thread", "Thread management commands")
        });

        ConsoleUI.WriteCommandGroup("Resource Management", new[] {
            ("agent", "Manage user-authored, extensible agents"),
            ("tool", "Manage user-authored, reusable tools"),
            ("doc", "Upload and manage documents like TSGs, architecture docs, and runbooks"),
            ("profile", "Switch between SRE Agent instances (multiple local/remote profiles supported)"),
            ("incidenthandler", "Manage incident handlers and filters"),
            ("scheduledtask", "Manage scheduled tasks for automated agent operations")
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
            Console.WriteLine();
        }

        ConsoleUI.WriteSection("More Information");
        ConsoleUI.WriteCommand("Command examples", "srectl help <command>");
        ConsoleUI.WriteCommand("Interactive mode", "srectl interactive");
        ConsoleUI.WriteCommand("Workspace status", "srectl status");
        Console.WriteLine();
    }

    /// <summary>Provides intelligent suggestions for common command syntax mistakes</summary>
    private static string GetCommonCommandSuggestion(string commandGroup, string subCommand, string possibleValue)
    {
        return (commandGroup.ToLower(), subCommand.ToLower()) switch
        {
            // Profile
            ("profile", "set") => $"srectl profile set --name {possibleValue}",
            ("profile", "create") => $"srectl profile create --name {possibleValue} --url <server-url>",
            ("profile", "delete") => $"srectl profile delete --name {possibleValue}",
            ("profile", "get") => $"srectl profile get --name {possibleValue}",

            // Agent
            ("agent", "create") => $"srectl agent create --name {possibleValue}",
            ("agent", "apply") => $"srectl agent apply --name {possibleValue}",
            ("agent", "delete") => $"srectl agent delete --name {possibleValue}",
            ("agent", "test") => $"srectl agent test --name {possibleValue} --message \"…\"",

            // Tool
            ("tool", "create") => $"srectl tool create --name {possibleValue}",
            ("tool", "apply") => $"srectl tool apply --name {possibleValue}",
            ("tool", "delete") => $"srectl tool delete --name {possibleValue}",

            // Doc
            ("doc", "upload") => $"srectl doc upload --file {possibleValue}",

            // Thread
            ("thread", "track") => $"srectl thread track --id {possibleValue}",

            _ => ""
        };
    }

    // ---- helpers for better error messages / scoped help ----

    private static CommandResult GetDeepestMatchedCommandResult(ParseResult result)
    {
        CommandResult current = result.CommandResult;
        while (true)
        {
            var next = current.Children.OfType<CommandResult>().LastOrDefault();
            if (next is null) return current;
            current = next;
        }
    }

    private static string GetCommandPath(SymbolResult result)
    {
        var parts = new System.Collections.Generic.Stack<string>();
        for (var r = result; r is not null; r = r.Parent)
        {
            if (r is CommandResult cr && cr.Command is not RootCommand)
                parts.Push(cr.Command.Name);
        }
        return string.Join(' ', parts);
    }

    private static void ShowScopedSuggestions(Command cmd)
    {
        if (cmd.Subcommands.Any())
        {
            ConsoleUI.WriteSection($"Valid subcommands for '{cmd.Name}'");
            foreach (var sc in cmd.Subcommands)
                ConsoleUI.WriteKeyValue($"srectl {cmd.Name} {sc.Name}", sc.Description ?? string.Empty, 28, ConsoleColor.Green);
            Console.WriteLine();
        }

        if (cmd.Options.Any())
        {
            ConsoleUI.WriteSection("Options");
            foreach (var opt in cmd.Options)
            {
                // Normalize display to the canonical long form --{name} to avoid duplication like "----name"
                var canonical = $"--{opt.Name}";
                ConsoleUI.WriteKeyValue(canonical, opt.Description ?? string.Empty, 28, ConsoleColor.Green);
            }
        }
    }

    private static string? SuggestClosestTopLevel(RootCommand root, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var cmds = root.Subcommands.Select(c => c.Name).ToArray();
        // Simple heuristic: prefer exact singularization, else starts-with, else shortest Levenshtein within threshold.
        if (cmds.Contains(token, StringComparer.OrdinalIgnoreCase)) return token;

        var singular = TrySingularize(token);
        var exact = cmds.FirstOrDefault(c => c.Equals(singular, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exact)) return exact;

        var starts = cmds.FirstOrDefault(c => token.StartsWith(c, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(starts)) return starts;

        // Tiny Levenshtein to keep things lightweight
        int Threshold = 2;
        string? best = null; int bestDist = int.MaxValue;
        foreach (var c in cmds)
        {
            int d = Lev(token, c);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return bestDist <= Threshold ? best : null;

        static int Lev(string a, string b)
        {
            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(
                        dp[i - 1, j] + 1,
                        dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            return dp[a.Length, b.Length];
        }
    }

    private static string? TrySingularize(string token)
        => token.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? token[..^1] : token;
}
