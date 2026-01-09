// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles the help command for displaying and exporting CLI documentation.
/// </summary>
public static class HelpCommandHandlers
{
    /// <summary>
    /// Handle the help command - show default help or export to markdown.
    /// </summary>
    public static Task<int> HandleHelpCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting help command");

        var outputFile = parseResult.GetValue(HelpCommandOptions.OutputOption);

        DebugLogger.Debug("Parameters", $"OutputFile: {outputFile ?? "(none)"}");

        if (string.IsNullOrWhiteSpace(outputFile))
        {
            // Default behavior: show the same help as `srectl --help`
            return ShowDefaultHelp(parseResult);
        }

        // Export help to markdown file
        return ExportHelpToMarkdown(outputFile);
    }

    /// <summary>
    /// Shows the same help output as `srectl --help`.
    /// </summary>
    private static Task<int> ShowDefaultHelp(ParseResult parseResult)
    {
        // Get the root command from the parse result
        var rootCommand = parseResult.RootCommandResult.Command as RootCommand;
        if (rootCommand == null)
        {
            ConsoleUI.WriteStatus(false, "Unable to access root command for help.");
            return Task.FromResult(1);
        }

        // Call the same custom help logic used by --help
        CommandBuilder.ShowRootHelp(rootCommand);

        return Task.FromResult(0);
    }

    /// <summary>
    /// Exports help documentation to a markdown file.
    /// </summary>
    private static Task<int> ExportHelpToMarkdown(string outputFile)
    {
        try
        {
            // Build the root command to traverse all commands
            var rootCommand = CommandBuilder.BuildCommands();

            var sb = new StringBuilder();
            sb.AppendLine("# SRECTL Command Reference");
            sb.AppendLine();
            sb.AppendLine("A comprehensive reference guide for all SRECTL commands, parameters, and usage patterns.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Process all commands
            foreach (var cmd in rootCommand.Children.OfType<Command>())
            {
                // Skip hidden commands
                if (cmd.Hidden)
                    continue;

                var hasSubcommands = cmd.Children.OfType<Command>().Any(c => !c.Hidden);

                if (hasSubcommands)
                {
                    // This is a command group (e.g., agent, tool)
                    WriteCommandGroup(sb, cmd);
                }
                else
                {
                    // This is a standalone command (e.g., init, status)
                    WriteStandaloneCommand(sb, cmd);
                }
            }

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);

            ConsoleUI.WriteStatus(true, $"Help documentation exported to: {outputFile}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to export help: {ex.Message}");
            DebugLogger.Debug("Help", $"Export error: {ex}");
            return Task.FromResult(1);
        }
    }

    /// <summary>
    /// Writes a standalone command (no subcommands) to markdown.
    /// </summary>
    private static void WriteStandaloneCommand(StringBuilder sb, Command cmd)
    {
        sb.AppendLine($"## `srectl {cmd.Name}`");
        sb.AppendLine();

        // Write description (first line only, before examples)
        WriteDescription(sb, cmd.Description);

        // Write examples from description
        WriteExamplesFromDescription(sb, cmd.Description);

        // Write parameters
        WriteParameters(sb, cmd);
    }

    /// <summary>
    /// Writes a command group with subcommands to markdown.
    /// </summary>
    private static void WriteCommandGroup(StringBuilder sb, Command cmd)
    {
        sb.AppendLine($"## `srectl {cmd.Name}`");
        sb.AppendLine();

        // Write group description
        WriteDescription(sb, cmd.Description);

        // Build command table
        var subcommands = cmd.Children.OfType<Command>().Where(c => !c.Hidden).ToList();
        if (subcommands.Any())
        {
            sb.AppendLine("**Commands**");
            sb.AppendLine();
            sb.AppendLine("| Name | Description |");
            sb.AppendLine("|------|-------------|");

            foreach (var subcmd in subcommands)
            {
                var shortDescription = GetShortDescription(subcmd.Description);
                var anchor = $"srectl-{cmd.Name}-{subcmd.Name}".Replace(" ", "-").ToLowerInvariant();
                sb.AppendLine($"| [`srectl {cmd.Name} {subcmd.Name}`](#{anchor})|{shortDescription}|");
            }

            sb.AppendLine();
        }

        // Write each subcommand
        foreach (var subcmd in subcommands)
        {
            WriteSubcommand(sb, cmd.Name, subcmd);
        }
    }

    /// <summary>
    /// Writes a subcommand to markdown.
    /// </summary>
    private static void WriteSubcommand(StringBuilder sb, string parentName, Command subcmd)
    {
        sb.AppendLine($"### `srectl {parentName} {subcmd.Name}`");
        sb.AppendLine();

        // Write description (first line only)
        WriteDescription(sb, subcmd.Description);

        // Write examples from description
        WriteExamplesFromDescription(sb, subcmd.Description);

        // Write parameters
        WriteParameters(sb, subcmd);
    }

    /// <summary>
    /// Writes the description (first line or text before Examples:).
    /// </summary>
    private static void WriteDescription(StringBuilder sb, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        // Get description text before "Examples:" section
        var examplesIndex = description.IndexOf("Examples:", StringComparison.OrdinalIgnoreCase);
        var descriptionText = examplesIndex > 0 
            ? description.Substring(0, examplesIndex).Trim()
            : description.Split('\n')[0].Trim();

        if (!string.IsNullOrWhiteSpace(descriptionText))
        {
            sb.AppendLine(descriptionText);
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Extracts and writes the examples section from a command description.
    /// </summary>
    private static void WriteExamplesFromDescription(StringBuilder sb, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;

        var examplesIndex = description.IndexOf("Examples:", StringComparison.OrdinalIgnoreCase);
        if (examplesIndex < 0)
            return;

        sb.AppendLine("**Examples:**");
        sb.AppendLine("```bash");

        // Extract examples section
        var examplesText = description.Substring(examplesIndex + "Examples:".Length);
        // Normalize line endings and split
        var lines = examplesText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        bool isFirstLine = true;
        bool needsBlankBeforeComment = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Mark that we might need a blank line before the next comment
                if (!isFirstLine)
                    needsBlankBeforeComment = true;
                continue;
            }

            // Add blank line before a new comment (new example block), but not the first one
            if (trimmedLine.StartsWith("#") && needsBlankBeforeComment)
            {
                sb.AppendLine();
            }

            sb.AppendLine(trimmedLine);
            isFirstLine = false;
            needsBlankBeforeComment = false;
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    /// <summary>
    /// Writes command parameters to markdown.
    /// </summary>
    private static void WriteParameters(StringBuilder sb, Command cmd)
    {
        var options = cmd.Options.Where(o => o is not System.CommandLine.Help.HelpOption).ToList();
        
        if (!options.Any())
            return;

        var requiredOptions = options.Where(o => o.Required).ToList();
        var optionalOptions = options.Where(o => !o.Required).ToList();

        if (requiredOptions.Any())
        {
            sb.AppendLine("**Required Parameters**");
            sb.AppendLine();

            foreach (var option in requiredOptions)
            {
                WriteOption(sb, option);
            }

            sb.AppendLine();
        }

        if (optionalOptions.Any())
        {
            sb.AppendLine("**Optional Parameters**");
            sb.AppendLine();

            foreach (var option in optionalOptions)
            {
                WriteOption(sb, option);
            }

            sb.AppendLine();
        }
    }

    /// <summary>
    /// Writes a single option to markdown.
    /// </summary>
    private static void WriteOption(StringBuilder sb, Option option)
    {
        // Combine Name and Aliases, then sort: long form (--) first, then short form (-)
        var allNames = new List<string> { option.Name };
        allNames.AddRange(option.Aliases);
        
        var sortedAliases = allNames
            .Distinct()
            .OrderByDescending(a => a.StartsWith("--"))
            .ThenBy(a => a.Length)
            .Select(a => $"`{a}`");

        var aliasesStr = string.Join(", ", sortedAliases);

        var description = option.Description ?? "";
        
        // Clean up description - remove newlines and extra spaces
        description = string.Join(" ", description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));

        sb.AppendLine($"- {aliasesStr}: {description}");
    }

    /// <summary>
    /// Gets a short description (first line only).
    /// </summary>
    private static string GetShortDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "";

        var firstLine = description.Split('\n')[0].Trim();
        
        // Truncate if too long
        if (firstLine.Length > 80)
            firstLine = firstLine.Substring(0, 77) + "...";

        return firstLine;
    }
}
