using Agent.Plugins.Implementation;
using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface ILogicAppsPlugin
    { 
        public Task<UpdateAppSettingResult> UpdateAppSetting(string resourceId, string key, string value);

        public Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId);

        Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName);

        Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId);

        public Task<string> ListRuns(string resourceId, string workflowName);

        public Task<string> ListRunActions(string resourceId, string workflowName, string runName);

        public Task<string> ListTriggers(string resourceId, string workflowName);

        public Task<string> ListActions(string resourceId, string workflowName);

        //public Task<IDictionary<string, string>> GetConnectionReferences(string resourceId);
    }
}
