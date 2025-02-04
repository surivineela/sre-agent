using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace OperationalAgentCore
{
    public class CurrentStatePlugin
    {
        private readonly ITaskClient _taskClient;
        private readonly ILogger<CurrentStatePlugin> _logger;

        public CurrentStatePlugin(ITaskClient taskClient, ILogger<CurrentStatePlugin> logger)
        {
            _taskClient = taskClient;
            _logger = logger;
        }

        [KernelFunction("app_service_current_state")]
        [Description("Returns current state of a specific app service including recent actions and health status")]
        public string GetCurrentAppState(
            [Description("Name of the app service to check")]
            string appName)
        {
            // First check tracked app state
            var trackedActions = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                .Where(a => a.Metadata["name"] == appName)
                .OrderByDescending(a => a.Timestamp)
                .ToList();

            var appStateInfo = trackedActions.FirstOrDefault();
            
            if (appStateInfo == null)
                return $"No tracked state found for app service {appName}";

            var stateDescription = $"App: {appName}\n" +
                                 $"Current State: {appStateInfo.Metadata["state"]}\n" +
                                 $"Location: {appStateInfo.Metadata["location"]}\n" +
                                 $"SKU: {appStateInfo.Metadata["sku"]}\n" +
                                 $"Kind: {appStateInfo.Metadata["kind"]}";

            // Also get remediation actions
            var remediationActions = TrackedActionHelper.GetActions(resourceId: appStateInfo.ResourceId)
                .Where(a => !a.Type.Equals(ActionType.AppStateTracking))
                .OrderByDescending(a => a.Timestamp)
                .Take(3);

            if (remediationActions.Any())
            {
                stateDescription += "\n\nRecent Actions:";
                foreach (var action in remediationActions)
                {
                    stateDescription += $"\n- {action.Type}: {action.Description} ({action.Status})";
                    if (action.DiagnosticEvents.Any())
                    {
                        var latestEvent = action.DiagnosticEvents.OrderByDescending(e => e.Timestamp).First();
                        stateDescription += $"\n  Latest Update: {latestEvent.Message}";
                    }
                }
            }

            return stateDescription;
        }

        [KernelFunction("current_state_bot")]
        [Description("Returns current state of the AI agent including active tasks and monitoring status. If no apps are being monitored, we should ask user to give a subscription to start monitoring")]
        public async Task<string> GetCurrentBotState()
        {
            var allActions = TrackedActionHelper.GetActions();
            var activeActions = allActions.Where(a => a.Status == ActionStatus.InProgress);
            var pendingRemediations = await _taskClient.GetPendingRemediationsAsync();

            var monitoredApps = allActions
                .Where(a => a.Type.Equals(ActionType.AppStateTracking))
                .OrderByDescending(a => a.Timestamp)
                .DistinctBy(a => a.Metadata["name"])
                .ToList();

            return $"Active Monitoring: {activeActions.Count()} tasks\n" +
                   $"Pending Remediations: {pendingRemediations.Count}\n" +
                   $"Monitored Apps: {monitoredApps.Count}\n" +
                   $"Last Action: {allActions.OrderByDescending(a => a.Timestamp).FirstOrDefault()?.Description ?? "None"}";
        }
    }
}
