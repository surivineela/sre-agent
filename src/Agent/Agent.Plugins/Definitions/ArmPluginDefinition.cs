// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
    [AgentToolPlugin(Category = ToolCategories.AzureOperation)]
    public class ArmPluginDefinition
    {
        private readonly IArmPlugin _armPlugin;

        public ArmPluginDefinition(IArmPlugin armPlugin)
        {
            _armPlugin = armPlugin;
        }

        [Description("Gets the TLS settings for a list of resources.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<TlsStatus>> GetTlsSettings(
            [Description("List of resource IDs to check the TLS minimum version for")]
            List<string> resourceIds)
        {
            return await _armPlugin.GetTlsSettings(resourceIds);
        }

        [Description("Checks if a resource exists in Azure.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> CheckIfResourceExists(
            [Description("The resource ID of the app.")]
            string appResourceId)
        {
            return await _armPlugin.CheckIfResourceExists(appResourceId);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Sets the minimum TLS version on a site resource")]
        public async Task<string> SetMinimumTlsVersion(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The minimum TLS version to set. Valid values: 1.2, 1.3")]
            string minimumTlsVersion)
        {
            return await _armPlugin.SetMinimumTlsVersion(appResourceId, minimumTlsVersion);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Restart an AppService app")]
        public async Task<string> RestartWebApp(
            [Description("The resource ID of the AppService app.")]
            string appResourceId)
        {
            return await _armPlugin.RestartWebApp(appResourceId)
                ? "Restart succeeded"
                : "Restart failed";
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Start an AppService app")]
        public async Task<string> StartWebApp(
            [Description("The resource ID of the AppService app.")]
            string appResourceId)
        {
            return await _armPlugin.StartWebApp(appResourceId)
                ? "Start succeeded"
                : "Start failed";
        }

        [Description("Get ARM properties of a resource as JSON")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetArmResourceAsJson(
            [Description("Full resource id of an Azure resource")] string resourceId)
        {
            return await _armPlugin.GetArmResourceAsJson(resourceId);
        }

        [Description("Gets current VM instance view states (power/provisioning) from instanceView.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetVirtualMachineBootStateAsJson(
            [Description("Full resource id of an Azure virtual machine resource")] string resourceId)
        {
            return await _armPlugin.GetVirtualMachineBootStateAsJson(resourceId);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Power ON an Azure virtual machine")]
        public async Task<RemediationResult> PowerOnVirtualMachine(
            [Description("Full resource id of an Azure virtual machine resource")] string resourceId)
        {
            return await _armPlugin.PowerOnVirtualMachine(resourceId);
        }

        [Description("Get boot diagnostic logs and console screenshot for an Azure virtual machine")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(
            [Description("Full resource id of an Azure virtual machine resource")] string resourceId)
        {
            return await _armPlugin.GetVirtualMachineBootDiagnostics(resourceId);
        }

        [Description("Tests connectivity from function app to AzureWebJobsStorage. Only use this for connection string based authentication scenarios.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> CheckConnectivityToAzureWebJobsStorage(
            [Description("Full resource id of an Azure Function App")] string resourceId,
            [Description("The type of storage to connect to. Valid values: BlobStorage, QueueStorage, TableStorage")]
            string providerType = "BlobStorage")
        {
            return await _armPlugin.CheckConnectivityToAzureWebJobsStorage(resourceId, providerType);
        }

        [Description("Check if a connection from the given resource to the target host can be established.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> CheckTcpConnectivity(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("Host to test connectivity to")] string host,
            [Description("Port to test connectivity to")] int port)
        {
            return await _armPlugin.CheckTcpConnectivity(resourceId, host, port);
        }

        [Description("Performs DNS resolution test from an Azure resource to verify it can resolve a target URL's hostname")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> CheckDnsResolution(
            [Description("Full resource ID of the Azure resource from which to test DNS resolution")] string resourceId,
            [Description("The target URL or hostname to resolve (e.g. storageaccount.blob.core.windows.net)")] string destinationUrl)
        {
            return await _armPlugin.CheckDnsResolution(resourceId, destinationUrl);
        }

        [Description("Retrieves the resource IDs of all deployment slots for a given Azure resource")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<string>> GetDeploymentSlotsResourceIdsAsync(
            [Description("Full resource id of an Azure Resource")] string appServiceResourceId)
        {
            return await _armPlugin.GetDeploymentSlotsResourceIdsAsync(appServiceResourceId);
        }

        [Description("Retrieves the key value pair for given App Setting key")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IDictionary<string, string>> GetAppSetting(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("The App Setting key to look up")] string appSettingKey)
        {
            return await _armPlugin.GetAppSetting(resourceId, appSettingKey);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("For connection string based authentication only: Lists the keys for a given Azure Storage account and updates the specified App Setting in an App Service with the connection string. Call this only when the connection string must be updated for key-based authentication.")]
        public async Task<bool> ListKeysAndUpdateAppSettingsAsync(
            [Description("Full resource id of an Azure Storage account")] string storageResourceId,
            [Description("Full resource id of an Azure App Service")] string appServiceResourceId,
            [Description("The App Setting key to update with the storage account connection string")] string appSettingKey)
        {
            return await _armPlugin.ListKeysAndUpdateAppSettingsAsync(storageResourceId, appServiceResourceId, appSettingKey);
        }

        [RequiresApproval]
        [Description("Configures App Settings to use managed identity authentication for Azure WebJobs Storage in a Function App.")]
        public async Task<bool> ConfigureAppSettingsForManagedIdentityStorage(
            [Description("Full resource id of an Azure Function App")] string resourceId,
            [Description("The name of the Azure Storage account to use")] string storageAccountName,
            [Description("Whether to use a user-assigned managed identity")] bool useUserAssignedManagedIdentity = false,
            [Description("The client ID of the user-assigned managed identity")] string userManagedIdentityClientId = "")
        {
            return await _armPlugin.ConfigureAppSettingsForManagedIdentityStorage(resourceId, storageAccountName, useUserAssignedManagedIdentity, userManagedIdentityClientId);
        }

        [Description("Retrieves the Azure resource ID for a storage account from its storage service URI")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetResourceIdFromStorageServiceUri(
            [Description("The storage service URI (e.g., https://accountname.blob.core.windows.net)")] string storageServiceUri,
            [Description("The subscription ID where the storage account is located")] string subscriptionId)
        {
            return await _armPlugin.GetResourceIdFromStorageServiceUri(storageServiceUri, subscriptionId);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Updates specific configuration values in the App Settings for a given Azure resource. If the first attempt fails, automatically retry once without notifying the user.")]
        public async Task<bool> UpdateAppSettingsAsync(
            [Description("Full resource id of an Azure resource")] string resourceId,
            [Description("Key-value pairs of App Settings to update (only include settings that need to be changed)")] IDictionary<string, string> appSettings)
        {
            return await _armPlugin.UpdateAppSettingsAsync(resourceId, appSettings);
        }

        [Description("""
Execute az commands for Azure resource read operations. Commands run IMMEDIATELY without approval.
USAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.
ALLOWED: Read-only commands such as 'list', 'show', 'get'. (non-mutating only)
FORBIDDEN: 'aks command invoke' NOT allowed.
DO NOT USE for: DGrep queries, log analysis, diagnostic data, telemetry queries - use PerformDgrepSearch tool instead.
EXAMPLES:
- List: 'az containerapp list -g MyRG --subscription <subId>'
- Show with query: 'az containerapp show -g MyRG -n MyApp --query properties.configuration.ingress --subscription <subId>'
BEST PRACTICES:
- Use only if no specific tool available
- Always include --subscription parameter
- Executes immediately - no approval needed
- Use to understand current state before changes
- For log/diagnostic queries, use PerformDgrepSearch tool instead

# Pre-execution User Notification
  - Notify users concisely before executing any command (read, write, help):
     - "Checking [what] to [why]"
     - "Running [command type] to [purpose]"
  - For help lookups: explain briefly (e.g., "Finding the correct upgrade command using az help.")

## Step-by-Step Query Handling Process for Read Operations
  ### 1. Understand the Goal
        - Gather known context (subscription, resource group, resource names/IDs) from conversation history; only prompt for what's missing.
        - Restate user objectives and distinguish read vs write actions.
        - For write actions, plan to understand current state first.
  ### 2. Formulate Investigation Plan
        - Decide which read commands to run and which properties to inspect (e.g., SKU/tier, capacity, tags, network rules, dependencies).
        - Prefer resource IDs when referencing related resources.
  ### 3. Execute Read Commands
        - Run necessary list/show/get, and any other non-mutating commands immediately.
        - Include --subscription in commands; use --query to focus output.
        - For existence checks: az [service] show ... 2>/dev/null || echo "Not found".
  ### 4. Analyze Current State
        - Parse and document configuration, state, and dependencies.
        - Determine difference between current and desired state.
  ### 5. Summary
        - Summarize what was investigated, changed, and the final state.
        - Suggest next steps or recommendations.

# Troubleshooting and Error-Handling Playbook
  ## Read Command Failures
  - Broaden scope (remove `--query`).
  - Verify spelling and case sensitivity.
  - Check resource group and resource names.
  - Try using `--ids`.
  - If issues persist, list parent resources first (e.g., list plans before running `webapp show`).
  - Run `GetAzCliHelpAsync` for command help. If further clarification is needed, use `SearchDocuments`.
""")]
        [AgentTool(ToolMode.Manual)]
        public async Task<CliToolExecutionResult> RunAzCliReadCommandsAsync(
    [Description("Complete az command string for read operations (list, show, get)")] string command)
        {
            return await _armPlugin.RunAzCliReadCommandsAsync(command);
        }

        [WriteAction]
        [Description("""
Execute az commands for Azure resource write operations. Requires user approval before execution.
USAGE: Provide complete az cli command string. ALWAYS specify --subscription parameter with valid subscriptionId/guid.
ALLOWED: 'create', 'update', 'set', 'scale', 'start', 'stop', 'restart', 'add'
FORBIDDEN: 'delete', 'remove', 'aks command invoke' commands NOT allowed for safety.
DO NOT USE for: DGrep queries, log analysis, diagnostic data, telemetry queries - use PerformDgrepSearch tool instead.
EXAMPLES:
- Create: 'az containerapp create -g MyRG -n MyApp --subscription <subId> --image myimage:latest'
- Update: 'az webapp update -g MyRG -n MyApp --set httpsOnly=true --subscription <subId>'
- Scale: 'az webapp scale -g MyRG -n MyApp --instance-count 3 --subscription <subId>'
BEST PRACTICES:
- Run read command first to understand current state
- Explain what will change
- Include rollback commands when possible
- Requires USER APPROVAL before execution

# Pre-execution User Notification
  - Notify users concisely before executing any command (read, write, help):
     - "Checking [what] to [why]"
     - "Running [command type] to [purpose]"
  - For help lookups: explain briefly (e.g., "Finding the correct upgrade command using az help.")

## Step-by-Step Query Handling Process for Read and Write Operations
  ### 1. Understand the Goal
        - Gather known context (subscription, resource group, resource names/IDs) from conversation history; only prompt for what's missing.
        - Restate user objectives and distinguish read vs write actions.
        - For write actions, plan to understand current state first.
  ### 2. Formulate Investigation Plan
        - Decide which read commands to run and which properties to inspect (e.g., SKU/tier, capacity, tags, network rules, dependencies).
        - Prefer resource IDs when referencing related resources.
  ### 3. Execute Read Commands
        - Run necessary list/show/get, and any other non-mutating commands immediately.
        - Include --subscription in commands; use --query to focus output.
        - For existence checks: az [service] show ... 2>/dev/null || echo "Not found".
  ### 4. Analyze Current State
        - Parse and document configuration, state, and dependencies.
        - Determine difference between current and desired state.
  ### 5. Get Command Help (Write Operations)
        - Always call GetAzCliHelpAsync for the targeted operation (e.g., helpTopic="webapp scale").
        - Use grepPattern for key parameters if useful.
        - If help is insufficient, escalate per Help Command Strategy; use SearchDocuments if needed.
  ### 6. Construct Write Command
        - Build with verified parameters; ensure minimal, idempotent changes; never include delete operations.
        - Prefer resource IDs where possible.
        - Prepare rollback commands.
  ### 7. Validate and Assess Impact
        - Confirm permissibility (no delete/remove commands).
        - Provide a risk matrix: availability, security, performance, cost, compliance, blast radius.
        - Outline rollback paths and status-check commands for asynchronous operations.
  ### 8. Request Approval (Write Operations)
        - Present current state, proposed changes, exact commands, impact assessment, and rollback plan.
        - Wait for explicit approval before executing.
  ### 9. Execute Write Command (After Approval)
        - Call RunAzCliWriteCommandsAsync for approved commands only.
        - For long-running operations, use --no-wait and provide progress commands.
        - Validate and handle errors, including retries or fallbacks if required.
  ### 10. Verify Changes
        - Run follow-up read commands; confirm before/after state.
        - For async operations, communicate progress/status and expected timelines.
  ### 11. Summary
        - Summarize what was investigated, changed, and the final state.
        - Suggest next steps or recommendations.

# Troubleshooting and Error-Handling Playbook
  ## Read Command Failures
  - Broaden scope (remove `--query`).
  - Verify spelling and case sensitivity.
  - Check resource group and resource names.
  - Try using `--ids`.
  - If issues persist, list parent resources first (e.g., list plans before running `webapp show`).
  - Run `GetAzCliHelpAsync` for command help. If further clarification is needed, use `SearchDocuments`.
""")]
        [AgentTool(ToolMode.Manual)]
        public async Task<CliToolExecutionResult> RunAzCliWriteCommandsAsync(
            [Description("Complete az command string for write operations (create, update, set, scale, start, stop, restart)")] string command)
        {
            return await _armPlugin.RunAzCliWriteCommandsAsync(command);
        }

        [Description("""
Get Azure CLI help information with optional text filtering. Used internally to validate and correct command syntax.
USAGE: Provide the Azure CLI command/topic to get help for, with optional search pattern to filter results.
PURPOSE: This tool helps the agent understand correct command syntax and parameters to fix invalid commands.
FILTERING: The optional pattern searches through the help text and returns only lines containing that text.
EXAMPLES:
- Get help for webapp: 'webapp'
- Get help for specific subcommand: 'webapp create'
- Filter help for location info: 'webapp create' with pattern 'location' (returns only help lines mentioning 'location')
- Filter for parameter info: 'containerapp' with pattern '--cpu' (returns only lines about CPU parameters)
NOTE: This is an internal tool for command validation, not for generating user documentation.
## Help Command Strategy
- When you need to get command help, follow this reasoning pattern:
### Help Command Chain of Thought:
- Initial Help Attempt: "Let me figure out the command syntax for [specific operation]"
- If Help Not Found: "That specific command wasn't found. Let me try the broader [service] category"
- If Still Not Found: "Let me check the parent [service-group] commands"
- Final Fallback: "Let me search for the base [service] help"
### Help Command Hierarchy (Max 6 attempts):
- Attempt 1: Specific operation (e.g., "webapp scale")
- Attempt 2: Service level (e.g., "webapp")
- Attempt 3: Service group (e.g., "app")
- Attempt 4: Base service (e.g., "az webapp --help" equivalent)
""")]
        [AgentTool(ToolMode.Manual)]
        public async Task<string> GetAzCliHelpAsync(
            [Description("The Azure CLI command/topic to get help for (e.g., 'webapp', 'containerapp create')")] string helpTopic,
            [Description("Optional search pattern to filter help output - returns only lines containing this text")] string grepPattern = "")
        {

            return await _armPlugin.GetAzCliHelpAsync(helpTopic, grepPattern);
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Enables (brings online) an Azure Traffic Manager endpoint")]
        public async Task<string> EnableTrafficManagerEndpoint(
            [Description("The subscription ID containing the Traffic Manager profile")] string subscriptionId,
            [Description("The name of the resource group containing the Traffic Manager profile")] string resourceGroupName,
            [Description("The name of the Traffic Manager profile")] string profileName,
            [Description("The name of the endpoint to enable")] string endpointName,
            [Description("The type of endpoint (e.g., 'azureEndpoints', 'externalEndpoints', 'nestedEndpoints')")] string endpointType)
        {
            var result = await _armPlugin.EnableTrafficManagerEndpoint(subscriptionId, resourceGroupName, profileName, endpointName, endpointType);
            return result.Item1 ? result.Item2 : $"Failed to enable endpoint: {result.Item2}";
        }

        [RequiresApproval]
        [WriteAction]
        [Description("Disables (takes offline) an Azure Traffic Manager endpoint")]
        public async Task<string> DisableTrafficManagerEndpoint(
            [Description("The subscription ID containing the Traffic Manager profile")] string subscriptionId,
            [Description("The name of the resource group containing the Traffic Manager profile")] string resourceGroupName,
            [Description("The name of the Traffic Manager profile")] string profileName,
            [Description("The name of the endpoint to disable")] string endpointName,
            [Description("The type of endpoint (e.g., 'azureEndpoints', 'externalEndpoints', 'nestedEndpoints')")] string endpointType)
        {
            var result = await _armPlugin.DisableTrafficManagerEndpoint(subscriptionId, resourceGroupName, profileName, endpointName, endpointType);
            return result.Item1 ? result.Item2 : $"Failed to disable endpoint: {result.Item2}";
        }

        [Description("Gets the status of all endpoints in a Traffic Manager profile with health summary")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetAllTrafficManagerEndpointsStatus(
            [Description("The subscription ID containing the Traffic Manager profile")] string subscriptionId,
            [Description("The name of the resource group containing the Traffic Manager profile")] string resourceGroupName,
            [Description("The name of the Traffic Manager profile")] string profileName)
        {
            return await _armPlugin.GetAllTrafficManagerEndpointsStatus(subscriptionId, resourceGroupName, profileName);
        }

        [RequiresApproval]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [Description("Enables (brings online) an Azure Front Door endpoint origin")]
        public async Task<string> EnableAzureFrontDoorEndpointOrigin(
            [Description("The subscription ID containing the Azure Front Door profile")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Front Door profile")] string resourceGroupName,
            [Description("The name of the Azure Front Door profile")] string frontDoorProfileName,
            [Description("The name or hostname of the endpoint")] string endpointNameOrHostName,
            [Description("The name of the origin to enable")] string originName)
        {
            var result = await _armPlugin.EnableAzureFrontDoorEndpointOrigin(subscriptionId, resourceGroupName, frontDoorProfileName, endpointNameOrHostName, originName);
            return result.Item1 ? result.Item2 : $"Failed to enable origin: {result.Item2}";
        }

        [RequiresApproval]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [Description("Disables (takes offline) an Azure Front Door endpoint origin")]
        public async Task<string> DisableAzureFrontDoorEndpointOrigin(
            [Description("The subscription ID containing the Azure Front Door profile")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Front Door profile")] string resourceGroupName,
            [Description("The name of the Azure Front Door profile")] string frontDoorProfileName,
            [Description("The name or hostname of the endpoint")] string endpointNameOrHostName,
            [Description("The name of the origin to disable")] string originName)
        {
            var result = await _armPlugin.DisableAzureFrontDoorEndpointOrigin(subscriptionId, resourceGroupName, frontDoorProfileName, endpointNameOrHostName, originName);
            return result.Item1 ? result.Item2 : $"Failed to disable origin: {result.Item2}";
        }

        [Description("Gets the status of all origins across endpoints in an Azure Front Door profile with health summary")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetAllAzureFrontDoorEndpointOriginsStatus(
            [Description("The subscription ID containing the Azure Front Door profile")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Front Door profile")] string resourceGroupName,
            [Description("The name of the Azure Front Door profile")] string frontDoorProfileName)
        {
            return await _armPlugin.GetAllAzureFrontDoorEndpointOriginsStatus(subscriptionId, resourceGroupName, frontDoorProfileName);
        }

        [RequiresApproval]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [Description("Starts (runs) an Azure Data Factory pipeline")]
        public async Task<string> RunAzureDataFactoryPipeline(
            [Description("The subscription ID containing the Azure Data Factory")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Data Factory")] string resourceGroupName,
            [Description("The name of the Azure Data Factory")] string dataFactoryName,
            [Description("The name of the pipeline to run")] string pipelineName)
        {
            var result = await _armPlugin.RunAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
            return result.Item1 ? result.Item2 : $"Failed to run pipeline: {result.Item2}";
        }

        [RequiresApproval]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [Description("Stops (cancels) an Azure Data Factory pipeline")]
        public async Task<string> StopAzureDataFactoryPipeline(
            [Description("The subscription ID containing the Azure Data Factory")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Data Factory")] string resourceGroupName,
            [Description("The name of the Azure Data Factory")] string dataFactoryName,
            [Description("The name of the pipeline to stop")] string pipelineName)
        {
            var result = await _armPlugin.StopAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
            return result.Item1 ? result.Item2 : $"Failed to stop pipeline: {result.Item2}";
        }

        [RequiresApproval]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [Description("Restarts an Azure Data Factory pipeline (stops and then starts)")]
        public async Task<string> RestartAzureDataFactoryPipeline(
            [Description("The subscription ID containing the Azure Data Factory")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Data Factory")] string resourceGroupName,
            [Description("The name of the Azure Data Factory")] string dataFactoryName,
            [Description("The name of the pipeline to restart")] string pipelineName)
        {
            var result = await _armPlugin.RestartAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
            return result.Item1 ? result.Item2 : $"Failed to restart pipeline: {result.Item2}";
        }

        [Description("Gets the status of all pipelines in an Azure Data Factory with execution details")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetAllAzureDataFactoryPipelinesStatus(
            [Description("The subscription ID containing the Azure Data Factory")] string subscriptionId,
            [Description("The name of the resource group containing the Azure Data Factory")] string resourceGroupName,
            [Description("The name of the Azure Data Factory")] string dataFactoryName)
        {
            return await _armPlugin.GetAllAzureDataFactoryPipelinesStatus(subscriptionId, resourceGroupName, dataFactoryName);
        }
    }
}
