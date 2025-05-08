using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent.SubAgentPlugins
{
    public class FunctionAppDiagnosticsPlugin : IMetaAgentFunctionAppDiagnosticsPlugin
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly FunctionAppDiagnosticsAgentFactory _functionAppDiagnosticsAgentFactory;

        public Guid? ThreadId { get; set; }

        public FunctionAppDiagnosticsPlugin(
            DurableTaskClient durableTaskClient,
            FunctionAppDiagnosticsAgentFactory functionAppDiagnosticAgentFactory)
        {
            _durableTaskClient = durableTaskClient;
            _functionAppDiagnosticsAgentFactory = functionAppDiagnosticAgentFactory;
        }

        [KernelFunction("diagnose_function_app_issues")]
        [Description("Start the workflow to diagnose issues with Azure Function Apps, including execution failures and connectivity problems")]
        public async Task<string> StartFunctionAppDiagnosticsAgent(
            [Description("The resource ID of the Azure Function App to investigate")] string functionAppResourceId)
        {
            if (ThreadId == null)
            {
                throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
            }

            try
            {
                var instanceId = await _functionAppDiagnosticsAgentFactory.StartOrchestration(functionAppResourceId, ThreadId.Value);
                return $"A workflow has been started to diagnose issues with Function App: {instanceId}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                throw new InvalidOperationException("Failed to start the orchestration for Function diagnostic investigation.", ex);
            }
        }
    }
}
