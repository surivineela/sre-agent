using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins
{
    public class CurrentStatePluginDefinition
    {
        private readonly ICurrentStatePlugin _currentStatePlugin;

        public CurrentStatePluginDefinition(ICurrentStatePlugin currentStatePlugin)
        {
            _currentStatePlugin = currentStatePlugin;
        }

        [KernelFunction("app_service_current_state")]
        [Description("Returns current state of a specific app service including recent actions and health status")]
        public string GetCurrentAppState(
            [Description("Name of the app service to check")]
            string appName)
        {
            return _currentStatePlugin.GetCurrentAppState(appName);
        }

        [KernelFunction("current_state_bot")]
        [Description("Returns current state of the AI agent including active tasks and monitoring status. If no apps are being monitored, we should ask user to give a subscription to start monitoring")]
        public async Task<string> GetCurrentBotState()
        {
            return await _currentStatePlugin.GetCurrentBotState();
        }
    }
}
