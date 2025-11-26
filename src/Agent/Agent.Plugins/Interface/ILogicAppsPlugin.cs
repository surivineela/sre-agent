using Agent.Plugins.Implementation;
using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    public interface ILogicAppsPlugin
    {
        public Task<LogicAppDescriptor?> GetLogicAppInfoAsync(string logicAppResourceId);

        public Task<UpdateAppSettingResult> UpdateAppSetting(string resourceId, string key, string value);

        public Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId);

        Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName);

        Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId);

        public Task<string> ListRuns(string resourceId, string workflowName);

        public Task<string> ListRunActions(string resourceId, string workflowName, string runName);

        public Task<string> ListTriggers(string resourceId, string workflowName);

        public Task<string> ListActions(string resourceId, string workflowName);

        Task<IReadOnlyList<string>> GetMissingDiagnosticSettingsAsync(string logicAppResourceId);

        public Task<bool> IsEasyAuthEnabledAsync(string resourceId);

        public Task<bool> IsApplicationInsightsConfiguredAsync(string resourceId);

        public Task<bool> IsExtensionBundleVersionPinnedAsync(string resourceId);

        public Task<IReadOnlyList<Workflow>> ListHttpRequestTriggerWorkflowsAsync(string logicAppResourceId);

        //public Task<IDictionary<string, string>> GetConnectionReferences(string resourceId);
    }
}
