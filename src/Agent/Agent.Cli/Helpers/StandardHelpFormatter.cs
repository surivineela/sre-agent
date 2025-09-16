using System.CommandLine;
using System.Text.RegularExpressions;

namespace Agent.Cli.Helpers;

/// <summary>
/// Standard help formatter providing consistent UI across all command groups
/// </summary>
public static class StandardHelpFormatter
{
    /// <summary>
    /// Shows the standard srectl header used across all help screens
    /// </summary>
    public static void ShowSrectlHeader()
    {
        Console.WriteLine();
        var currentDir = Directory.GetCurrentDirectory();
        var bannerLines = new[]
        {
            "✻ SRE Agent CLI (srectl)",
            "",
            "  Your intelligent assistant for Incident Diagnosis and automation",
            "",
            $"  cwd: {currentDir}"
        };

        // Calculate the width needed for the banner
        var maxContentWidth = bannerLines.Max(line => line.Length);
        var totalWidth = Math.Max(maxContentWidth + 4, 80); // At least 80 chars wide
        var horizontalLine = new string(ConsoleUI.Chars.H[0], totalWidth - 2);

        ConsoleUI.WithColor(ConsoleColor.Cyan, () => {
            Console.WriteLine($"{ConsoleUI.Chars.TL}{horizontalLine}{ConsoleUI.Chars.TR}");
            foreach (var line in bannerLines)
            {
                var paddedLine = line.PadRight(totalWidth - 4);
                Console.WriteLine($"{ConsoleUI.Chars.V} {paddedLine} {ConsoleUI.Chars.V}");
            }
            Console.WriteLine($"{ConsoleUI.Chars.BL}{horizontalLine}{ConsoleUI.Chars.BR}");
        });
        Console.WriteLine();
    }

    /// <summary>
    /// Shows command groups dynamically extracted from a parent command
    /// </summary>
    public static void ShowCommandGroups(Command parentCommand, Dictionary<string, string[]>? commandGroups = null, Dictionary<string, string>? groupDescriptions = null)
    {
        var all = parentCommand.Subcommands.ToArray();

        if (commandGroups != null)
        {
            // Show commands organized by specified groups
            foreach (var (groupName, commandNames) in commandGroups)
            {
                var groupCommands = all
                    .Where(c => commandNames.Contains(c.Name))
                    .ToArray();

                if (groupCommands.Any())
                {
                    // Header first
                    ConsoleUI.WriteSection(groupName);
                    // Show group description beneath header (plain gray text, not bullet)
                    if (groupDescriptions?.TryGetValue(groupName, out var desc) == true && !string.IsNullOrWhiteSpace(desc))
                    {
                        ConsoleUI.Write(desc.Trim(), ConsoleColor.Gray);
                        Console.WriteLine();
                    }
                    foreach (var cmd in groupCommands)
                    {
                        var (shortDesc, examples) = ParseDescriptionAndExamples(cmd.Description ?? string.Empty);
                        // Show each subcommand row with optional examples
                        ConsoleUI.WriteSubcommand(cmd.Name, shortDesc, examples.Length > 0 ? examples : null);
                    }
                }
            }
        }
        else
        {
            // Show all commands in a single group
            if (all.Any())
            {
                ConsoleUI.WriteSection("Available Commands");
                foreach (var cmd in all)
                {
                    var (shortDesc, examples) = ParseDescriptionAndExamples(cmd.Description ?? string.Empty);
                    ConsoleUI.WriteSubcommand(cmd.Name, shortDesc, examples.Length > 0 ? examples : null);
                }
            }
        }
    }

    /// <summary>
    /// Shows standard footer with quick actions
    /// </summary>
    public static void ShowStandardFooter(string commandName)
    {
        ConsoleUI.WriteSection("Quick Actions");
        ConsoleUI.WriteCommand("Get help on a command", $"srectl {commandName} <command> --help");
        ConsoleUI.WriteCommand("Interactive guidance", "srectl interactive");
        Console.WriteLine();
    }

    // Parse the command description into a short one-liner and structured examples
    private static (string shortDescription, (string Comment, string Command)[] examples) ParseDescriptionAndExamples(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ("No description available", Array.Empty<(string, string)>());

        // Split into two parts: text before "Examples:" and after
        var idx = description.IndexOf("Examples:", StringComparison.OrdinalIgnoreCase);
        string header = idx >= 0 ? description[..idx] : description;
        string examplesBlock = idx >= 0 ? description[(idx + "Examples:".Length)..] : string.Empty;

        // Short description = first non-empty line of header
        var shortDesc = header
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? header.Trim();

        var examples = new List<(string Comment, string Command)>();
        if (!string.IsNullOrWhiteSpace(examplesBlock))
        {
            string? pendingComment = null;
            foreach (var raw in examplesBlock.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("# "))
                {
                    // comment line
                    pendingComment = line[2..].Trim();
                    continue;
                }

                // Accept lines that look like commands (start with srectl)
                if (line.StartsWith("srectl ", StringComparison.OrdinalIgnoreCase))
                {
                    examples.Add((pendingComment ?? string.Empty, line));
                    pendingComment = null;
                }
            }
        }

        return (shortDesc, examples.ToArray());
    }

    /// <summary>
    /// Show formatted help for a command group with consistent layout
    /// </summary>
    public static void ShowCommandGroupHelp(
        string groupDisplayName,
        string groupDescription,
        ConsoleColor panelColor,
        Command parentCommand,
        Dictionary<string, string[]>? commandGroups = null,
        Dictionary<string, string>? groupDescriptions = null,
        string[]? examples = null)
    {
        ShowSrectlHeader();

        ConsoleUI.DrawPanel(groupDisplayName, groupDescription, panelColor);
        Console.WriteLine();

        ShowCommandGroups(parentCommand, commandGroups, groupDescriptions);
    }

    /// <summary>
    /// Shows a bottom banner section with command group context
    /// </summary>
    private static void ShowBottomBanner(string groupDisplayName, string groupDescription, ConsoleColor bannerColor)
    {
        var bannerLines = new[]
        {
            groupDisplayName,
            "",
            groupDescription
        };

        // Calculate the width needed for the banner
        var maxContentWidth = bannerLines.Max(line => line.Length);
        var totalWidth = Math.Max(maxContentWidth + 4, 40); // At least 40 chars wide
        var horizontalLine = new string(ConsoleUI.Chars.H[0], totalWidth - 2);

        ConsoleUI.WithColor(bannerColor, () => {
            Console.WriteLine($"{ConsoleUI.Chars.TL}{horizontalLine}{ConsoleUI.Chars.TR}");
            foreach (var line in bannerLines)
            {
                var paddedLine = line.PadRight(totalWidth - 4);
                Console.WriteLine($"{ConsoleUI.Chars.V} {paddedLine} {ConsoleUI.Chars.V}");
            }
            Console.WriteLine($"{ConsoleUI.Chars.BL}{horizontalLine}{ConsoleUI.Chars.BR}");
        });
        Console.WriteLine();
    }
}
