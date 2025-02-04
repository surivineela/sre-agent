using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using OperationalAgentRuntime.Skills;

namespace OperationalAgentRuntime.Tools
{
    public class OrchestrationFunctionTool
    {
        private readonly TaskOrchestrationContext context;

        public OrchestrationFunctionTool(TaskOrchestrationContext context)
        {
            this.context = context;
        }

        [Description("Add subscriptions to the agent for monitoring")]
        public async Task AddSubscriptionsToAgentAsync(
            [Description("The user message containing the details of what should be monitored")]
            string userMessage)
        {
            await context.CallSubOrchestratorAsync(nameof(AddResourcesToAgent.AddSubscriptionsToAgent), userMessage);
        }
    }
}
