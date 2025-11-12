# YAML Schema Reference

This document captures the YAML resources accepted by the SRE Agent runtime. Two resource types are in scope:

- **ToolList** – the tool catalog you register via `srectl tool apply` or the portal.
- **AgentConfiguration** – the agent profile you apply with `srectl agent apply`.

Both resources are stored in Cosmos DB and synchronized into the runtime, where they are materialized as `ToolFactory<TContext>` registrations or `YamlAgentDescriptor` instances.

> **Maintaining this document:**
> 1. Open GitHub Copilot Chat and start the saved task prompt: `@workspace /prompt .github/prompts/yaml-schema/yaml-schema-maintenance-00-task.prompt.md`.
> 2. When describing the change, either paste a diff such as `git diff -- src/Agent/Agent.Framework/YamlAgentDescriptor.cs` or give a concrete instruction like “Reflect the new field on YamlAgentDescriptor in the doc.” Copilot will ask for additional snippets if it needs more context.
> 3. Apply the suggested edits to this file, and rerun the prompt if other schema files also changed.

## 1. Schema overview

### ToolList

- Data model: `src/Agent/Agent.Web/Models/ExtendedAgents/ToolsDeploymentModel.cs`
- Per-tool entries: `ExtendedAgentToolApiModel` and derived types (`KustoToolApiModel`, `LinkToolApiModel`, …)
- Runtime mapping: `ApiToRuntimeMapper.ToDocumentTool` → `YamlToolDefinitionBase`

### AgentConfiguration

- Data model: `src/Agent/Agent.Web/Models/ExtendedAgents/AgentDeploymentModel.cs`
- Body: `YamlAgentDescriptor`
- Runtime mapping: registered as `IAgentDescriptor` inside `ExtendedAgentService.RefreshAgentAndToolsRegisterationsAsync`

## 2. Sample YAML

### 2.1 Tool definition (ToolList)

```yaml
api_version: azuresre.ai/v1
kind: ToolList
metadata:
  owner: sre-agent@example.com
  version: 1.2.3
  tags: [kusto, diagnostics]
spec:
  tools:
    - name: CheckSiteErrors
      type: KustoTool
      connector: kusto-default
      description: Retrieves failed requests for the specified site in the time window.
      mode: Query
      query: |-
        let startTime = datetime("##start_time##");
        let endTime = datetime("##end_time##");
        let siteName = "##site_name##";
        AppRequests
        | where Timestamp between (startTime .. endTime)
        | where Success == false and SiteName == siteName
        | summarize Failures = count() by bin(Timestamp, 15m)
      database: wawsprod
      cluster_hint: eastus
      display_options:
        show_table: true
        show_chart: true
        max_table_rows: 200
        chart_title: Failed requests by 15 minute bucket
        x_field: Timestamp
        series_fields: [Failures]
      parameters:
        - name: site_name
          type: string
          description: Site hostname (example.azurewebsites.net)
          required: true
          validation:
            regex: '^[a-z0-9-]+\\.azurewebsites\\.net$'
            error_message: "site_name must be a full azurewebsites.net host"
            normalize: [ trim, lowerInvariant ]
        - name: start_time
          type: string
          description: ISO timestamp (UTC)
          required: true
          validation:
            normalize: [ trim ]
        - name: end_time
          type: string
          description: ISO timestamp (UTC)
          required: true
          validation:
            normalize: [ trim ]
```

### 2.2 Agent definition (AgentConfiguration)

