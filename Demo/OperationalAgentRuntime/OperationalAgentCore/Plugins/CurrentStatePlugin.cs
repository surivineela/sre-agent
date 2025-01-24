using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime.Cli.DemoExec.Helpers;
using OperationalAgentRuntime.Cli.DemoExec.Models;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;
using System.ComponentModel;

namespace OperationalAgentRuntime.Cli
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
        public async Task<string> GetCurrentAppState(
            [Description("Name of the app service to check")]
        string appName)
        {
            var actions = TrackedActionHelper.GetActions(resourceId: appName);
            if (!actions.Any())
                return $"No tracked actions found for {appName}";

            var latestAction = actions.OrderByDescending(a => a.Timestamp).First();
            var recentDiagnostics = latestAction.DiagnosticEvents
                .OrderByDescending(d => d.Timestamp)
                .Take(3);

            return $"App: {appName}\n" +
                   $"Latest Action: {latestAction.Type} ({latestAction.Status})\n" +
                   $"Description: {latestAction.Description}\n" +
                   "Recent Events: " + string.Join(", ", recentDiagnostics.Select(d => d.Message));
        }

        [KernelFunction("current_state_bot")]
        [Description("Returns current state of the AI agent including active tasks and monitoring status")]
        public async Task<string> GetCurrentBotState()
        {
            var actions = TrackedActionHelper.GetActions();
            var activeActions = actions.Where(a => a.Status == ActionStatus.InProgress);
            var pendingRemediations = await _taskClient.GetPendingRemediationsAsync();

            return $"Active Monitoring: {activeActions.Count()} tasks\n" +
                   $"Pending Remediations: {pendingRemediations.Count}\n" +
                   $"Last Action: {actions.OrderByDescending(a => a.Timestamp).FirstOrDefault()?.Description ?? "None"}";
        }
    }
}