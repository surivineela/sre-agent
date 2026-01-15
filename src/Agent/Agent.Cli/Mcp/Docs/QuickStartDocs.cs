// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Quick start guide and configuration documentation.
/// </summary>
public static class QuickStartDocs
{
    public static string GetQuickStartGuide() => """
        # Quick Start

        ## Workflow Overview

        **IMPORTANT: Always follow this sequence:**
        1. Plan architecture → **GET USER CONFIRMATION**
        2. Check/create connectors
        3. Generate YAML → **GET USER CONFIRMATION**
        4. Deploy and test

        ---

        ## Step 1: Plan Architecture

        Call `plan_agent_architecture` with user requirements.

        **CRITICAL:** After receiving the plan:
        - Display the mermaid diagram to the user
        - Ask: "Does this architecture look correct?"
        - **DO NOT proceed until user confirms**

        ---

        ## Step 2: Check Existing Connectors

        Use `list_connectors` tool to see what's already available.

        ---

        ## Step 3: Create Connector if Needed

        If no Kusto connector exists for your cluster:
        ```
        Use create_kusto_connector tool:
        - name: my-kusto-connector
        - clusterUrl: https://mycluster.eastus2.kusto.windows.net
        ```

        If permission denied (403), the tool provides Az CLI commands to run manually.

        ---

        ## Step 4: Create a KustoTool

        **CRITICAL: Always use `##param##` format for placeholders. Always include `target: dictionary:args:string`.**

        ```yaml
        # tools/get-errors.yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: get-errors
        spec:
          type: KustoTool
          connector: my-kusto-connector      # From step 2 or 3
          toolMode: Auto                     # Auto or Manual
          description: |-
            Purpose:
            Get errors for a resource within a time range

            Usage:
            Call with subscriptionId and hours parameters

            Output Format:
            Returns error log entries (limited to 100)
          database: TelemetryDB
          query: |-
            let _subscriptionId = '##subscriptionId##';
            let _hours = toint('##hours##');
            ServiceLogs
            | where SubscriptionId == _subscriptionId
            | where Timestamp > ago(_hours * 1h)
            | where Level == 'Error'
            | take 100
          parameters:
            - name: subscriptionId
              type: string
              description: Azure subscription ID
              required: true
              target: dictionary:args:string
            - name: hours
              type: string
              description: Hours to look back
              required: true
              target: dictionary:args:string
        ```

        ---

        ## Step 5: Deploy the Tool

        ```powershell
        srectl apply-yaml --file tools/get-errors.yaml
        ```

        ---

        ## Step 6: Create an Agent

        ```yaml
        # agents/error-analyst.yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgent
        metadata:
          name: error-analyst
        spec:
          instructions: |-
            You analyze service errors from telemetry.
            Use get-errors to query error logs.
            Look for patterns and suggest root causes.
          handoffDescription: 'Analyzes service errors'
          handoffs: []
          tools:
            - get-errors
          maxReflectionCount: 0
          customReflectionNote: ''
          commonPrompts: []
          enableVanillaMode: false
        ```

        ---

        ## Step 7: Deploy and Test

        ```powershell
        srectl apply-yaml --file agents/error-analyst.yaml
        srectl agent chat --name error-analyst
        ```
        """;

    public static string GetMcpConfigurationGuide() => """
        # MCP Server Configuration

        ## VS Code

        Add to `.vscode/settings.json`:

        ```json
        {
          "mcp.servers": {
            "srectl": {
              "command": "srectl",
              "args": ["mcp", "start"]
            }
          }
        }
        ```

        ## Claude Desktop

        Add to config:

        ```json
        {
          "mcpServers": {
            "srectl": {
              "command": "srectl",
              "args": ["mcp", "start"]
            }
          }
        }
        ```

        ## MCP Tools Available

        | Tool | Purpose |
        |------|---------|
        | plan_agent_architecture | Design solution (call first) |
        | generate_and_validate_yaml | Generate agent/tool YAML |
        | get_documentation | Get platform docs |
        | list_stable_tool_types | List BYO tool types |
        """;
}
