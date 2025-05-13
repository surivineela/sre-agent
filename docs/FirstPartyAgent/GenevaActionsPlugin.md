# GenevaActionsPlugin User Documentation

## Overview

The `GenevaActionsPlugin` class provides functionality to execute Geneva Actions—predefined automated workflows—using a single entry point. It supports dynamic configuration from CosmosDB, Azure Storage, or local files, and performs several validations before executing an action. This plugin is designed for use in automated agents and integrates with logging, Teams notifications, and session messaging.

---

## Configuration

Geneva Actions are configured via a prioritized lookup:
1. **CosmosDB**: If enabled, the plugin loads Geneva Actions from a CosmosDB container (`GenevaActionsConfigs`).
2. **Azure Storage**: If enabled, the plugin loads from a blob (default: `genevaactionsconfig/GenevaActions.json`).
3. **Local File**: If neither CosmosDB nor Storage is available, the plugin loads from a local file at `Plugins/GenevaActionsPlugin/GenevaActions.json`.

The plugin uses the first available configuration source with valid data.

---

## Kernel Functions

### ExecuteGenevaAction

Executes a Geneva Action workflow by name, with the provided input parameters.

**Signature:**
```csharp
[KernelFunction("execute_geneva_action")] public async Task<string> ExecuteGenevaAction( [Description("Action Name")] string actionName, [Description("Input Parameters")] Dictionary<string, string> inputParameters, Kernel kernel)
```


**Input Parameters:**
- `actionName` (string): The name of the Geneva Action to execute.
- `inputParameters` (Dictionary<string, string>): Key-value pairs required by the action.

**Description:**
- Looks up the Geneva Action configuration by `actionName`.
- Validates that all required input parameters for the action are present.
- Checks if the action is a write action and if the workflow client is in read-only mode.
- If the action requires an internal subscription, validates the subscription using a Kusto query.
- Executes the Geneva Action workflow and returns the result or error message.

---

## Validations Performed

1. **Action Existence**:  
   - If the specified `actionName` does not exist in the loaded configuration, the plugin returns an error message.

2. **Required Parameters**:  
   - The plugin checks that all required workflow input parameters (as defined in the action config) are present in `inputParameters`.
   - If any are missing, it returns a message listing the required parameters.

3. **Read-Only Mode**:  
   - If the workflow client is in read-only mode and the action is a write action, the plugin returns `"Success. ICM Workflow Client is in ReadOnly mode."` without executing the action.

4. **Subscription Validation**:  
   - If the action is not allowed on external subscriptions, and a `subscriptionId` or `subscription` parameter is provided, the plugin checks if the subscription is internal using a Kusto query.
   - If the subscription is external, the action is not executed and a message is returned.

5. **Execution and Logging**:  
   - All major steps and errors are logged.
   - The plugin integrates with Teams and session messaging for operational transparency.

---

## Defining Geneva Actions in Config

GenevaActions.json Schema
•	GenevaActions: Array of Geneva Action definitions.
•	ActionName (string, required): Unique name for the action.
•	Description (string, optional): Human-readable description of the action.
•	WorkflowName (string, required): Name of the workflow to execute.
•	TenantId (string, required): Tenant ID to use for the workflow.
•	WorkflowInputParameters (array of string, required): List of required input parameter names.
•	IsWriteAction (boolean, required): Indicates if the action performs a write operation.
•	IsAllowedOnExternalSubs (boolean, required): Whether the action is allowed on external subscriptions.


Example:

```json
{
  "ActionName": "RestartWebApp",
  "Description": "Restarts a specified Azure Web App.",
  "WorkflowName": "Workflow-RestartWebApp",
  "TenantId": "your-tenant-id",
  "WorkflowInputParameters": [
    "subscriptionId",
    "webappName",
    "webspaceName"
  ],
  "IsWriteAction": true,
  "IsAllowedOnExternalSubs": false
}
```