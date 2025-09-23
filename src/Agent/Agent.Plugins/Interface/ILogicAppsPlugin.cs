using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface ILogicAppsPlugin
    {
        public Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId);

        Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName);

        Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId);
    }
}