```yaml
api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  owner: sre-agent-team@example.com
  version: "1.0.0"
  tags:
    - azure-functions
    - event-hub
    - preflight
    - analysis
  created_at: 2025-10-17
  updated_at: 2025-10-17
spec:
  name: eb_message_delaypercentiles_agent

  system_prompt: |
    You validate event hub message delay (in seconds) via CheckEventHubMessageDelayPercentiles to Summarizes EventHub message processing delays at multiple percentiles (5, 50, 90, 95, 99) to analyze latency distribution.

  tools:
    - CheckEventHubMessageDelayPercentiles
    - ShareAgentResult

  output_type: "WorkflowActivityLLMOutput"

  next_agent_mappings:
    - condition: "any"
      next_agents: []

  agent_type: "Activity"

  user_prompt_override: |
    Process:
    - Run CheckEventHubMessageDelayPercentiles to Summarizes EventHub message processing delays (in seconds) at multiple percentiles (5, 50, 90, 95, 99) to analyze latency distribution.
    - Share representative records and extract EventHub message processing delays at multiple percentiles (5, 50, 90, 95, 99).
    - Share your findings by calling ShareAgentResult and following the output structure.

    Output Structure:
    - Analysis: Whether event hub messages are being delayed for processing, with brief evidence.
    - Parameters: {"StartTime": "value", "EndTime": "value", "SiteName": "value", "EventPrimaryStampName": "value", "FunctionName": "value", "KustoCluster": "value",  "percentile_Delay_5": "value",	"percentile_Delay_50": "value", "percentile_Delay_90": "value", "percentile_Delay_95": "value", "percentile_Delay_99": "value"}
    - NextSteps: []

  disable_document_retrieval: true
  disable_common_prompts: true
  common_prompts:
    - format_guidelines
    - guard_rail
```

## 3. Field reference

### 3.1 ToolList top-level

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `api_version` | string | Yes | Schema version; defaults to `agent.platform.ai/v1`. | [`ToolsDeploymentModel.ApiVersion`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ToolsDeploymentModel.cs) |
| `kind` | string | Yes | Must be `ToolList`. | [`ToolsDeploymentModel.Kind`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ToolsDeploymentModel.cs) |
| `metadata` | object | No | Ownership metadata such as owner, version, tags. | [`YamlMetadata`](../../src/Agent/Agent.Framework/YamlMetadata.cs) |
| `spec` | object | Yes | Body containing the tool array. | [`ToolsDeploymentModel.ToolSpec`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ToolsDeploymentModel.cs) |

