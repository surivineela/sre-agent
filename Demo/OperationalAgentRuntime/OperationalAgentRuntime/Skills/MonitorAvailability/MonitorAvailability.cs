using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
                string availabilityMessage = $"Hi, I have detected that the app service : **{resourceName}** is facing server errors and the availability in last 30 mins is **{Math.Round(sla, 2)}%**.\n\nHang tight!! I am trying to figure out the potential issue and action to recover your application.";

                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(availabilityMessage, availabilityGraph));
                
                ApplensIssueRootCause potentialRootCause = await context.CallActivityAsync<ApplensIssueRootCause>(nameof(BasicSkills.GetProblemRootCause),  new Tuple<string, string>(appResourceId, "The app is facing server errors. check memory"));

                if (potentialRootCause == null) return;

                string rootCauseMessage = $"Potential Issue : {potentialRootCause.RootCauseMessage}";
                string evidenceImage = string.Empty;

                if (!potentialRootCause.RootCauseIntent.Equals("memory", StringComparison.OrdinalIgnoreCase))
                {
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage("Unfortunately, I cannot dertermine the root cause right now. Please investigate the issue using Diagnose and Solve Problems menu item on App services page in azure portal."));
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

                string approvalMessage = string.Empty;
                DataCollection dataCollection = DataCollection.None;
                QuickMitigation quickMitigation = QuickMitigation.Reboot;
                AppPlanSku currentAppSku = null, nextAppSku = null;
                if (potentialRootCause.DataCollection.Equals("memorydump", StringComparison.OrdinalIgnoreCase))
                {
                    dataCollection = DataCollection.MemoryDump;
                    approvalMessage = "I can collect memory dump to further analyze issue.";
                }

                if (potentialRootCause.QuickMitigation.Equals("scaleup", StringComparison.OrdinalIgnoreCase))
                {
                    quickMitigation = QuickMitigation.ScaleUp;
                    approvalMessage = $"{approvalMessage} I can scale up the app service plan to mitigate the issue";
                    currentAppSku = await context.CallActivityAsync<AppPlanSku>(nameof(BasicSkills.GetAppSku), appResourceId);
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Here is your current App service Plan SKU. {HtmlHelpers.GenerateHtmlTableForAppSku(currentAppSku)}"));
                }

                if (approvalMessage == string.Empty)
                {
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage("Unfortunately, I cannot dertermine the root cause right now. Please investigate the issue using Diagnose and Solve Problems menu item on App services page in azure portal."));
                }

                var openAICallMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                    new ChatMessage(ChatRole.User, $"Rephrase this. {approvalMessage}"),
                };

                string openAIResponse = await context.CallActivityAsync<string>(nameof(BasicSkills.GetOpenAIResponse), openAICallMessages);
                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"{openAIResponse}. Would you like me to proceed? <a href='{approvalLink}'>Click here to approve</a>"));

                using (var timeoutCts = new CancellationTokenSource())
                {
                    int approvalTimeoutInSeconds = 3600;
                    DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(approvalTimeoutInSeconds);
                    Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);
                    Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApproveMemoryDumpAndScaleUp");

                    if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
                    {
                        timeoutCts.Cancel();
                        var approvalResult = await approvalEvent;
                        logger.LogInformation($"approvalEvent : {approvalResult}");
                        if (!approvalResult)
                        {
                            logger.LogInformation($"Approval denied");
                            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Denied. I will continue to monitor for any additional issues."));
                            return;
                        }

                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Received. I'll continue with this action in a safe manner and will notify you once I am done."));
                        int waitTimeInSeconds = 30;

                        using (var appActionTimeoutCts = new CancellationTokenSource())
                        {
                            string memoryDumpLink = string.Empty;
                            bool scaleUpSuccess = false;
                            if (dataCollection == DataCollection.MemoryDump)
                            {;
                                await TrackedAgentOperationActionHelper.AddOperation(context, new TrackedAgentOperation()
                                {
                                    Id = Guid.NewGuid(),
                                    OperationName = "CaptureMemoryDump",
                                    Annotations = [ $"Capture a memory dump of the degraded web app" ],
                                    Approver = "",
                                    CreatedTime = DateTime.UtcNow,
                                });
                                memoryDumpLink = await context.CallActivityAsync<string>(nameof(BasicSkills.CaptureMemoryDump), appResourceId);
                            }

                            Task durableWaitTime = context.CreateTimer(TimeSpan.FromSeconds(waitTimeInSeconds), appActionTimeoutCts.Token);
                            await durableWaitTime;

                            if (quickMitigation == QuickMitigation.ScaleUp)
                            {
                                nextAppSku = ArmHelper.GetNextSku(currentAppSku);
                                await TrackedAgentOperationActionHelper.AddOperation(context, new TrackedAgentOperation()
                                {
                                    Id = Guid.NewGuid(),
                                    OperationName = "ScaleUpAppServicePlan",
                                    Annotations = [ $"Scale up app service plan of the degraded web app" ],
                                    Approver = "",
                                    CreatedTime = DateTime.UtcNow,
                                });
                                scaleUpSuccess = await context.CallActivityAsync<bool>(nameof(BasicSkills.ScaleUpAppServicePlan), new Tuple<string, AppPlanSku>(appResourceId, nextAppSku));
                            }

                            if (!string.IsNullOrWhiteSpace(memoryDumpLink))
                            {
                                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"I have captured a memory dump of the impacted application for your further investigation. <a href='{memoryDumpLink}'>Click here to download</a>"));
                            }
                            else
                            {
                                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Unfortunately, I was not able to capture a memory dump."));
                            }

                            if (scaleUpSuccess)
                            {
                                await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Also, I have scaled up the application's app service plan. Here is the new sku : {HtmlHelpers.GenerateHtmlTableForAppSku(nextAppSku)}"));
                            }

                            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"I will continue to monitor the Application availability and notify when the issue is mitigated"));
                            await context.CreateTimer(TimeSpan.FromSeconds(600), appActionTimeoutCts.Token);

                            var updatedAvailabilityTimeSeries = await context.CallActivityAsync<List<TimeSeriesData>>(nameof(BasicSkills.GetAppAvailability), appResourceId);
                            var updatedChartImageInput = new ChartImageInput()
                            {
                                TimeSeries = updatedAvailabilityTimeSeries,
                                Title = "Availability",
                                YAxisLabel = "Percent",
                                YAxisMin = 0.0,
                                YAxisMax = 105.0
                            };
                            var updatedAvailabilityGraph = await context.CallActivityAsync<string>(nameof(BasicSkills.GetChartImageForTimeSeries), updatedChartImageInput);

                            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Good news: The application's availability appears to have recovered. I will continue to monitor and report any further issues. ", updatedAvailabilityGraph));
                        }
                    }
                    else
                    {
                        logger.LogInformation($"No approval received within {approvalTimeoutInSeconds} seconds.");
                    }
                }
            }
        }

        [Function("MonitorAvailability_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("MonitorAvailability_HttpStart");
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
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
