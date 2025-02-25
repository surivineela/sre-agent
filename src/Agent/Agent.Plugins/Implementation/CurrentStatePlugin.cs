
using Agent.Core.Helpers;
using Agent.Core.Models;
using Octokit;

namespace Agent.Plugins.Implementation
{
    public class CurrentStatePlugin : ICurrentStatePlugin
    {
        public string GetCurrentAppState(string appName)
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

        public async Task<string> GetCurrentBotState()
        {
            var allActions = TrackedActionHelper.GetActions();
            var activeActions = allActions.Where(a => a.Status == ActionStatus.InProgress);

            var monitoredApps = allActions
                .Where(a => a.Type.Equals(ActionType.AppStateTracking))
                .OrderByDescending(a => a.Timestamp)
                .DistinctBy(a => a.Metadata["name"])
                .ToList();

            return $"Active Monitoring: {activeActions.Count()} tasks\n" +
                   $"Monitored Apps: {monitoredApps.Count}\n" +
                   $"Last Action: {allActions.OrderByDescending(a => a.Timestamp).FirstOrDefault()?.Description ?? "None"}";
        }
    }
}
