using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;

namespace OperationalAgentRuntime.Tools
{
    internal class PlanFunctionTool
    {
        private DurableTaskClient durableClient;

        public PlanFunctionTool(DurableTaskClient client)
        {
            this.durableClient = client;
        }

        [Description("When the user asks for some change to the plan, this tool should be called to notify the executing agent.")]
        public async Task SendPlanUpdate(string message)
        {
            //await durableClient.Entities.SignalEntityAsync(new EntityInstanceId("ChatHistoryEntity", "BasicAuth"), "appenduser", message);
            await durableClient.RaiseEventAsync("RunBasicAuthV3Async_instance", "PlanUpdateEvent", message);
        }

        [Description("Used to indicate when no more agent actions are needed.")]
        public async Task MarkPlanComplete(
            [Description("The message to send to the user, indicating that the plan has been executed, summarizing the actions.")]
            string message)
        {

        }
    }
}
