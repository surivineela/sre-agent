# Workflow Orchestrator (RCARouterAgent) Onboarding

This guide explains how to enable the workflow orchestrator, author workflow YAML, and integrate automated ICM-driven RCA. The orchestrator is a lightweight workflow layer on top of AgentV2 optimized for Azure Functions RCA where many KQL steps must run consistently with minimal parameters and stable prompts. It is planned to converge with Eben’s RCA future work.

## Enable the feature

You enable the orchestrator via an environment variable (feature flag):

- AGENT_TYPE_NAME=RCARouterAgent

Ways to set it:

- Use the existing HTTPS profile in `src/Agent/Agent.Web/Properties/launchSettings.json` which already includes `"AGENT_TYPE_NAME": "RCARouterAgent"`.
- Or set it manually before running Agent.Web:

```powershell
$env:AGENT_TYPE_NAME="RCARouterAgent"
dotnet run --project src/Agent/Agent.Web/Agent.Web.csproj -- --session --legacy
```

When this is set, `ReasoningLoopFactory` selects the workflow path and delegates to `WorkflowOrchestrator` for execution.

## Where YAML agents are loaded from

YAML agents are discovered and loaded by the factory:

- Agent discovery: `Agent.Framework.AgentFactory`
- First‑party vs non‑first‑party: depending on your mode, the app scans a narrower first‑party folder or the broader `AgentsV2` tree. If a YAML doesn’t show up:
  - Ensure it resides under the scanned directory for your mode, or
  - Explicitly load an extra folder via `AgentFactory.LoadYamlAgentsFromFolder(...)`.

Note: YAML parsing is strict by default; avoid unknown fields unless the descriptor supports them.

## YAML schema (orchestrator and activity agents)

The orchestrator recognizes the following fields (subset):

- Common:
  - `name`: unique agent name
  - `system_prompt`: system instructions for the LLM
  - `tools`: list of tool names the agent can call
  - `handoffs`: optional handoff targets
  - `user_prompt_override`: use if your instruction was not works properly
- Workflow-specific:
  - `agent_type`: `Orchestrator` or `Activity`
  - `parameter_extraction_agent`: run first to extract parameters (Orchestrator)
  - `orchestration_start_agents`: initial activity agents (Orchestrator)
  - `result_summarization_prompt`: optional summarization
  - `output_type`: for activity agents set to `WorkflowActivityLLMOutput`
  - `next_agent_mappings`: conditional routing rules for activities

### Minimal orchestrator example

```yaml
name: functions_rca_orchestrator
system_prompt: |
  You orchestrate Functions RCA analysis by invoking activity agents and summarizing outcomes.
agent_type: Orchestrator

# Extract parameters first from incident/context
parameter_extraction_agent: BlobTriggerParameterExtractionAgent

# Kick off the first step(s)
orchestration_start_agents:
  - BlobTriggerPreflightAgent

result_summarization_prompt: |
  Summarize key findings and next steps for the user.

tools: []
handoffs: []
```

Note: In this repository, `rca_router_meta_agent` is not an orchestrator; use a dedicated orchestrator agent name like `functions_rca_orchestrator`.

### Activity agent example (Blob Trigger preflight)

```yaml
name: BlobTriggerPreflightAgent
system_prompt: |
  Perform preflight checks for Blob Trigger–based Functions and propose next steps.
agent_type: Activity
output_type: "WorkflowActivityLLMOutput"

tools:
  - CheckFunctionRuntimeAndWorkers
  - GetMostFrequentEventIpAddress
  - ShareAgentResult

next_agent_mappings:
  - condition: "records_found"
    next_agents:
      - bt_select_event_ip_agent
      - bt_determine_function_has_been_triggered_agent
      - bt_polling_check_agent
  - condition: "no_records"
    next_agents: []
```

Activity outputs must conform to `WorkflowActivityLLMOutput` (the runtime merges parameters and routes to the next agents via `next_agent_mappings` or model‐provided `NextSteps`).

## Execution flow

