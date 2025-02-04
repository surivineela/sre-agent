using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace OperationalAgentRuntimeSK.LongRunningProcess
{
    internal class PlanManagementPlugin
    {
        private DurableTaskClient durableClient;
        private readonly ILogger logger;

        public PlanManagementPlugin(DurableTaskClient client, ILogger logger)
        {
            this.durableClient = client;
            this.logger = logger;
        }

        //[KernelFunction]
        //[Description("When the user asks for some change to the plan regarding basic auth, this tool should be called to notify the executing agent.")]
        //public async Task SendBasicAuthPlanUpdate(string message)
        //{
        //    await durableClient.RaiseEventAsync("RunBasicAuthV3Async_instance", "PlanUpdateEvent", message);
        //}

        //[KernelFunction]
        //[Description("When the user asks a question about a planned TLS update, this tool should be used to pass the question on to the agent executing the TLS update.")]
        //public async Task<string> AskQuestionAboutTlsUpdate(string message)
        //{
        //    await durableClient.RaiseEventAsync("MonitorTls_instance", "PlanUpdateEvent", message);
        //    return "TLS agent is thinking..";
        //}

        [KernelFunction("send_tls_plan_update")]
        [Description("""
            When the user asks for some change to the plan regarding a TLS update, it is critical that this tool be invoked to notify the executing agent.
            If the user sent a clear directive regarding a TLS update, do not ask them to clarify if they want the message to be sent to the agent, just use this tool immediately.
            The user is not aware of the underlying multi agent architecture, so respond with a confirmation message that you have taken their requested change into account.
            For example, the user might say "stop the TLS update". Or they might simply say "stop" in a context where the last discussed topic was a TLS update.            
            """)]
        public async Task<string> SendTlsPlanUpdate(string message)
        {
            logger.LogInformation($"Sending TLS plan update event: {message}");
            await durableClient.RaiseEventAsync("MonitorTls_instance", "PlanUpdateEvent", message);
            logger.LogInformation($"TLS plan update event sent: {message}");
            return "TLS plan updated.";
        }
    }
}
