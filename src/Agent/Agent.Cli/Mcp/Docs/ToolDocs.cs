// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Tool types and connector documentation.
/// </summary>
public static class ToolDocs
{
    public static string GetToolDocumentation() => """
        # Custom Tool Types (ExtendedAgentTool)

        Custom tools let you define reusable queries and links.

        ## CRITICAL: Parameter Placeholder Format

        **ALWAYS use `##paramName##` format for parameter placeholders in queries.**
        - CORRECT: `'##subscriptionId##'`
        - WRONG: `'{{subscriptionId}}'` or `'{subscriptionId}'`

        **ALWAYS include `target: dictionary:args:string` for each parameter.**
        **ALWAYS use `type: string` for all parameters.**

        ## KustoTool

        Execute parameterized Kusto queries.

        **Required Fields:**
        - `connector`: Kusto connector name (from `srectl tool show-connectors`)
        - `database`: Database name
        - `description`: What the tool does

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: get-error-count
        spec:
          type: KustoTool
          connector: my-kusto-connector
          toolMode: Auto
          description: Count errors for a resource in time range
          database: TelemetryDB
          query: |
            let _subscriptionId = '##subscriptionId##';
            let _resourceGroup = '##resourceGroup##';
            let _hours = toint('##hours##');
            ServiceLogs
            | where SubscriptionId == _subscriptionId
            | where ResourceGroup == _resourceGroup
            | where Timestamp > ago(_hours * 1h)
            | where Level == 'Error'
            | count
          parameters:
            - name: subscriptionId
              type: string
              description: Azure subscription ID
              required: true
              target: dictionary:args:string
            - name: resourceGroup
              type: string
              description: Resource group name
              required: true
              target: dictionary:args:string
            - name: hours
              type: string
              description: Hours to look back
              required: true
              target: dictionary:args:string
        ```

        ### Validating KustoTool Queries

        **If Kusto MCP is available**, use it to validate queries before deployment:
        1. Run `.show table TableName schema as json` to verify table/column names
        2. Test query with hardcoded values first
        3. Replace with `##param##` placeholders after validation
        4. Run `srectl tool validate --file tool.yaml` before deployment

        ## LinkTool

        Generate URLs with parameter substitution.

        **Required Fields:**
        - `template`: URL with `##param##` placeholders
        - `description`: What the link is for

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: open-dashboard
        spec:
          type: LinkTool
          toolMode: Auto
          description: Open Azure dashboard by ID
          template: https://portal.azure.com/#@/dashboard/##dashboardId##
          parameters:
            - name: dashboardId
              type: string
              description: Dashboard resource ID
              required: true
              target: dictionary:args:string
        ```

        ## Parameters

        All parameters MUST have description, type: string, and target:

        ```yaml
        parameters:
          - name: param-name
            type: string                      # ALWAYS use string
            description: What this parameter is for  # REQUIRED
            required: true                    # or false
            target: dictionary:args:string    # REQUIRED - always include this
        ```
        """;

    public static string GetConnectorDocumentation() => """
        # Connectors

        Connectors are pre-authenticated endpoints for data sources.

        ## Available Connector Types

        | Type | Purpose |
        |------|---------|
        | Kusto | Query Kusto/ADX clusters |
        | ICM | Incident management |
        | Outlook | Send emails |
        | Teams | Send notifications |
        | MCP | Connect to external MCP servers |

        ## Workflow: Check Before Creating

        1. **List existing connectors**: Use `list_connectors` tool
        2. **Reuse if exists**: If your Kusto cluster already has a connector, use it
        3. **Create if needed**: Use `create_kusto_connector` tool

        ## Creating a Kusto Connector

        The `create_kusto_connector` tool will:
        1. Check if connector already exists (reuse it)
        2. If not found, provide Az CLI/PowerShell commands for manual creation

        ### Connector Properties

        | Property | Description |
        |----------|-------------|
        | name | Unique connector name (kebab-case) |
        | dataConnectorType | `Kusto`, `ICM`, `Outlook`, `Teams`, `MCP` |
        | dataSource | Cluster URL for Kusto |
        | identity | `system` (uses managed identity) |

        ## Using Connectors in KustoTool

        ```yaml
        spec:
          type: KustoTool
          connector: my-kusto-connector   # Exact name from list_connectors
          database: TelemetryDB
        ```

        ## Permission Requirements

        - **List connectors**: Reader access
        - **Create connectors**: Contributor access on the Agent resource

        The tool provides Az CLI and PowerShell commands for users to create connectors themselves.
        """;
}
