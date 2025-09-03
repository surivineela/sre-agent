using System.CommandLine;

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
        var commands = ExtractCommandInfo(parentCommand);
        
        if (commandGroups != null)
        {
            // Show commands organized by specified groups
            foreach (var (groupName, commandNames) in commandGroups)
            {
                var groupCommands = commands
                    .Where(c => commandNames.Contains(c.name))
                    .ToArray();
                    
                if (groupCommands.Any())
                {
                    // Show group description if provided
                    if (groupDescriptions?.ContainsKey(groupName) == true)
                    {
                        ConsoleUI.WriteInfo($"{groupName}: {groupDescriptions[groupName]}", ConsoleColor.Gray);
                        Console.WriteLine();
                    }
                    
                    ConsoleUI.WriteCommandGroup(groupName, groupCommands);
                }
            }
        }
        else
        {
            // Show all commands in a single group
            if (commands.Any())
            {
                ConsoleUI.WriteCommandGroup("Available Commands", commands);
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

    /// <summary>
    /// Extract command information from a Command object
    /// </summary>
    private static (string name, string description)[] ExtractCommandInfo(Command parentCommand)
    {
        return parentCommand.Subcommands
            .Select(cmd => (cmd.Name, cmd.Description ?? "No description available"))
            .ToArray();
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
        
        // Examples are now included within command descriptions, not as separate section
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