- With `AGENT_TYPE_NAME=RCARouterAgent`, `ReasoningLoopFactory` switches to a workflow loop and instantiates `WorkflowOrchestrator`.
- The orchestrator optionally runs a parameter extraction step, then executes `orchestration_start_agents`, recursively following `NextSteps`/`next_agent_mappings`.
- Each step’s output (`WorkflowActivityLLMOutput`) contains:
  - `Analysis`: short narrative
  - `Parameters`: JSON map (merged into the orchestrator context)
  - `NextSteps`: array of next activity agent names
- At completion, it can produce a summary using `result_summarization_prompt`.

Relevant code:

- `Agent.Runtime/Reasoning/ReasoningLoopFactory.cs`
- `Agent.Runtime/Reasoning/WorkflowOrchestrator.cs`
- `Agent.Runtime/Reasoning/WorkflowActivityAgentOutput.cs` (a.k.a. WorkflowActivityLLMOutput)
- `Agent.Framework/YamlAgentDescriptor.cs`

## ICMScanner integration and AutomatedRCA

The ICM scanner can automatically trigger workflow‑based RCA for targeted incidents and post links back to the web UI.

Configuration shape (see `Agent.Core.Configuration.IncidentManagementSettings`):

```json
{
  "IncidentManagement": {
    "Type": "Icm",
    "AutomatedRCA": {
      "Enabled": true,
      "WebBaseUrl": "https://localhost:7023"
    },
    "ICMAPI": {
      "APIEndpoint": "<icm-api-endpoint>",
      "IcmMSIResource": "api://icmapi-prod",
      "OwningServiceId": "<service-id>",
      "UserToken": "<ICMUserToken from your browser here>",
      "ReadOnly": true
    }
  }
}
```

Notes:
- For local testing, you need to get `UserToken` for the ICM. Go to ICM page and Go `MoreTools` > `Developer Tools` to find bearer token. It will be available for a couple of hours.
- AutomatedRCA.Enabled toggles the scanner’s automation; WebBaseUrl is used to build links to agent threads.

### Enable filters via IncidentPlaygroundController (OwningTeamId)

To make the ICM scanner pick up incidents, you must create and enable an Incident Filter that includes your target OwningTeamId. This is done via the IncidentPlaygroundController.

- Create or update a filter using the IncidentFilterDocumentPayload (note the `OwningTeamId`):
- Filtering by `OwnerName` (or OwningTeam) limits which incidents kick off the workflow.
- If `ReadOnly` is true, the scanner avoids mutating ICM and posts status to the agent thread instead.

### Local enablement (HTTP profile + incident filter)

If you also want to run with the plain `http` launch profile (instead of `https`) and still enable the workflow orchestrator, add the same environment variable to the `http` profile inside `src/Agent/Agent.Web/Properties/launchSettings.json`:

```jsonc
"profiles": {
  "http": {
    "commandName": "Project",
    "commandLineArgs": "--session --legacy",
    "launchBrowser": true,
    "launchUrl": "static/",
    "applicationUrl": "http://localhost:5073",
    "environmentVariables": {
      "ASPNETCORE_ENVIRONMENT": "Development",
      "AGENT_ENDPOINT": "http://localhost:5073/",
      "AGENT_TYPE_NAME": "RCARouterAgent"
    }
  }
}
```

### Create an incident filter (OwningTeam scoped)

Register (or update) an incident filter so the ICM scanner auto‑starts RCA for matching incidents. Use an arbitrary unique filter id (here `filter-005`). Send this payload over **HTTP** (not HTTPS) because the dev certificate may be rejected by simple clients.

Endpoint:

```
POST http://localhost:5073/api/v1/IncidentPlayground/filters/filter-005
```

Body:

```json
{
  "id": "filter-005",
  "name": "BlobTrigger CRI RCA Filter",
  "incidentType": "CustomerReported",
  "isEnabled": true,
  "agentMode": "",
  "owningTeamId": "84433"
}
```

After enabling this filter, any new Active incident with `OwningTeamId` = `84433` will automatically trigger the workflow RCA (subject to `AutomatedRCA.Enabled`).

Optional PowerShell helper:

```powershell
$payload = @{
  id = "filter-005"
  name = "BlobTrigger CRI RCA Filter"
  incidentType = "CustomerReported"
  isEnabled = $true
  agentMode = ""
  owningTeamId = "84433"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5073/api/v1/IncidentPlayground/filters/filter-005" `
  -Method Post `
  -ContentType "application/json" `
  -Body $payload
