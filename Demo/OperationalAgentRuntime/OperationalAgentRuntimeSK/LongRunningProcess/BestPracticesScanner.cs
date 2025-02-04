using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using OperationalAgentCore;
using OperationalAgentCore.Models;
using OperationalAgentRuntimeSK.LongRunningProcess;



namespace OperationalAgentRuntime.Skills.DisableBasicAuth
{
    public class BestPracticesScanner
    {
        private SubscriptionPlugin subscriptionPlugin;
        private Version desiredVersion = new Version("1.2");

        public BestPracticesScanner(SubscriptionPlugin subscriptionPlugin)
        {
            this.subscriptionPlugin = subscriptionPlugin;
        }


        [Function(nameof(RunBestPracticesScanner))]
        public async Task RunBestPracticesScanner(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            List<string> resourceIds = await context.CallActivityAsync<List<string>>(nameof(GetResourcesForBestPracticesMonitoring));
            if (resourceIds.Count > 0)
            {
                // disabling basic auth checks for now

                //List<BasicAuthStatus> basicAuthViolations = await context.CallActivityAsync<List<BasicAuthStatus>>(nameof(CheckBasicAuthForResourcesV2), resourceIds);

                //if (basicAuthViolations.Count > 0)
                //{
                //    var options = new SubOrchestrationOptions(new TaskOptions(), "RunBasicAuthV3Async_instance");
                //    await context.CallSubOrchestratorAsync(nameof(BasicAuthV3), basicAuthViolations, options);
                //}

                List<TlsStatus> tlsViolations = await context.CallActivityAsync<List<TlsStatus>>(nameof(CheckTlsForResources), resourceIds);

                if (tlsViolations.Count > 0)
                {
                    var options = new SubOrchestrationOptions(new TaskOptions(), "MonitorTls_instance");
                    await context.CallSubOrchestratorAsync(nameof(MonitorTls), new MonitorTls.MonitorTlsInput {  AppsInViolation = tlsViolations, DesiredVersion = desiredVersion.ToString() }, options);
                }
            }
            
            await context.CreateTimer(TimeSpan.FromSeconds(30), CancellationToken.None);
            context.ContinueAsNew(null);
        }

        [Function(nameof(GetResourcesForBestPracticesMonitoring))]
        public async Task<List<string>> GetResourcesForBestPracticesMonitoring(
            [ActivityTrigger] string input,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(GetResourcesForBestPracticesMonitoring));

            logger.LogInformation("Checking for resources to monitor");

            var trackedStates = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                .OrderByDescending(a => a.Timestamp)
                .DistinctBy(a => a.Metadata["name"])
                .ToList();

            logger.LogInformation($"Found {trackedStates.Count} resources to monitor");

            var resourceIds = trackedStates.Select(x => x.ResourceId).ToList();

            return resourceIds;
        }

        [Function(nameof(CheckBasicAuthForResourcesV2))]
        public static async Task<List<BasicAuthStatus>> CheckBasicAuthForResourcesV2([ActivityTrigger] List<string> resourceIds, FunctionContext executionContext)
        {
            var results = await ArmHelper.CheckBasicAuth(resourceIds);
            var appsInViolation = results.Where(p => p.FtpBasicAuthAllowed || p.ScmBasicAuthAllowed).ToList();

            return appsInViolation;
        }

        [Function(nameof(CheckTlsForResources))]
        public async Task<List<TlsStatus>> CheckTlsForResources(
            [ActivityTrigger] List<string> resourceIds,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(CheckTlsForResources));

            var results = await ArmHelper.GetTlsSettings(resourceIds);
            logger.LogInformation(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

            var appsInViolation = results.Where(p => new Version(p.MinimumTlsVersion) < desiredVersion).OrderBy(x => x.Name).ToList();
            logger.LogInformation($"Found {appsInViolation.Count} apps in violation");

            return appsInViolation;
        }
    }
}
