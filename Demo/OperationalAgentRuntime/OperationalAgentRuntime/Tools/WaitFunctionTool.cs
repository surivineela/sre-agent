using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.SemanticKernel;

namespace OperationalAgentRuntime.Tools
{
    public class WaitFunctionTool
    {
        private readonly TaskOrchestrationContext context;

        public WaitFunctionTool(TaskOrchestrationContext context)
        {
            this.context = context;
        }

        [KernelFunction, Description("Waits for a specified amount of time")]
        public async Task<string> Wait(
            [Description("The amount of time to wait in seconds")]
            int seconds
        )
        {
            Console.WriteLine("Waiting...");
            await context.CreateTimer(TimeSpan.FromSeconds(seconds), CancellationToken.None);
            Console.WriteLine($"Waited for {seconds} seconds");
            return $"Waited for {seconds} seconds";
        }
    }
}
