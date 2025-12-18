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

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: tool-name                     # kebab-case
        spec:
          type: KustoTool
          connector: connector-name           # REQUIRED - from show-connectors
          database: DatabaseName              # REQUIRED
          description: What this tool does    # REQUIRED
          query: |
            Table
            | where Column == '{{param}}'
          parameters:
            - name: param
              type: string
              description: Parameter purpose  # REQUIRED
              required: true
        ```

        ## LinkTool Schema

        ```yaml
        api_version: azuresre.ai/v2
        kind: ExtendedAgentTool
        metadata:
          name: tool-name                     # kebab-case
        spec:
          type: LinkTool
          template: https://url/{{param}}     # REQUIRED - note: 'template' not 'urlTemplate'
          description: What this link is for  # REQUIRED
          parameters:
            - name: param
              type: string
              description: Parameter purpose  # REQUIRED
              required: true
        ```

        ## Scheduled Task Schema

        ```yaml
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

        Time-based trigger using cron expressions.

        ```yaml
        api_version: azuresre.ai/v2
        kind: CronScheduledTask
        metadata:
          name: daily-health-check
        spec:
          agentName: health-check-agent
          schedule: '0 8 * * *'
          prompt: |-
            Run daily health check.
          enabled: true
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

        ## Creating a Scheduled Task

        1. Create the agent first
        2. Create the scheduled task referencing the agent

        ```yaml
        api_version: azuresre.ai/v2
        kind: CronScheduledTask
        metadata:
          name: daily-health-check
        spec:
          agentName: health-check-agent       # Must exist
          schedule: '0 8 * * *'               # Daily at 8 AM UTC
          prompt: |-
            Run the daily health check.
            Report any issues found.
          enabled: true
        ```

        ## Deployment Order

        ```powershell
        # 1. Tools (if any custom tools)
        srectl apply-yaml --file tools/my-tool.yaml

        # 2. Agent
        srectl apply-yaml --file agents/health-check-agent.yaml

        # 3. Scheduled task LAST
        srectl apply-yaml --file scheduledtasks/daily-health-check.yaml
        ```

        ## Management

        ```powershell
        srectl scheduledtask list
        srectl scheduledtask get --name daily-health-check
        srectl scheduledtask delete --name daily-health-check
        ```
        """;
}
