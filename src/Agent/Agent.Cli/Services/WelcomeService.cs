using System.Text;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

/// <summary>
/// Service for displaying welcome messages and first-time user guidance
/// </summary>
public static class WelcomeService
{
    /// <summary>
    /// Display a welcome banner with helpful information for new users
    /// </summary>
    public static void ShowWelcomeBanner()
    {
        Console.WriteLine();
        ConsoleUI.DrawPanel("SRE Agent CLI (srectl)", "Your intelligent assistant for SRE automation", ConsoleColor.Cyan);
        Console.WriteLine();
        
        ConsoleUI.WriteInfo($"cwd: {Directory.GetCurrentDirectory()}");
        ConsoleUI.WriteInfo("srectl --help for help, srectl interactive for guided setup");
        Console.WriteLine();
        
        ConsoleUI.WriteSection("Core Commands");
        ConsoleUI.WriteCommand("Initialize workspace", "srectl init");
        ConsoleUI.WriteCommand("Interactive guided mode", "srectl interactive");
        ConsoleUI.WriteCommand("Check workspace status", "srectl status");
        Console.WriteLine();

        ConsoleUI.WriteSection("Agent Management");
        ConsoleUI.WriteCommand("Create new SRE agent", "srectl agent create");
        ConsoleUI.WriteCommand("List deployed agents", "srectl agent list");
        ConsoleUI.WriteCommand("Deploy agent to server", "srectl agent apply");
        ConsoleUI.WriteCommand("Test agent functionality", "srectl agent test");
        Console.WriteLine();

        ConsoleUI.WriteSection("Tool Management");
        ConsoleUI.WriteCommand("Create new tools for agents", "srectl tool create");
        ConsoleUI.WriteCommand("See available tool types", "srectl tool show-types");
        ConsoleUI.WriteCommand("Deploy tools to server", "srectl tool apply");
        Console.WriteLine();

        ConsoleUI.WriteSection("Interactive Features");
        ConsoleUI.WriteCommand("Interactive chat with SRE Agent", "srectl chat");
        ConsoleUI.WriteCommand("Start new conversation thread", "srectl thread new");
        ConsoleUI.WriteCommand("Continue existing conversation", "srectl thread continue");
        Console.WriteLine();

        ConsoleUI.WriteSection("Workspace");
        ConsoleUI.WriteCommand("List deployed resources", "srectl list");
        ConsoleUI.WriteCommand("Manage connection profiles", "srectl profile");
        ConsoleUI.WriteCommand("Apply YAML configurations", "srectl apply-yaml");
        Console.WriteLine();

        ConsoleUI.WriteSection("Pro Tips");
        ConsoleUI.WriteBullet("Add --debug to any command for detailed output");
        ConsoleUI.WriteBullet("Use --dry-run with apply commands to preview changes");
        ConsoleUI.WriteBullet("Try --smart with create commands for AI assistance");
        Console.WriteLine();

        ConsoleUI.WriteSection("Get detailed help");
        ConsoleUI.WriteCommand("Help for specific command", "srectl <command> --help");
        ConsoleUI.WriteCommand("Real-world examples", "srectl help examples");
        ConsoleUI.WriteCommand("Step-by-step guide", "srectl help quickstart");
        ConsoleUI.WriteCommand("Common issues and solutions", "srectl help troubleshooting");
    }

    /// <summary>
    /// Display contextual help based on user's current workspace state
    /// </summary>
    public static async Task ShowContextualGuidance()
    {
        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        if (!hasConfig)
        {
            ConsoleUI.WriteInfo("Looks like this is your first time using srectl!");
            ConsoleUI.WriteCommand("Set up your workspace", "srectl init --resource-url <your-server-url>");
            Console.WriteLine();
            return;
        }

        // Check if they have any agents or tools
        var hasAgents = Directory.Exists("agents") && Directory.GetDirectories("agents").Length > 0;
        var hasTools = Directory.Exists("tools") && Directory.GetDirectories("tools").Length > 0;

        if (!hasAgents && !hasTools)
        {
            ConsoleUI.WriteInfo("Ready to create your first agent?");
            ConsoleUI.WriteCommand("Create first agent", "srectl agent create --name MyFirstAgent --smart");
            ConsoleUI.WriteCommand("Getting started guide", "srectl help quickstart");
        }
        else
        {
            ConsoleUI.WriteSection("Workspace Status");
            if (hasAgents)
                ConsoleUI.WriteBullet($"{Directory.GetDirectories("agents").Length} agents configured");
            if (hasTools)
                ConsoleUI.WriteBullet($"{Directory.GetDirectories("tools").Length} tools available");
            ConsoleUI.WriteCommand("See deployed agents", "srectl list agents");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Show success celebration for completed operations
    /// </summary>
    public static void ShowSuccess(string operation, string details = "")
    {
        ConsoleUI.WriteStatus(true, $"Success! {operation}");
        if (!string.IsNullOrEmpty(details))
        {
            ConsoleUI.WriteBullet(details);
        }
    }

    /// <summary>
    /// Show helpful error suggestions
    /// </summary>
    public static void ShowErrorWithSuggestions(string error, string[] suggestions)
    {
        ConsoleUI.WriteStatus(false, error);
        Console.WriteLine();
        ConsoleUI.WriteSection("Here are some things you can try");
        foreach (var suggestion in suggestions)
        {
            ConsoleUI.WriteBullet(suggestion);
        }
        Console.WriteLine();
        ConsoleUI.WriteInfo("For more help: srectl --help or srectl help troubleshooting");
    }
}
