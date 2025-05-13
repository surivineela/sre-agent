# SRE ICM Agent Configuration

## Overview

The SRE Agent is a configurable, chat-based tool integrated with Microsoft Teams to streamline and automate incident management. It operates based on a JSON configuration file deployed to a production endpoint and interacts with incidents via the Incident Management Service (ICM) and Geneva actions. The agent supports multiple operational modes, enabling a gradual transition from manual oversight to full automation.

---

## 1. Configuration & Deployment

### Configuration File

The configuration JSON allows users to:

* **Select Incidents**: Define criteria (e.g., severity, tags, service names) for which incidents the agent should handle.
* **Design Processing Workflows**: Outline step-by-step procedures for handling each incident, incorporating Kusto queries, ICM reads, Geneva actions, and custom scripts.
* **Set Conditional Logic**: Use query results or incident properties to determine next steps.
* **Manage Notifications & Escalations**: Configure when to notify teams, seek approvals, or escalate issues.


---

### ICMAlertConfig Properties

| Property                          | Type                              | Description                                                                                   |
|------------------------------------|-----------------------------------|-----------------------------------------------------------------------------------------------|
| `id`                              | string                            | Unique identifier for the alert config.                                                       |
| `TeamId`                          | int                               | The owning team's numeric ID.                                                                 |
| `AlertingId`                      | string                            | The alerting rule or signal ID (for LSI/Azure Alerting).                                      |
| `IncidentTitle`                   | string (nullable)                 | Exact incident title to match (for CRI-based configs).                                        |
| `IncidentTitleContains`           | string (nullable)                 | Substring to match in incident titles (for CRI-based configs).                                |
| `OwningTeams`                     | List<string>                      | List of team names that own this alert.                                                       |
| `AgentMode`                       | string                            | The operational mode of the agent (e.g., "Sev2", "ICMAgent").                                 |
| `UseCorrelationIdForKustoQuery`   | bool                              | If true, use the incident's correlation ID in Kusto queries.                                  |
| `GenevaActions`                   | List<GenevaActionConfigBase>      | List of Geneva actions (see below) that can be executed for this alert.                       |
| `AllowedGenevaActions`            | List<string>                      | List of allowed Geneva action names for this alert.                                           |
| `KustoQueries`                    | List<ICMConfigKustoQueryModel>    | List of Kusto queries to run for this alert (see below).                                      |
| `Owners`                          | List<string>                      | List of owners (e.g., email addresses or aliases).                                            |
| `ActionTimeoutIntervalInMinutes`  | int                               | Timeout for actions in minutes.                                                               |
| `DefaultHumanInterventionLoop`    | string                            | Default escalation or intervention loop name.                                                 |
| `RoutingInstructions`             | List<string>                      | Instructions for routing the incident.                                                        |
| `MitigationInstructions`          | List<string>                      | Instructions for mitigating the incident.                                                     |
| `MonitoringInstructions`          | List<string>                      | Instructions for monitoring the incident.                                                     |
| `IncidentProcessingGuide`         | List<string>                      | General guide or checklist for incident processing.                                           |

---

### GenevaActionConfig

Defines a Geneva action that can be executed as part of incident handling.

| Property                  | Type            | Description                                              |
|---------------------------|-----------------|----------------------------------------------------------|
| `ActionName`              | string          | Unique name of the Geneva action.                        |
| `TenantId`                | string          | Tenant ID for the workflow.                              |
| `WorkflowName`            | string          | Name of the workflow to execute.                         |
| `WorkflowInputParameters` | List<string>    | List of required input parameter names.                  |
| `IsWriteAction`           | bool            | Indicates if the action performs a write operation.      |
| `IsAllowedOnExternalSubs` | bool            | If true, action is allowed on external subscriptions.    |

---

### ICMConfigKustoQueryModel

Defines a Kusto query to be executed for the alert.

| Property      | Type    | Description                                 |          |
|---------------|---------|---------------------------------------------|----------|
| `Title`       | string  | Title or description of the query.          |          |
| `KustoQuery`  | string  | The Kusto query string.                     |          |
| `Cloud`       | string  | Cloud environment (e.g., "AzurePublic").    | Optional |
| `Cluster`     | string  | Kusto cluster name.                         | Optional |
| `Database`    | string  | Kusto database name.                        | Optional |

---

## Examples of ICMAlertConfig JSON

- [Triaging Customer Reported Incidents (CRIs) with Kusto Queries and simple Instructions](/src/Agent/FirstPartyAgent.Core/ICMAlertConfigs/000fa255-4323-41ce-ac8a-369e6bfe0284.json)
- [Handling Live Site Incidents with Geneva Actions](/src/Agent/FirstPartyAgent.Core/ICMAlertConfigs/7c5a4fca-bd4d-4272-baa1-6f4abe6f24a2.json)
- [Handling Customer Reported Incidents (CRIs)](/src/Agent/FirstPartyAgent.Core/ICMAlertConfigs/f9110571-1b04-484b-b10f-ee3d130b1447.json)
- [Incident Enrichment Example](/src/Agent/FirstPartyAgent.Core/ICMAlertConfigs/c1168c0a-13db-43d0-8188-062d5c273c08.json)

---

## Usage Notes

- The SRE Agent reads this configuration to determine how to process, enrich, and automate incident handling.
- Geneva actions and Kusto queries are referenced by name and can be reused across multiple alert configs.
- The configuration supports both exact and partial incident title matching for CRI-based automation.
- Instructions and guides are provided as lists of strings for flexible, step-by-step automation or manual review.

---