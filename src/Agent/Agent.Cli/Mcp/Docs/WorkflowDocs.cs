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

        ### Phase 3: Deploy in Order

        ```
        DEPLOYMENT ORDER (MANDATORY)
        ─────────────────────────────
        1. TOOLS FIRST
           srectl apply-yaml --file tools/X/X.yaml

        2. AGENTS SECOND
           srectl apply-yaml --file agents/Y/Y.yaml

        3. TRIGGERS LAST (if any)
           srectl apply-yaml --file scheduledtasks/Z.yaml
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

        ## 3. Tools Ready?
        - [ ] Custom tool YAML has connector + database (KustoTool)
        - [ ] Custom tool YAML has template (LinkTool)
        - [ ] All parameters have descriptions

        ## 4. Agent Ready?
        - [ ] Clear, specific instructions
        - [ ] Tools reference only existing tools
        - [ ] handoffDescription filled in
        - [ ] handoffs: [] (empty unless truly needed)

        ## 5. Deploy Order?
        - [ ] Tools FIRST
        - [ ] Agents SECOND
        - [ ] Triggers LAST
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

        ## KustoTool Example

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: get-service-errors
        spec:
          type: KustoTool
          connector: my-kusto-connector
          database: TelemetryDB
          description: Get error counts for a service
          query: |
            ServiceLogs
            | where SubscriptionId == '{{subscriptionId}}'
            | where ResourceGroup == '{{resourceGroup}}'
            | where Timestamp > ago({{hours}}h)
            | where Level == 'Error'
            | summarize ErrorCount=count() by bin(Timestamp, 1h)
          parameters:
            - name: subscriptionId
              type: string
              description: Azure subscription ID
              required: true
            - name: resourceGroup
              type: string
              description: Resource group name
              required: true
            - name: hours
              type: string
              description: Hours to look back
              required: true
        ```
        """;
}
