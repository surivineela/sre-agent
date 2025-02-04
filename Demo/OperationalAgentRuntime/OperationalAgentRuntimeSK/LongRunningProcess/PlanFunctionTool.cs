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

        [Description("Used to indicate when no more agent actions are needed.")]
        public async Task MarkPlanComplete(
            [Description("The message to send to the user, indicating that the plan has been executed, summarizing the actions.")]
            string message)
        {
            // no op - will be handled manually by the orchestrator
        }
    }
}
