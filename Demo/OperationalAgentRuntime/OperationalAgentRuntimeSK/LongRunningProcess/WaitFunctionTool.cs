using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace OperationalAgentRuntime.Tools
{
    public class WaitFunctionTool
    {
        private readonly TaskOrchestrationContext context;
        private readonly ILogger logger;
        private CancellationTokenSource? cts;

        public WaitFunctionTool(TaskOrchestrationContext context, ILogger logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public WaitFunctionTool(TaskOrchestrationContext context, ILogger logger, CancellationTokenSource cts)
        {
            this.context = context;
            this.logger = logger;
            this.cts = cts;
        }

        [Description("Waits for a specified amount of time")]
        public async Task<string> Wait(
            [Description("The amount of time to wait in seconds")]
            int seconds
        )
        {
            if (context == null)
                throw new Exception("This tool was invoked in the wrong execution context.");

            logger?.LogInformation("Waiting...");
            await context.CreateTimer(TimeSpan.FromSeconds(seconds), cts?.Token ?? CancellationToken.None);
            logger?.LogInformation($"Waited for {seconds} seconds");
            return $"Waited for {seconds} seconds";
        }
    }
}
