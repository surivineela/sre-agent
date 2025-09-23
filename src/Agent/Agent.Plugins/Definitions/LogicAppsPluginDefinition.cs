using System;
using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
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
        public async Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId)
        {
            return await _plugin.LookupServiceProviderConnectorEquivalent(managedConnectorId);
        }
    }
}
