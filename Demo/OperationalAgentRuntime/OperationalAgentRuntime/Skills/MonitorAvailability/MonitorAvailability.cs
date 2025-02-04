using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Skills.MonitorAvailability
{
    public static class MonitorAvailability
    {
        [Function(nameof(MonitorAvailability))]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MonitorAvailability));

            var resourceMemoryEntity = new EntityInstanceId("ResourceMemory", "SREResourceMemory");
            var currentResourceList = await context.Entities.CallEntityAsync<List<AzureSubscription>>(resourceMemoryEntity, "Get");
            List<string> resourceIds = currentResourceList.SelectMany(c => c.Resources).ToList();

            foreach (var appResourceId in resourceIds)
            {
                // TODO: Remove post-demo
                var whitelist = Environment.GetEnvironmentVariable("MonitorAppList");
                if (!string.IsNullOrWhiteSpace(whitelist) && !whitelist.Contains(appResourceId)) continue;

                var availabilityTimeSeries = await context.CallActivityAsync<List<TimeSeriesData>>(nameof(BasicSkills.GetAppAvailability), appResourceId);
                var sla = availabilityTimeSeries.TakeLast(30).Average(ts => ts.Value);

                if (sla > 99.9) continue;

                var chartImageInput = new ChartImageInput()
                {
                    TimeSeries = availabilityTimeSeries,
                    Title = "Availability",
                    YAxisLabel = "Percent",
                    YAxisMin = 0.0,
                    YAxisMax = 105.0
                };

                string resourceName = appResourceId.Split('/').Last();
                string availabilityGraph = await context.CallActivityAsync<string>(nameof(BasicSkills.GetChartImageForTimeSeries), chartImageInput);
                string availabilityMessage = $"Hi, I have detected that the app service : **{resourceName}** is facing server errors and the availability in last 30 mins is **{Math.Round(sla, 2)}%**.\n";
                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(availabilityMessage, availabilityGraph));
                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage("Hang tight!!I am trying to figure out the potential issue and action to recover your application."));
                
                ApplensIssueRootCause potentialRootCause = await context.CallActivityAsync<ApplensIssueRootCause>(nameof(BasicSkills.GetProblemRootCause),  new Tuple<string, string>(appResourceId, "The app is facing server errors. check memory"));

                if (potentialRootCause == null) return;

                string rootCauseMessage = $"Potential Issue : {potentialRootCause.RootCauseMessage}";
                string evidenceImage = string.Empty;

                if (!potentialRootCause.RootCauseIntent.Equals("memory", StringComparison.OrdinalIgnoreCase))
                {
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage("Unfortunately, I cannot dertermine the root cause right now. Please investigate the issue using Diagnose and Solve Problems menu item on App services page in azure portal."));
                    return;
                }

                var privateBytesTimeSeries = await context.CallActivityAsync<List<TimeSeriesData>>(nameof(BasicSkills.GetAppPrivateBytes), appResourceId);
                var privateBytesChartImageInput = new ChartImageInput()
                {
                    TimeSeries = privateBytesTimeSeries,
                    Title = "Private Bytes",
                    YAxisLabel = "GB"
                };

                string privateBytesGraph = await context.CallActivityAsync<string>(nameof(BasicSkills.GetChartImageForTimeSeries), privateBytesChartImageInput);
                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(rootCauseMessage, privateBytesGraph));

                string approvalLink = string.Format(Environment.GetEnvironmentVariable("ApprovalUrl"), context.InstanceId);

                foreach(var mitigation in potentialRootCause.QuickMitigation)
                {
                    string approvalMessage = string.Empty;
                    AppPlanSku currentAppSku = null, nextAppSku = null;
                    bool rebootSuccessful = false, scaleUpSuccessful = false;
                    string memoryDumpLink = string.Empty;

                    if (mitigation == QuickMitigation.Reboot)
                    {
                        approvalMessage = $"I can restart the app service to mitigate the issue.";
                    }
                    else
                    {
                        if (potentialRootCause.DataCollection == DataCollection.MemoryDump)
                        {
                            approvalMessage = "I can collect memory dump to further analyze issue.";
                        }

                        if (mitigation == QuickMitigation.ScaleUp)
                        {
                            approvalMessage = $"{approvalMessage} I can scale up the app service plan to mitigate the issue";
                            currentAppSku = await context.CallActivityAsync<AppPlanSku>(nameof(BasicSkills.GetAppSku), appResourceId);
                            nextAppSku = ArmHelper.GetNextSku(currentAppSku);
                            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Here is your current App service Plan SKU. {HtmlHelpers.GenerateHtmlTableForAppSku(currentAppSku)}"));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(approvalMessage))
                    {
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage("Unfortunately, I cannot dertermine further course of action right now. Please investigate the issue using Diagnose and Solve Problems menu item on App services page in azure portal."));
                        return;
                    }

                    var openAICallMessages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                        new ChatMessage(ChatRole.User, $"Rephrase this. {approvalMessage}"),
                    };

                    string openAIResponse = await context.CallActivityAsync<string>(nameof(BasicSkills.GetOpenAIResponse), openAICallMessages);
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"{openAIResponse}. Would you like me to proceed? <a href='{approvalLink}'>Click here to approve</a>"));

                    var timeoutCts = new CancellationTokenSource();
                    int approvalTimeoutInSeconds = 3600;
                    DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(approvalTimeoutInSeconds);
                    Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);
                    Task<ApprovalEventPayload> approvalEventTask = context.WaitForExternalEvent<ApprovalEventPayload>("ApproveMemoryDumpAndScaleUp");

                    if (approvalEventTask != await Task.WhenAny(approvalEventTask, durableTimeout))
                    {
                        logger.LogInformation($"No approval received within {approvalTimeoutInSeconds} seconds.");
                        return;
                    }

                    timeoutCts.Cancel();
                    var approvalEvent = await approvalEventTask;
                    bool approvalResult = approvalEvent.ApprovalAction;
                    string decisionMaker = approvalEvent.DecisionMakerName;

                    if (!approvalResult)
                    {
                        logger.LogInformation($"Approval denied");
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Denied by {decisionMaker}. I will continue to monitor for any additional issues."));
                        return;
                    }

                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Received from {decisionMaker}. I'll continue with this action in a safe manner and will notify you once I am done."));
                    int waitTimeInSeconds = 180;

                    var mitigationOperationTrackedOperation = new TrackedAgentOperation()
                    {
                        Id = context.NewGuid(),
                        OperationName = mitigation.ToString(),
                        Annotations = [$"Apply {mitigation} mitigation to degraded web app"],
                        Approver = decisionMaker,
                        CreatedTime = context.CurrentUtcDateTime,
                    };

                    await TrackedAgentOperationActionHelper.AddOperation(context, mitigationOperationTrackedOperation);

                    if (mitigation == QuickMitigation.Reboot)
                    {
                        rebootSuccessful = await context.CallActivityAsync<bool>(nameof(BasicSkills.RestartWebApp), appResourceId);
                        string rebootMessage = rebootSuccessful ? "I have successfully restarted the application. I will monitor the availability of the impacted app to confirm mitigation."
                            : "I was not able to restart the application. Let me try to find more mitigations.";

                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"{rebootMessage}"));
                    }
                    else
                    {
                        if (potentialRootCause.DataCollection == DataCollection.MemoryDump)
                        {
                            var memoryDumpTrackedObject = new TrackedAgentOperation()
                            {
                                Id = context.NewGuid(),
                                OperationName = "CaptureMemoryDump",
                                Annotations = [$"Capture a memory dump of the degraded web app"],
                                Approver = decisionMaker,
                                CreatedTime = context.CurrentUtcDateTime,
                            };
                            await TrackedAgentOperationActionHelper.AddOperation(context, memoryDumpTrackedObject);
                            memoryDumpLink = await context.CallActivityAsync<string>(nameof(BasicSkills.CaptureMemoryDump), appResourceId);
                            string memoryDumpOperationMessage = string.IsNullOrWhiteSpace(memoryDumpLink) ? "Operation failed" : "Operation is finished.";
                            await TrackedAgentOperationActionHelper.AppendAnnotation(context, memoryDumpTrackedObject, memoryDumpOperationMessage);
                            await context.CreateTimer(TimeSpan.FromSeconds(30), (new CancellationTokenSource()).Token);
                        }

                        if (mitigation == QuickMitigation.ScaleUp)
                        {
                            scaleUpSuccessful = await context.CallActivityAsync<bool>(nameof(BasicSkills.ScaleUpAppServicePlan), new Tuple<string, AppPlanSku>(appResourceId, nextAppSku));
                            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"I have scaled up the application's app service plan. I will monitor the availability of the impacted app to confirm mitigation. Here is the new app plan sku : {HtmlHelpers.GenerateHtmlTableForAppSku(nextAppSku)}"));
                        }
                    }

                    await TrackedAgentOperationActionHelper.AppendAnnotation(context, mitigationOperationTrackedOperation, "Operation is finished.");

                    Task durableWaitTime = context.CreateTimer(TimeSpan.FromSeconds(waitTimeInSeconds), (new CancellationTokenSource()).Token);
                    await durableWaitTime;

                    var updatedAvailabilityTimeSeries = await context.CallActivityAsync<List<TimeSeriesData>>(nameof(BasicSkills.GetAppAvailability), appResourceId);
                    var updatedSLA = updatedAvailabilityTimeSeries.TakeLast(30).Average(ts => ts.Value);
                    var updatedChartImageInput = new ChartImageInput()
                    {
                        TimeSeries = updatedAvailabilityTimeSeries,
                        Title = "Availability",
                        YAxisLabel = "Percent",
                        YAxisMin = 0.0,
                        YAxisMax = 105.0
                    };
                    var updatedAvailabilityGraph = await context.CallActivityAsync<string>(nameof(BasicSkills.GetChartImageForTimeSeries), updatedChartImageInput);

                    if (updatedSLA >= 99.9)
                    {
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Good news: The application's availability appears to have recovered. I will continue to monitor and report any further issues. ", updatedAvailabilityGraph));
                        return;
                    }

                    string continuationMessage = mitigation != potentialRootCause.QuickMitigation.Last() ? "Let me try to find more mitigations." : "";
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"The application still seems to be facing availability issues. {continuationMessage}", updatedAvailabilityGraph));

                    if (!string.IsNullOrWhiteSpace(memoryDumpLink))
                    {
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"This might be related to application code issue. I have captured a memory dump for your further investigation. <a href='{memoryDumpLink}'>Click here to download</a>"));
                        return;
                    }
                }
            }
        }

        [Function("MonitorAvailability_TimerStart")]
        public static async Task TimerStart(
            [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("MonitorAvailability_TimerStart");
            string instanceId = "MonitorAvailability_instance";

            // Check if an instance with the specified ID is already running  
            var existingInstance = await client.GetInstanceAsync(instanceId);
            if (existingInstance == null || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
            {
                StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId);
                instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(MonitorAvailability), options);
                logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            }
            else
            {
                logger.LogInformation($"Orchestration with ID = '{instanceId}' is already running.");
            }

            // Returns an HTTP 202 response with an instance management payload  
            // return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
