using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Skills.DisableBasicAuth
{
    public class BestPracticesScanner
    {
        [Function(nameof(RunBestPracticesScanner))]
        public static async Task RunBestPracticesScanner(
            [OrchestrationTrigger] TaskOrchestrationContext context
            )
        {
            var resourceMemoryEntity = new EntityInstanceId("ResourceMemory", "SREResourceMemory");
            var currentResourceList = await context.Entities.CallEntityAsync<List<AzureSubscription>>(resourceMemoryEntity, "Get");
            List<string> resourceIds = currentResourceList.SelectMany(c => c.Resources).ToList();

            if (resourceIds.Count > 0)
            {
                List<BasicAuthStatus> violations = await context.CallActivityAsync<List<BasicAuthStatus>>(nameof(CheckBasicAuthForResourcesV2), resourceIds);

                if (violations.Count > 0)
                {
                    var options = new SubOrchestrationOptions(new TaskOptions(), "RunBasicAuthV3Async_instance");
                    await context.CallSubOrchestratorAsync(nameof(BasicAuthV3), violations, options);
                }
            }
            
            await context.CreateTimer(TimeSpan.FromSeconds(30), CancellationToken.None);
            context.ContinueAsNew(null);
        }

        [Function(nameof(CheckBasicAuthForResourcesV2))]
        public static async Task<List<BasicAuthStatus>> CheckBasicAuthForResourcesV2([ActivityTrigger] List<string> resourceIds, FunctionContext executionContext)
        {
            var results = await ArmHelper.CheckBasicAuth(resourceIds);
            var appsInViolation = results.Where(p => p.FtpBasicAuthAllowed || p.ScmBasicAuthAllowed).ToList();

            return appsInViolation;
        }
    }
}
