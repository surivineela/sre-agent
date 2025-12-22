// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Mcp;

namespace Agent.Cli.Commands;

/// <summary>
/// Command handlers for MCP server commands.
/// </summary>
internal static class McpCommandHandlers
{
    /// <summary>
    /// Handle the 'mcp start' command - starts the MCP server with stdio transport.
    /// Uses the official ModelContextProtocol SDK for automatic schema generation.
    /// </summary>
    public static async Task HandleStartCommand(ParseResult parseResult)
    {
        var verbose = parseResult.GetValue(McpCommandOptions.Start.VerboseOption);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            if (verbose)
            {
                Console.Error.WriteLine("SRE Agent MCP Server starting (using official SDK)...");
                Console.Error.WriteLine("Listening on stdin for JSON-RPC messages.");
                Console.Error.WriteLine("Press Ctrl+C to stop.");
            }

            await McpServerRunner.RunAsync(verbose, cts.Token);

            if (verbose)
            {
                Console.Error.WriteLine("MCP Server stopped.");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle the 'mcp info' command - displays documentation.
    /// </summary>
    public static void HandleInfoCommand(ParseResult parseResult)
    {
        var showAll = parseResult.GetValue(McpCommandOptions.Info.AllOption);
        var topic = parseResult.GetValue(McpCommandOptions.Info.TopicOption);

        if (showAll)
        {
            Console.WriteLine(SreAgentDocumentation.GetAllDocumentation());
            return;
        }

        var topicLower = topic?.ToLowerInvariant() ?? "overview";

        var documentation = topicLower switch
        {
            "overview" => SreAgentDocumentation.GetOverview(),
            "architecture" or "trigger-flow" or "flow" => SreAgentDocumentation.GetArchitectureDocumentation(),
            "cli" or "commands" or "cli-commands" => SreAgentDocumentation.GetCliCommandStructure(),
            "agents" => SreAgentDocumentation.GetAgentDocumentation(),
            "tools" => SreAgentDocumentation.GetToolDocumentation(),
            "triggers" => SreAgentDocumentation.GetTriggerDocumentation(),
            "subagents" or "handoffs" => SreAgentDocumentation.GetSubagentDocumentation(),
            "subagent-building" or "building-subagents" => SreAgentDocumentation.GetSubagentBuildingDocumentation(),
            "scheduled-tasks" or "scheduledtasks" => SreAgentDocumentation.GetScheduledTaskDocumentation(),
            "platform-subagents" or "platform" => SreAgentDocumentation.GetPlatformSubagentsDocumentation(),
            "yaml-schema" or "schema" or "schemas" => SreAgentDocumentation.GetYamlSchemaDocumentation(),
            "connectors" or "connector" => SreAgentDocumentation.GetConnectorDocumentation(),
            "mcp" or "mcp-config" or "mcp-setup" => SreAgentDocumentation.GetMcpConfigurationGuide(),
            "quickstart" or "quick-start" => SreAgentDocumentation.GetQuickStartGuide(),
            "all" => SreAgentDocumentation.GetAllDocumentation(),
            _ => GetTopicHelp(topicLower)
        };

        Console.WriteLine(documentation);
    }

    private static string GetTopicHelp(string topic)
    {
        return $@"Unknown topic: '{topic}'

Available topics:
  overview            - Overview of the SRE Agent platform
  architecture        - Trigger -> Agent -> Tool flow
  cli                 - CLI command structure
  agents              - Agent configuration and concepts
  tools               - Tool configuration (System & BYO)
  triggers            - Trigger types and configuration
  subagents           - Subagent/handoff concepts
  subagent-building   - Building custom subagents (C# guide)
  scheduled-tasks     - Scheduled task configuration
  platform-subagents  - Platform-provided subagents
  yaml-schema         - Full YAML schemas (v1/v2)
  connectors          - Connector configuration (Kusto, Python)
  mcp                 - MCP server configuration guide
  quickstart          - Getting started guide
  all                 - All documentation

Usage:
  srectl mcp info --topic <topic>
  srectl mcp info --all
";
    }
}
