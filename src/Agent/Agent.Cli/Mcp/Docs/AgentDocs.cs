// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// Agent documentation - focused and minimal.
/// </summary>
public static class AgentDocs
{
    public static string GetAgentDocumentation() => """
        # Agent YAML Schema

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgent
        metadata:
          name: my-agent
        spec:
          instructions: |-
            Clear, specific instructions for the agent.
            What it should do, how it should respond.
          handoffDescription: 'One-line description for routing'
          handoffs: []
          tools:
            - tool-name-1
            - tool-name-2
          maxReflectionCount: 0
          customReflectionNote: ''
          commonPrompts: []
          enableVanillaMode: false
        ```

        ## Required Fields

        | Field | Description |
        |-------|-------------|
        | api_version | `azuresre.ai/v2` |
        | kind | `ExtendedAgent` |
        | metadata.name | Unique kebab-case name |
        | spec.instructions | Clear instructions for the LLM |
        | spec.handoffDescription | Required. Short description |
        | spec.handoffs | Required. Array (use `[]` if none) |

        ## Model

        Fixed at `gpt-5`. Do not specify.

        ## Tools

        Reference tools by name. Tool must exist before deploying agent.

        ```yaml
        tools:
          - my-kusto-tool      # Custom tool you created
          - CheckAzureResource # System tool from platform
        ```
        """;

    public static string GetSubagentDocumentation() => """
        # Handoffs (Use Sparingly)

        Handoffs let agents delegate to other agents. Use only when truly needed.

        ## When to Use Handoffs

        - Complex scenarios requiring specialized expertise
        - Clear separation of concerns (e.g., incident triage vs. remediation)

        ## When NOT to Use Handoffs

        - Simple single-purpose agents (most cases)
        - When one agent with multiple tools suffices
        - To artificially split functionality

        ## If You Need Handoffs

        1. Deploy child agents first (they have no handoffs)
        2. Deploy parent agent last (references children)

        ```yaml
        # Parent agent references children
        spec:
          handoffs:
            - specialist-agent-1
            - specialist-agent-2
        ```

        Keep it simple. One well-designed agent is usually better than many small ones.
        """;

    public static string GetSubagentBuildingDocumentation() => """
        # Building Agents

        ## Simple Agent (Recommended)

        Most use cases need just one agent with appropriate tools:

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgent
        metadata:
          name: service-health-agent
        spec:
          instructions: |-
            You help investigate service health issues.

            Use your Kusto tools to query telemetry.
            Look for errors, latency spikes, and anomalies.
            Correlate using subscriptionId, resourceGroup, resourceName.
          handoffDescription: 'Investigates service health using telemetry'
          handoffs: []
          tools:
            - get-service-errors
            - get-latency-metrics
            - get-request-volume
          maxReflectionCount: 0
          customReflectionNote: ''
          commonPrompts: []
          enableVanillaMode: false
        ```

        ## Deployment

        ```powershell
        # 1. Deploy custom tools first
        srectl apply-yaml --file tools/get-service-errors.yaml
        srectl apply-yaml --file tools/get-latency-metrics.yaml

        # 2. Deploy agent second
        srectl apply-yaml --file agents/service-health-agent.yaml
        ```
        """;

    public static string GetPlatformSubagentsDocumentation() => """
        # System Tools

        The platform provides pre-built system tools for common operations.

        ## Discovering System Tools

        System tools are available via the platform API. Key categories include:

        - **Azure Operations**: CheckAzureResource, RestartAppService, etc.
        - **ICM**: GetIncidentInfo, PostDiscussionEntry, etc.
        - **GitHub**: CreateIssue, FetchIssues, etc.
        - **Charting**: GenerateBarChart, GeneratePieChart, etc.
        - **DGrep**: Query distributed logs

        ## Using System Tools

        Reference system tools by name in your agent's tools list:

        ```yaml
        tools:
          - CheckAzureResource    # System tool
          - my-custom-kusto-tool  # Your custom KustoTool
        ```

        System tools don't need YAML - they're provided by the platform.
        Custom tools (KustoTool, LinkTool) need ExtendedAgentTool YAML.
        """;
}
