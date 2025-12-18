// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Platform overview and core concepts documentation.
/// </summary>
public static class OverviewDocs
{
    public static string GetOverview() => """
        # SRE Agent Platform Overview

        AI-powered platform for Site Reliability Engineering. Automates incident response, monitoring, and operational tasks.

        ## Core Concepts

        **Runtime Flow**: Trigger → Agent → Tools → Connectors

        **Deployment Order**: Tools → Agents → Triggers (always deploy dependencies first)

        | Concept | Description |
        |---------|-------------|
        | **Connectors** | Pre-authenticated endpoints (Kusto, ICM, Outlook, Teams, MCP) |
        | **Tools** | Query/action definitions - System tools (platform) or Custom (KustoTool, LinkTool) |
        | **Agents** | AI orchestration with instructions, tools, and optional handoffs |
        | **Triggers** | Events that invoke agents (ICM incidents, scheduled tasks) |
        | **Handoffs** | Agents delegating to other agents (use sparingly) |

        ## Key Principle: Kusto-Centric

        Internal Microsoft teams diagnose Azure services via Kusto telemetry using (subscription, resourceGroup, resourceName) as keys. Do NOT mix with Azure Resource Graph or Az CLI - operators access their own telemetry, not customer resources directly.

        ## File Structure

        ```
        your-project/
        ├── agents/
        │   └── MyAgent/MyAgent.yaml
        ├── tools/
        │   └── MyTool/MyTool.yaml
        └── scheduledtasks/
        ```
        """;
}
