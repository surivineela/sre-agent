// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Architecture and CLI documentation.
/// </summary>
public static class ArchitectureDocs
{
    public static string GetArchitecture() => """
        # SRE Agent Architecture

        ```
        ┌─────────────────────────────────────────────────────────────┐
        │                    SRE Agent Platform                       │
        ├─────────────────────────────────────────────────────────────┤
        │                                                             │
        │  TRIGGER ──▶ AGENT ──▶ TOOLS ──▶ CONNECTORS                 │
        │                                                             │
        │  Examples:                                                  │
        │  - ICM Incident    Agent with      KustoTool    Kusto       │
        │  - Scheduled Task  instructions    LinkTool     ICM         │
        │  - Manual                          SystemTools  Outlook     │
        │                                                 Teams       │
        │                                                 MCP         │
        └─────────────────────────────────────────────────────────────┘
        ```

        ## Component Hierarchy

        ### Level 1: Connectors (Platform-Managed)
        Pre-authenticated endpoints. Created by platform team via ARM.

        | Type | Purpose |
        |------|---------|
        | Kusto | Query Kusto/ADX clusters |
        | ICM | Incident management |
        | Outlook | Send emails |
        | Teams | Send notifications |
        | MCP | External MCP servers |

        ### Level 2: Tools

        **System Tools**: Pre-built platform tools (from SystemTools API)
        - See `PublishedTools.json` for available tools
        - Include charting, Azure operations, GitHub integration, etc.

        **Custom Tools (BYO)**: User-defined via ExtendedAgentTool YAML
        - KustoTool: Parameterized Kusto queries
        - LinkTool: URL generation with parameters

        ### Level 3: Agents
        LLM orchestration with tools and instructions.
        - Model: gpt-5 (fixed)
        - References tools by name
        - Optional handoffs to other agents (use sparingly)

        ### Level 4: Triggers
        - CronScheduledTask: Time-based
        - ICM Incident Handler: Incident-driven
        """;

    public static string GetCliStructure() => """
        # srectl Command Reference

        ## Apply & Validate

        ```
        srectl apply-yaml --file <path>    Apply YAML to platform
        srectl validate --file <path>      Validate YAML without applying
        ```

        ## Discovery

        ```
        srectl tool show-connectors        List available connectors (IMPORTANT!)
        srectl tool list                   List all tools
        srectl agent list                  List all agents
        srectl scheduledtask list          List scheduled tasks
        ```

        ## Tool Operations

        ```
        srectl tool get --name <name>      Get tool YAML
        srectl tool delete --name <name>   Delete tool
        srectl tool run --name <name>      Test tool execution
        ```

        ## Agent Operations

        ```
        srectl agent get --name <name>     Get agent YAML
        srectl agent delete --name <name>  Delete agent
        srectl agent chat --name <name>    Interactive chat with agent
        ```

        ## Scheduled Task Operations

        ```
        srectl scheduledtask get --name <name>    Get task config
        srectl scheduledtask delete --name <name> Delete task
        ```
        """;
}
