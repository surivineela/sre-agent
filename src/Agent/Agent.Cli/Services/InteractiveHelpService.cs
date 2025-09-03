using System.Text;
using Agent.Cli.Commands;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

/// <summary>
/// Enhanced help system with interactive examples and contextual guidance
/// </summary>
public static class InteractiveHelpService
{
    /// <summary>
    /// Show interactive help with command suggestions based on current context
    /// </summary>
    public static async Task ShowInteractiveHelp(string? specificCommand = null)
    {
        if (specificCommand != null)
        {
            await ShowCommandSpecificHelp(specificCommand);
            return;
        }

        StandardHelpFormatter.ShowSrectlHeader();
        
        ConsoleUI.DrawPanel("Interactive Help System", "Examples, troubleshooting, and command guidance", ConsoleColor.Cyan);
        Console.WriteLine();

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
            ("doc", "Document management commands"),
            ("profile", "Profile management commands")
        });

        ConsoleUI.WriteSection("Quick Actions");
        ConsoleUI.WriteCommand("Start chatting", "srectl chat");
        ConsoleUI.WriteCommand("Check status", "srectl status");
        ConsoleUI.WriteCommand("Get suggestions", "srectl suggest");
        Console.WriteLine();

        ConsoleUI.WriteSection("Pro Tips");
        ConsoleUI.WriteBullet("Add --debug to any command for detailed output");
        ConsoleUI.WriteBullet("Use --dry-run with apply commands to preview changes");
        ConsoleUI.WriteBullet("Try --smart with create commands for AI assistance");
        Console.WriteLine();

        ConsoleUI.WriteSection("More Information");
        ConsoleUI.WriteCommand("Command examples", "srectl help <command>");
        ConsoleUI.WriteCommand("Interactive mode", "srectl interactive");
        ConsoleUI.WriteCommand("Workspace status", "srectl status");
        Console.WriteLine();
    }

    /// <summary>
    /// Show contextual help based on current workspace state
    /// </summary>
    public static Task ShowCommandSpecificHelp(string command)
    {
        var helpContent = command.ToLowerInvariant() switch
        {
            "quickstart" => GetQuickstartHelp(),
            "examples" => GetExamplesHelp(),
            "troubleshooting" => GetTroubleshootingHelp(),
            "workflows" => GetWorkflowsHelp(),
            _ => GetGenericCommandHelp(command)
        };

        Console.WriteLine(helpContent);
        return Task.CompletedTask;
    }

    private static string GetQuickstartHelp()
    {
        return @"
🚀 SRE Agent CLI Quickstart Guide

┌─ Step 1: Initialize Your Workspace ────────────────────────────┐
│  srectl init --resource-url https://localhost:7023              │
│  # Creates directories and connects to your SRE Agent server   │
└────────────────────────────────────────────────────────────────┘

┌─ Step 2: Create Your First Agent ──────────────────────────────┐
│  srectl agent create --name DevOpsHelper --smart               │
│  # AI will help generate instructions and recommend tools      │
└────────────────────────────────────────────────────────────────┘

┌─ Step 3: Deploy Your Agent ────────────────────────────────────┐
│  srectl agent apply --name DevOpsHelper                        │
│  # Deploys the agent to your server                            │
└────────────────────────────────────────────────────────────────┘

┌─ Step 4: Test Your Agent ──────────────────────────────────────┐
│  srectl agent test --name DevOpsHelper \                       │
│    --message ""Help me troubleshoot a failing pod""            │
└────────────────────────────────────────────────────────────────┘

🎯 What's Next?
   • Create custom tools: srectl tool create --help
   • Start interactive chat: srectl chat
   • Explore examples: srectl help examples
";
    }

    private static string GetExamplesHelp()
    {
        return @"
📚 Real-World Examples

🔥 Incident Response Agent:
┌─────────────────────────────────────────────────────────────────┐
│  # Create monitoring tools                                      │
│  srectl tool create --name QueryMetrics --type KustoTool        │
│  srectl tool create --name CheckHealth --type AzureTool         │
│                                                                 │
│  # Create incident response agent                               │
│  srectl agent create --name IncidentBot \                       │
│    --instructions ""First-line incident response assistant"" \  │
│    --tools QueryMetrics CheckHealth                             │
│                                                                 │
│  # Deploy and test                                              │
│  srectl agent apply --name IncidentBot                          │
│  srectl agent test --name IncidentBot \                         │
│    --message ""Service outage in production""                   │
└─────────────────────────────────────────────────────────────────┘

🐳 Kubernetes SRE Agent:
┌─────────────────────────────────────────────────────────────────┐
│  # Use AI to create Kubernetes expert                           │
│  srectl agent create --name K8sExpert --smart \                 │
│    --instructions ""Kubernetes troubleshooting specialist""     │
│                                                                 │
│  # Test with real scenarios                                     │
│  srectl agent test --name K8sExpert \                           │
│    --message ""Pods in payment-service namespace are failing""  │
└─────────────────────────────────────────────────────────────────┘

💾 Database SRE Agent:
┌─────────────────────────────────────────────────────────────────┐
│  srectl tool create --name QueryDBMetrics --type KustoTool \    │
│    --extra database:DatabaseMetrics cluster:prod-monitoring     │
│                                                                 │
│  srectl agent create --name DatabaseSRE \                       │
│    --tools QueryDBMetrics \                                     │
│    --instructions ""Database performance and reliability expert""│
└─────────────────────────────────────────────────────────────────┘

🌍 Multi-Environment Workflow:
┌─────────────────────────────────────────────────────────────────┐
│  # Set up profiles for different environments                   │
│  srectl profile create --name local \                           │
│    --resource-url https://localhost:7023                         │
│  srectl profile create --name production \                      │
│    --resource-url https://prod-sre.company.com                  │
│                                                                 │
│  # Switch between environments                                  │
│  srectl profile set --name local      # Develop locally         │
│  srectl profile set --name production # Deploy to prod          │
└─────────────────────────────────────────────────────────────────┘

💡 More examples: Run 'srectl help workflows' for step-by-step guides
";
    }

    private static string GetTroubleshootingHelp()
    {
        return @"
🔧 Troubleshooting Guide

❌ Common Issues and Solutions:

┌─ Connection Problems ───────────────────────────────────────────┐
│  Issue: ""Failed to connect to server""                         │
│  Solutions:                                                     │
│    • Check server URL: srectl profile get                       │
│    • Test connection: srectl list agents --debug                │
│    • Verify server is running                                   │
│    • Check authentication if required                           │
└─────────────────────────────────────────────────────────────────┘

┌─ Agent Creation Issues ─────────────────────────────────────────┐
│  Issue: ""Agent validation failed""                             │
│  Solutions:                                                     │
│    • Validate config: srectl agent validate --all               │
│    • Check tool dependencies: --check-tools                     │
│    • Review YAML syntax                                         │
│    • Use --debug for detailed error info                        │
└─────────────────────────────────────────────────────────────────┘

┌─ Tool Problems ─────────────────────────────────────────────────┐
│  Issue: ""Tool not found or invalid""                           │
│  Solutions:                                                     │
│    • List available types: srectl tool show-types               │
│    • Validate tool config: srectl tool validate --name MyTool   │
│    • Check server tools: srectl list tools                      │
│    • Apply tool first: srectl tool apply --name MyTool          │
└─────────────────────────────────────────────────────────────────┘

┌─ Performance Issues ────────────────────────────────────────────┐
│  Issue: ""Slow responses or timeouts""                          │
│  Solutions:                                                     │
│    • Use --no-wait for fire-and-forget messages                 │
│    • Check server resources and logs                            │
│    • Reduce agent complexity                                    │
│    • Monitor with srectl thread track <thread-id>               │
└─────────────────────────────────────────────────────────────────┘

🔍 Debug Mode:
   Add --debug to ANY command for detailed logging:
   srectl agent apply --name MyAgent --debug

🆘 Still stuck?
   • Check server logs
   • Review agent YAML files in ./agents/ directory
   • Use 'srectl chat --debug' for interactive debugging
   • Validate workspace: srectl agent validate --all --check-tools
";
    }

    private static string GetWorkflowsHelp()
    {
        return CommandExamples.Workflows.QuickStart + "\n\n" +
               CommandExamples.Workflows.DevelopmentWorkflow + "\n\n" +
               CommandExamples.Workflows.TeamCollaborationWorkflow;
    }

    private static string GetGenericCommandHelp(string command)
    {
        return $@"
ℹ️  Help for '{command}' command

Use one of these options to get detailed help:
   srectl {command} --help           # Command-specific help
   srectl help examples             # Real-world examples
   srectl help quickstart           # Getting started guide
   srectl help troubleshooting      # Common issues

Available help topics:
   • quickstart     - Step-by-step setup guide
   • examples       - Real-world usage examples
   • workflows      - Common development patterns
   • troubleshooting - Problem solving guide
";
    }

}