### 3.2 `spec.tools[]` (common fields)

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | Tool identifier; must be unique in Cosmos DB. | [`ExtendedAgentToolApiModel.Name`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `type` | string | Yes | Discriminator such as `KustoTool`, `LinkTool`. | [`ExtendedAgentToolApiModel.Type`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `connector` | string | No | Connector name bound at execution time. | [`ExtendedAgentToolApiModel.Connector`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `description` | string | Yes | Summary shown to the LLM when picking a tool. | [`ExtendedAgentToolApiModel.Description`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `parameters` | array | No | Collection of `YamlParameter`. | [`ExtendedAgentToolApiModel.Parameters`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `attributes` | string[] | No | Optional flags. | [`ExtendedAgentToolApiModel.Attributes`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |
| `metadata` | object | No | Additional metadata primarily used on export. | [`ExtendedAgentToolApiModel.Metadata`](../../src/Agent/Agent.Web/Models/ExtendedAgents/ExtendedAgentToolApiModel.cs) |

### 3.3 Kusto tool fields

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `mode` | enum (`Function`, `Query`, `Script`) | Yes | Execution mode. | [`KustoToolApiModel.Mode`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `function` | string | When `mode: Function` | Name of the function to invoke. | [`KustoToolApiModel.Function`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `query` | string | When `mode: Query` | Inline KQL executed by the runtime. | [`KustoToolApiModel.Query`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `file` | string | When `mode: Script` | Path to a KQL script. | [`KustoToolApiModel.File`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `database` | string | Yes | Target database. | [`KustoToolApiModel.Database`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `cluster_hint` | string | No | Preferred cluster name. | [`KustoToolApiModel.ClusterHint`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `cluster_uri` | string | No | Explicit cluster URI override. | [`KustoToolApiModel.ClusterUri`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `regional_cluster_groups` | array | No | Region-to-cluster mapping. | [`KustoToolApiModel.RegionalClusterGroups`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |
| `display_options` | object | No | Visualization hints for downstream surfaces. | [`KustoToolApiModel.DisplayOptions`](../../src/Agent/Agent.Web/Models/ExtendedAgents/KustoToolApiModel.cs) |

#### `parameters[]` (`YamlParameter`)

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | Parameter name exposed to the LLM. | [`YamlParameter.Name`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `type` | string | Yes | Display label for the parameter type. | [`YamlParameter.Type`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `description` | string | Yes | Human-readable explanation of the value. | [`YamlParameter.Description`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `required` | bool | No (default `false`) | Whether the parameter is mandatory. | [`YamlParameter.Required`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `map_to` | string | No | Alternate argument name used at execution time. | [`YamlParameter.MapTo`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `target` | string | No | Supports writing into arrays/dictionaries. | [`YamlParameter.Target`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `value` | any | No | Default value injected by `YamlToolFunction`. | [`YamlParameter.Value`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `validation.regex` | string | No | Regular expression to enforce. | [`YamlParameterValidation.Regex`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `validation.error_message` | string | No | Friendly error surfaced on validation failure. | [`YamlParameterValidation.ErrorMessage`](../../src/Agent/Agent.Framework/YamlParameter.cs) |
| `validation.normalize` | string[] | No | Ordered normalizers such as `trim`, `lowerInvariant`. | [`YamlParameterValidation.Normalize`](../../src/Agent/Agent.Framework/YamlParameter.cs) |

#### `display_options` (`KustoDisplayOptionsDefinition`)

| Key | Type | Description |
| --- | --- | --- |
| `show_table` | bool | Enables tabular rendering. |
| `show_chart` | bool | Enables chart rendering where supported. |
| `max_table_rows` | int | Row cap for tables (validated non-negative). |
| `max_chart_points` | int | Point cap for charts (validated non-negative). |
| `chart_title` | string | Optional chart title override. |
| `x_field` | string | Column plotted on the X-axis. |
| `series_fields` | string[] | Columns grouped into chart series. |

### 3.4 AgentConfiguration top-level

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `api_version` | string | Yes | Resource version. | [`AgentDeploymentModel.ApiVersion`](../../src/Agent/Agent.Web/Models/ExtendedAgents/AgentDeploymentModel.cs) |
| `kind` | string | Yes | Must be `AgentConfiguration`. | [`AgentDeploymentModel.Kind`](../../src/Agent/Agent.Web/Models/ExtendedAgents/AgentDeploymentModel.cs) |
| `metadata` | object | No | Ownership metadata. | [`YamlMetadata`](../../src/Agent/Agent.Framework/YamlMetadata.cs) |
| `spec` | object | Yes | Agent body. | [`YamlAgentDescriptor`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |

### 3.5 `spec` (`YamlAgentDescriptor` notable fields)

| Key | Type | Required | Description | Implementation |
| --- | --- | --- | --- | --- |
| `name` | string | Yes | Agent identifier. | [`YamlAgentDescriptor.Name`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `system_prompt` | string | Yes | Base prompt for the agent. | [`YamlAgentDescriptor.Instructions`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `handoff_description` | string | No | Summary displayed during handoff. | [`YamlAgentDescriptor.HandoffDescription`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `handoffs` | string[] | No | Agent names used for handoff. | [`YamlAgentDescriptor.Handoffs`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `tools` | string[] | No | Registered tools the agent may call. | [`YamlAgentDescriptor.Tools`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `mcp_tools` | string[] | No | MCP tool identifiers. | [`YamlAgentDescriptor.McpTools`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `connectors` | string[] | No | Connector names available to the agent. | [`YamlAgentDescriptor.Connectors`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `allow_parallel_tool_calls` | bool | No (default `true`) | Allow multiple tool executions in parallel. | [`YamlAgentDescriptor.AllowParallelToolCalls`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `agents_as_tools` | object[] | No | Configure other agents as callable tools. | [`YamlAgentDescriptor.AgentsAsTools`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `max_reflection_count` | int | No | Maximum reflection iterations. | [`YamlAgentDescriptor.MaxReflectionCount`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `critic_prompt_path` | string | No | Path to an additional critic prompt template. | [`YamlAgentDescriptor.CriticPromptPath`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `critic_on_handoff` | bool | No | Trigger a critic pass on handoff. | [`YamlAgentDescriptor.CriticOnHandOff`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `custom_reflection_note` | string | No | Text appended to reflection outputs. | [`YamlAgentDescriptor.CustomReflectionNote`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `common_prompts` | string[] | No | Shared prompts applied automatically. | [`YamlAgentDescriptor.CommonPrompts`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `common_tools` | string[] | No | Shared tools list. | [`YamlAgentDescriptor.CommonTools`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `disable_document_retrieval` | bool | No | Disables default document retrieval behavior. | [`YamlAgentDescriptor.DisableDocumentRetrieval`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `instructions_override` | string | No | Runtime override for the base system prompt. | [`YamlAgentDescriptor.InstructionsOverride`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `enable_handoff_prompt_override` | bool | No | Enables the custom handoff prompt. | [`YamlAgentDescriptor.EnableHandoffPromptOverride`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `handoff_prompt_override` | string | No | Custom prompt shown during handoff. | [`YamlAgentDescriptor.HandoffPromptOverride`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `user_prompt_override` | string | No | Overrides the user prompt before execution. | [`YamlAgentDescriptor.UserPromptOverride`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `temperature` | float | No | Sampling temperature for the LLM. | [`YamlAgentDescriptor.Temperature`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `llm_model_name` | string | No | Preferred LLM model identifier. | [`YamlAgentDescriptor.LlmModelName`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `llm_scenario_type` | enum | No | Scenario hint for LLM routing. | [`YamlAgentDescriptor.LlmScenarioType`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `disable_common_prompts` | bool | No | Opt out of default common prompts. | [`YamlAgentDescriptor.DisableCommonPrompts`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `vanilla_mode` | bool | No | Enables vanilla mode (minimal automation). | [`YamlAgentDescriptor.EnableVanillaMode`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `enable_skills` | bool | No | Enables skill execution for the agent. | [`YamlAgentDescriptor.EnableSkills`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `add_system_skills` | bool | No | Automatically include system skills. | [`YamlAgentDescriptor.AddSystemSkills`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `agent_type` | enum (`Autonomous`, `Orchestrator`, `Activity`) | No (default `Autonomous`) | Execution type. | [`YamlAgentDescriptor.AgentType`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `parameter_extraction_agent` | string | WorkflowOrchestrator only | Agent used for parameter extraction; see [WorkflowOrchestrator README](../WorkflowOrchestrator/README.md). | [`YamlAgentDescriptor.ParameterExtractionAgent`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `orchestration_start_agents` | string[] | WorkflowOrchestrator only | Agents invoked before orchestration begins; see [WorkflowOrchestrator README](../WorkflowOrchestrator/README.md). | [`YamlAgentDescriptor.OrchestrationStartAgents`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `result_summarization_prompt` | string | WorkflowOrchestrator only | Prompt used to summarize orchestrated output; see [WorkflowOrchestrator README](../WorkflowOrchestrator/README.md). | [`YamlAgentDescriptor.ResultSummarizationPrompt`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `next_agent_mappings` | object[] | Activity only | Routing table mapping status → next agent. | [`YamlAgentDescriptor.NextAgentMappings`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `output_type` | string | No | Output format (for example, `markdown`). | [`YamlAgentDescriptor.OutputType`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |
| `meta_data` | object | No | Additional metadata mirrored from YAML. | [`YamlAgentDescriptor.Metadata`](../../src/Agent/Agent.Framework/YamlAgentDescriptor.cs) |

---

### Operational notes

- When you apply YAML with `srectl`, `ResourceDeploymentService` upserts the document in Cosmos DB. Immediately after, `ExtendedAgentService.RefreshAgentAndToolsRegisterationsAsync` refreshes runtime registrations.
- `parameters[].validation` is enforced by `YamlToolFunction.ValidateAndNormalizeParameters`, and default values supplied via `value` are also normalized and validated.
- `display_options` influences Web UI rendering and `ExtendedAgentYamlUtils` output; review the settings whenever you add or update a tool.
