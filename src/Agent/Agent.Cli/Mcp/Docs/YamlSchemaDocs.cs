// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Mcp.Docs;

/// <summary>
/// YAML schema documentation for V2 API.
/// </summary>
public static class YamlSchemaDocs
{
    public static string GetYamlSchemaDocumentation() => """
        # YAML Schema Reference (V2)

        ## API Version

        All YAML uses: `api_version: azuresre.ai/v2`

        ## Agent Schema

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgent
        metadata:
          name: agent-name                    # kebab-case
        spec:
          instructions: |-                    # Multi-line instructions
            Clear instructions for the agent.
          handoffDescription: 'Short desc'   # REQUIRED
          handoffs: []                        # REQUIRED (can be empty)
          tools:
            - tool-name-1
            - tool-name-2
          maxReflectionCount: 0
          customReflectionNote: ''
          commonPrompts: []
          enableVanillaMode: false
        ```

        ## KustoTool Schema

        **CRITICAL: Always use `##param##` format for placeholders, NEVER use `{{param}}`**
        **CRITICAL: Always use `type: string` and `target: dictionary:args:string` for all parameters**

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: tool-name                     # kebab-case
        spec:
          type: KustoTool
          connector: connector-name           # REQUIRED - from show-connectors
          toolMode: Auto                      # Auto or Manual (default: Auto)
          description: |-                     # Use |- for multi-line descriptions
            Purpose:
            What this tool does and when to use it

            Usage:
            How to call this tool with parameters

            Output Format:
            What columns/data the tool returns
          database: DatabaseName              # REQUIRED
          query: |-
            let _startTime = todatetime('##startTime##');
            let _endTime = todatetime('##endTime##');
            let _resourceId = '##resourceId##';
            Table
            | where Timestamp between (_startTime .. _endTime)
            | where ResourceId == _resourceId
          parameters:
            - name: startTime
              type: string
              description: Start time for the query (e.g., 2024-01-01T00:00:00Z)
              required: true
              target: dictionary:args:string
            - name: endTime
              type: string
              description: End time for the query
              required: true
              target: dictionary:args:string
            - name: resourceId
              type: string
              description: Azure resource ID
              required: true
              target: dictionary:args:string
        ```

        ### Tool Mode

        - `Auto`: Tool is automatically available to the agent (default)
        - `Manual`: Tool requires explicit user approval before execution

        ### Query Schema Validation

        When an agent has KustoTools, always validate the query schema:
        1. Ensure parameter placeholders use `##param##` format (NEVER `{{param}}`)
        2. Ensure all parameters have `type: string` and `target: dictionary:args:string`
        3. Verify database and table names are correct
        4. Test queries with sample data before deployment
        5. Use `srectl tool validate --file tool.yaml` to validate syntax

        **If Kusto MCP is available**: Use it to validate queries before deployment:
        - Run `.show table TableName schema as json` to verify table/column names
        - Test query with hardcoded values first, then replace with `##param##` placeholders
        - Verify the query returns expected results with sample data

        **For client agents with tools**: Before deployment, validate that:
        - All referenced tools exist and are accessible
        - Query parameters use `##param##` format, NOT `{{param}}`
        - All parameters have `target: dictionary:args:string`
        - Database permissions are correctly configured for the connector
        - Run `srectl agent validate --name agent-name` to check tool bindings

        ## LinkTool Schema

        **CRITICAL: Use `##param##` format for placeholders in templates**

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: tool-name                     # kebab-case
        spec:
          type: LinkTool
          toolMode: Auto                      # Auto or Manual (default: Auto)
          description: What this link is for  # REQUIRED
          template: https://url/##param##     # REQUIRED - use ##param## format
          parameters:
            - name: param
              type: string
              description: Parameter purpose  # REQUIRED
              required: true
              target: dictionary:args:string  # REQUIRED
        ```

        ## Scheduled Task Schema

        **CRITICAL: DO NOT use YAML for scheduled tasks. ALWAYS use the CLI command instead.**

        Use `srectl scheduledtask create` command:
        ```powershell
        srectl scheduledtask create \
          --name "daily-report" \
          --description "Daily scheduled task description" \
          --cron "0 8 * * *" \
          --agent "target-agent-name" \
          --prompt "What to ask the agent"
        ```

        The YAML schema below is for reference only - DO NOT USE IT:
        ```yaml
        # DO NOT USE YAML FOR SCHEDULED TASKS - USE CLI COMMAND ABOVE
        api_version: azuresre.ai/v2
        kind: CronScheduledTask
        metadata:
          name: task-name
        spec:
          agentName: target-agent             # Agent to invoke
          schedule: '0 8 * * *'               # Cron expression
          prompt: |-
            What to ask the agent.
          enabled: true
        ```

        ## Naming

        - Use kebab-case: `my-agent-name`
        - Keep names descriptive but concise
        """;

    public static string GetTriggerDocumentation() => """
        # Triggers

        Triggers invoke agents automatically.

        ## CronScheduledTask

        **CRITICAL: DO NOT use YAML for scheduled tasks. ALWAYS use the CLI command instead.**

        ```powershell
        srectl scheduledtask create \
          --name "daily-health-check" \
          --description "Daily health check task" \
          --cron "0 8 * * *" \
          --agent "health-check-agent" \
          --prompt "Run daily health check."
        ```

        ## Cron Expression Reference

        ```
        ┌───────────── minute (0-59)
        │ ┌───────────── hour (0-23)
        │ │ ┌───────────── day of month (1-31)
        │ │ │ ┌───────────── month (1-12)
        │ │ │ │ ┌───────────── day of week (0-6)
        │ │ │ │ │
        * * * * *
        ```

        | Expression | Meaning |
        |------------|---------|
        | `0 * * * *` | Every hour |
        | `0 8 * * *` | Daily at 8 AM |
        | `0 */6 * * *` | Every 6 hours |
        | `0 8 * * 1` | Monday at 8 AM |

        ## ICM Incident Handler

        ICM-based triggers are configured separately via incident handler setup, not YAML.
        """;

    public static string GetScheduledTaskDocumentation() => """
        # Scheduled Tasks

        Run agents on a schedule.

        ## CRITICAL: Always Use CLI Command - DO NOT Use YAML

        **ALWAYS use `srectl scheduledtask create` command. DO NOT use YAML files for scheduled tasks.**

        ## Creating a Scheduled Task

        ```powershell
        srectl scheduledtask create \
          --name "daily-token-consumption-report" \
          --description "Daily scheduled task that monitors SRE Agent token consumption" \
          --cron "0 8 * * *" \
          --agent "token-consumption-monitor" \
          --prompt "Generate a daily SRE Agent token consumption report. Query the last 7 days of token consumption data to show trends. Identify the top consuming agents and subscriptions. Compare today's consumption with the 7-day average. Highlight any significant changes or anomalies."
        ```

        ## Deployment Order

        ```powershell
        # 1. Tools (if any custom tools)
        srectl apply-yaml --file tools/my-tool.yaml

        # 2. Agent
        srectl apply-yaml --file agents/health-check-agent.yaml

        # 3. Scheduled task LAST - ALWAYS use CLI command
        srectl scheduledtask create --name "daily-health-check" --agent "health-check-agent" --cron "0 8 * * *" --prompt "Run the daily health check."
        ```

        ## Management

        ```powershell
        srectl scheduledtask list
        srectl scheduledtask get --name daily-health-check
        srectl scheduledtask delete --name daily-health-check
        ```
        """;
}
