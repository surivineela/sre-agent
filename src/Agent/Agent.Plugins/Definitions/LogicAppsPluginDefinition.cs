using System;
using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.VisualStudio.Services.Common;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.Deployment)]
    public class LogicAppsPluginDefinition
    {
        private readonly ILogicAppsPlugin _plugin;

        public LogicAppsPluginDefinition(ILogicAppsPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description("Gets the details of a Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<LogicAppDescriptor?> GetLogicAppInfoAsync(
            [Description("The ARM resource id of a Logic App")] string logicAppResourceId)
        {
            return await this._plugin.GetLogicAppInfoAsync(logicAppResourceId);
        }

        [WriteAction]
        [RequiresApproval]
        [Description("Update the app setting for a given Logic App")]
        public async Task<UpdateAppSettingResult> UpdateAppSetting(string resourceId, string key, string value)
        {
            return await this._plugin.UpdateAppSetting(resourceId, key, value);
        }

        [Description("Gets the list of triggers of a workflow of the Logic App using REST API")]
        public async Task<string> ListTriggers(
            [Description("The ARM resource id of a standard Logic App")]
            string resourceId,
            [Description("The workflow name inside the standard Logic App")]
            string workflowName)
        {
            return await _plugin.ListTriggers(resourceId, workflowName);
        }

        [Description("Gets the list of actions of a workflow of the Logic App using REST API")]
        public async Task<string> ListActions(
            [Description("The ARM resource id of a standard Logic App")]
            string resourceId,
            [Description("The workflow name inside the standard Logic App")]
            string workflowName)
        {
            return await _plugin.ListActions(resourceId, workflowName);
        }

        [Description("Gets the list of runs of a workflow of the Logic App using REST API")]
        public async Task<string> ListRuns(
            [Description("The ARM resource id of a standard Logic App")]
            string resourceId,
            [Description("The workflow name inside the standard Logic App")]
            string workflowName)
        {
            return await _plugin.ListRuns(resourceId, workflowName);
        }

        [Description("Gets a list of actions for a specific run of a flow in the Logic App using REST API")]
        public async Task<string> ListRunActions(
            [Description("The ARM resource id of a standard Logic App")]
            string resourceId,
            [Description("The workflow name inside the standard Logic App")]
            string workflowName,
            [Description("The run name")]
            string runName)
        {
            return await _plugin.ListRunActions(resourceId, workflowName, runName);
        }

        [Description("Get the list of managed connectors for a given Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyList<ManagedConnector>> GetManagedConnectors(string subscriptionId, string resourceGroupName, string logicAppName)
        {
            var workflows = await _plugin.ListWorkflowsAsync($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{logicAppName}");
            if (workflows == null || workflows.Count == 0)
            {
                return Array.Empty<ManagedConnector>();
            }

            var allConnectors = new Dictionary<string, ManagedConnector>();
            foreach (var workflow in workflows)
            {
                var connectors = await _plugin.GetManagedConnectorsByWorkflow(subscriptionId, resourceGroupName, logicAppName, workflow.Name);
                if (connectors != null && connectors.Count > 0)
                {
                    foreach (var connector in connectors)
                    {
                        allConnectors.TryAdd(connector.Id.ToLowerInvariant(), connector);
                    }
                }
            }

            return allConnectors.Values.ToArray();
        }

        [Description("Gets the list of workflows in a Logic App")]
        public async Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId)
        {
            return await _plugin.ListWorkflowsAsync(logicAppResourceId);
        }

        [Description("Looks up a service provider connector equivalent for a managed connector.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(
            [Description("Managed connector ID (e.g., managedApis/sftpwithssh).")] string managedConnectorId)
        {
            return await _plugin.LookupServiceProviderConnectorEquivalent(managedConnectorId);
        }

        [Description("Gets the missing Diagnostic Settings categories of the Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyList<string>> GetMissingDiagnosticSettingsAsync(
            [Description("The ARM resource id of a Logic App")] string resourceId)
        {
            return await _plugin.GetMissingDiagnosticSettingsAsync(resourceId);
        }

        [Description("Check if Easy Auth is enabled for a given Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> IsEasyAuthEnabled(
            [Description("The ARM resource id of a Logic App")] string resourceId)
        {
            return await _plugin.IsEasyAuthEnabledAsync(resourceId);
        }

        [Description("Check if Application Insights is configured for a given Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> IsApplicationInsightsConfigured(
            [Description("The ARM resource id of a Logic App")] string resourceId)
        {
            return await _plugin.IsApplicationInsightsConfiguredAsync(resourceId);
        }

        [Description("Check if Extension Bundle version is pinned for a given Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<bool> IsExtensionBundleVersionPinned(
            [Description("The ARM resource id of a Logic App")] string resourceId)
        {
            return await _plugin.IsExtensionBundleVersionPinnedAsync(resourceId);
        }

        [Description("Get the list of workflows with HTTP Request trigger in a Logic App")]
        [AgentTool(ToolMode.Auto)]
        public async Task<IReadOnlyList<Workflow>> ListHttpRequestTriggerWorkflows(
            [Description("The ARM resource id of a Logic App")] string logicAppResourceId)
        {
            return await _plugin.ListHttpRequestTriggerWorkflowsAsync(logicAppResourceId);
        }
    }
}
