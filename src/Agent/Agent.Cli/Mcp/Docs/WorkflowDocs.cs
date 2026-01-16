// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Deployment workflow and best practices documentation.
/// </summary>
public static class WorkflowDocs
{
    public static string GetDeploymentWorkflow() => """
        # Deployment Workflow

        ## Design First, Deploy Second

        ### Phase 1: Design
        1. Understand user requirements
        2. List tools needed (system tools vs custom KustoTool/LinkTool)
        3. Define agent with clear instructions
        4. Get user confirmation

        ### Phase 2: Generate YAML
        Generate YAML files for custom tools and agents.
        **CRITICAL: Use `##param##` format for all parameter placeholders.**
        **CRITICAL: Include `target: dictionary:args:string` for all parameters.**

        ### Phase 3: Deploy in Order

        ```
        DEPLOYMENT ORDER (MANDATORY)
        ─────────────────────────────
        1. TOOLS FIRST
           srectl apply-yaml --file tools/X/X.yaml

        2. AGENTS SECOND
           srectl apply-yaml --file agents/Y/Y.yaml

        3. SCHEDULED TASKS LAST (if any) - USE CLI COMMAND, NOT YAML
           srectl scheduledtask create --name "task-name" --agent "agent-name" --cron "0 8 * * *" --prompt "..."
        ```

        ## Why Order Matters

        - Agents reference tools by name → tools must exist first
        - Triggers reference agents → agents must exist first
        """;

    public static string GetSelfReflectionChecklist() => """
        # Pre-Deployment Checklist

        Before deploying, verify:

        ## 1. Requirements Clear?
        - [ ] User requirements understood
        - [ ] Tools identified (system vs custom)
        - [ ] Single agent sufficient? (avoid unnecessary subagents)

        ## 2. Connectors Verified?
        - [ ] Ran `srectl tool show-connectors`
        - [ ] KustoTool references existing Kusto connector
        - [ ] Connector name matches exactly

        ## 3. Tools Ready? (CRITICAL FORMAT REQUIREMENTS)
        - [ ] Custom tool YAML has connector + database (KustoTool)
        - [ ] Custom tool YAML has template (LinkTool)
        - [ ] All parameters have descriptions
        - [ ] **All parameters use `type: string`**
        - [ ] **All parameters have `target: dictionary:args:string`**
        - [ ] **Query uses `##param##` format (NOT `{{param}}`)**

        ## 4. Agent Ready?
        - [ ] Clear, specific instructions
        - [ ] Tools reference only existing tools
        - [ ] handoffDescription filled in
        - [ ] handoffs: [] (empty unless truly needed)

        ## 5. Deploy Order?
        - [ ] Tools FIRST
        - [ ] Agents SECOND
        - [ ] **Scheduled Tasks LAST - use CLI command, NOT YAML**
        """;

    public static string GetDashboardWorkflowGuidance() => """
        # Kusto-Based Agent Workflow

        ## Key Principle: Kusto-Centric Diagnostics

        Internal Microsoft teams diagnose Azure services via their own Kusto telemetry. Use (subscription, resourceGroup, resourceName) as keys to correlate data.

        Do NOT use Azure Resource Graph or Az CLI - operators access telemetry, not customer resources directly.

        ## Building a Telemetry Agent

        1. **Discover connectors**: `srectl tool show-connectors`
        2. **Identify Kusto cluster/database** for your telemetry
        3. **Create KustoTool** for each query pattern
        4. **Create agent** that uses those tools

        ## CRITICAL: Parameter Format

        - **ALWAYS use `##param##` format for placeholders** (NEVER `{{param}}`)
        - **ALWAYS use `type: string` for all parameters**
        - **ALWAYS use `target: dictionary:args:string` for all parameters**

        ## KustoTool Example

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: get-service-errors
        spec:
          type: KustoTool
          connector: my-kusto-connector
          toolMode: Auto
          description: |-
            Purpose:
            Get error counts for a service within a time range

            Usage:
            Call with subscriptionId, resourceGroup, and hours parameters

            Output Format:
            Returns ErrorCount by hourly time bins
          database: TelemetryDB
          query: |-
            let _subscriptionId = '##subscriptionId##';
            let _resourceGroup = '##resourceGroup##';
            let _hours = toint('##hours##');
            ServiceLogs
            | where SubscriptionId == _subscriptionId
            | where ResourceGroup == _resourceGroup
            | where Timestamp > ago(_hours * 1h)
            | where Level == 'Error'
            | summarize ErrorCount=count() by bin(Timestamp, 1h)
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
        """;
}