```

Once created you can still call the enable endpoint if needed:

```
POST http://localhost:5073/api/v1/IncidentPlayground/filters/filter-005/enable
```


Scanner behavior (code pointers):

- `Agent.Runtime/SubAgents/IcmScanner/IcmScanner.cs` — fetches incidents, applies filters, de‑dupes by tags, starts threads, and monitors completion.
- `Agent.Core/Services/ICMAPIClient.cs` — ICM API implementation and constraints (e.g., OwningTeam filters may require IncidentType).

## Troubleshooting

- YAML not loading:
  - Confirm your YAML resides under the scanned directory (first‑party vs non‑first‑party).
  - Ensure schema matches `YamlAgentDescriptor` (remove unknown top‑level keys or extend the descriptor and deserializer to accept them).
- Orchestrator doesn’t route:
  - Check `output_type: WorkflowActivityLLMOutput` and `next_agent_mappings` conditions/agent names.
- ICM automation no‑ops:
  - Verify `AutomatedRCA.Enabled=true`, filters match your incidents, and the service has API access.

## Quick start

Place your workflow YAML under the scanned folder (e.g., `AgentsV2/RCA/BlobTriggerPreflight`) and start a new thread. The orchestrator will run `parameter_extraction_agent` (if any) and then `orchestration_start_agents` (e.g., `BlobTriggerPreflightAgent`).

Start `Agent.Web` project with the `launchSettings.json` with following:

```json
    "https": {
      "commandName": "Project",
      "commandLineArgs": "--session --legacy",
      "launchBrowser": true,
      "launchUrl": "static/",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "AGENT_ENDPOINT": "https://localhost:7023/",
        "AGENT_TYPE_NAME": "RCARouterAgent"
      },
```

- Explicitly enable the filter (ensures it’s active for the scanner):

Endpoints used:

- `PUT /api/v1/incidentplayground/filters/{filterId}` — create filter
- `POST /api/v1/incidentplayground/filters/{filterId}` — update filter
- `POST /api/v1/incidentplayground/filters/{filterId}/enable` — enable filter
- `POST /api/v1/incidentplayground/filters/{filterId}/disable` — disable filter

Internals and models:

- `IncidentPlaygroundController` persists filters via `IIncidentFilterManagementService`
- `IncidentFilterDocumentPayload` contains `OwningTeamId` (required to target your team’s incidents)
- Enabled filters are used by `IcmScanner`; without any enabled filters, logs will show “No incident filters found, skipping IcM scanner.”

Post following on the web:

This is a interactive testing for a workflow.

```dotnetcli
IncidentId: <IncidentId>
```

e.g. `IncidentId: 665975009`.

**References:**

- [`Agent.Runtime.Reasoning.ReasoningLoopFactory`](src/Agent/Agent.Runtime/Reasoning/ReasoningLoopFactory.cs)
- [`Agent.Runtime.Reasoning.WorkflowReasoningLoop`](src/Agent/Agent.Runtime/Reasoning/WorkflowReasoningLoop.cs)
- [`Agent.Runtime.Reasoning.WorkflowOrchestrator`](src/Agent/Agent.Runtime/Reasoning/WorkflowOrchestrator.cs)
- [`Agent.Framework.YamlAgentDescriptor`](src/Agent/Agent.Framework/YamlAgentDescriptor.cs)
- [`Agent.Runtime.Reasoning.WorkflowActivityLLMOutput`](src/Agent/Agent.Runtime/Reasoning/WorkflowActivityAgentOutput.cs)
- [`Agent.Framework.Models.NextAgentMapping`](src/Agent/Agent.Framework/Models/NextAgentMapping.cs)
- [`Agent.Runtime.SubAgents.IcmScanner.IcmScanner`](src/Agent/Agent.Runtime/SubAgents/IcmScanner/IcmScanner.cs)
- [`Agent.Core.Configuration.IncidentManagementSettings`](src/Agent/Agent.Core/Configuration/IncidentManagementSettings.cs)
