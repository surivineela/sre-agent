using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Skills
{
    public class ApprovalEventPayload
    {
        public bool ApprovalAction { get; set; }
        public string DecisionMakerName { get; set; }
    }

    public static class CheckAndDisableBasicAuth
    {
        [Function(nameof(CheckAndDisableBasicAuth))]
        public static async Task RunOrchestrator(
            [DurableClient] DurableTaskClient durableClient,
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(CheckAndDisableBasicAuth));

            var resourceMemoryEntity = new EntityInstanceId("ResourceMemory", "SREResourceMemory");
            var currentResourceList = await context.Entities.CallEntityAsync<List<AzureSubscription>>(resourceMemoryEntity, "Get");
            List<string> resourceIds = currentResourceList.SelectMany(c => c.Resources).ToList();

            var basicAuthChecks = await context.CallActivityAsync<List<BasicAuthStatus>>(nameof(BasicSkills.CheckBasicAuthForResources), resourceIds);

            //await TrackedActionHelper.TrackAsAssistant(durableClient, "I checked the following resources and here is what I found:\r\n" + JsonSerializer.Serialize(basicAuthChecks));
    
            var appsInViolation = basicAuthChecks.Where(p => p.FtpBasicAuthAllowed || p.ScmBasicAuthAllowed).ToList();
            
            if(!appsInViolation.Any()) return;

            var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                    new ChatMessage(ChatRole.User, "Write a user friendly two line message to tell user that you found a list of App services which have basic auth enabled and its not recommended for secure apps. Its fine to say Hi but Do not write Thanks or Best regards. Also dont write feel free to reach out. Also say I not we."),
                };

            string openAIResponse = await context.CallActivityAsync<string>(nameof(BasicSkills.GetOpenAIResponse), messages);
            string htmlTable = HtmlHelpers.GenerateHtmlTableForBasicAuth(appsInViolation);
            string approvalLink = string.Format(Environment.GetEnvironmentVariable("ApprovalUrl"), context.InstanceId);

            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"{openAIResponse} {htmlTable}"));
            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"I can disable basic authentication for these applications individually. Would you like me to proceed? <a href='{approvalLink}'>Click here to approve</a>"));

            using (var timeoutCts = new CancellationTokenSource())
            {
                int approvalTimeoutInSeconds = 3600;
                DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(approvalTimeoutInSeconds);
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);
                Task<ApprovalEventPayload> approvalEventTask = context.WaitForExternalEvent<ApprovalEventPayload>("DisableBasicAuthApprovalEvent");

                if (approvalEventTask == await Task.WhenAny(approvalEventTask, durableTimeout))
                {
                    timeoutCts.Cancel();
                    var approvalEvent = await approvalEventTask;

                    bool approvalResult = approvalEvent.ApprovalAction;
                    string decisionMaker = approvalEvent.DecisionMakerName;

                    logger.LogInformation($"approvalEvent : {approvalResult}");
                    if (approvalResult)
                    {
                        var currentOperation = new TrackedAgentOperation()
                        {
                            Id = context.NewGuid(),
                            OperationName = "DisablingBasicAuth",
                            Annotations = [ $"Triggered by approval link", $"Apps tracked for disablement: {string.Join(",", appsInViolation.Select(x => x.Name))}" ],
                            Approver = approvalEvent.DecisionMakerName,
                            CreatedTime = DateTime.UtcNow,
                        };
                        await TrackedAgentOperationActionHelper.AddOperation(context, currentOperation);

                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Received by {decisionMaker}. I'll continue to disable basic authentication for these applications in a safe manner and will notify you once I am done."));
                        int waitTimeInSeconds = 30;

                        using (var appActionTimeoutCts = new CancellationTokenSource())
                        {
                            foreach (var app in appsInViolation)
                            {
                                Task durableWaitTimeBetweenApps = context.CreateTimer(TimeSpan.FromSeconds(waitTimeInSeconds), appActionTimeoutCts.Token);
                                bool result = await context.CallActivityAsync<bool>(nameof(BasicSkills.DisableBasicAuth), app);
                                logger.LogInformation($"App: {app.Name}, Basic Auth Disablement Result : {result}");
                                if (result)
                                {
                                    await TrackedAgentOperationActionHelper.AppendAnnotation(context, currentOperation, $"Disabled basic auth for app {app.Name}");
                                }
                                await durableWaitTimeBetweenApps;
                            }
                        }

                        var appRechecks = await context.CallActivityAsync<List<BasicAuthStatus>>(nameof(BasicSkills.CheckBasicAuthForResources), appsInViolation.Select(p => p.ResourceId).ToList());
                        string finalTable = HtmlHelpers.GenerateHtmlTableForBasicAuth(appRechecks);
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"I have successfully disabled basic authentication on the apps. Here is the latest status update. I will continue to monitor for any additional issues and provide further reports. {finalTable}"));
                    }
                    else
                    {
                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Denied by {decisionMaker}. I will continue to monitor for any additional issues."));
                    }
                }
                else
                {
                    logger.LogInformation($"No approval received within {approvalTimeoutInSeconds} seconds.");
                }
            }
        }

        [Function("CheckAndDisableBasicAuth_TimerTrigger")]
        public static async Task TimerStart(
            [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckAndDisableBasicAuth_HttpStart");

            string instanceId = "CheckAndDisableBasicAuth_instance";

            var existingInstance = await client.GetInstanceAsync(instanceId);
            if (existingInstance == null || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
            {
                StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId);

                instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(CheckAndDisableBasicAuth), options);
                logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            }
            else
            {
                logger.LogInformation($"Orchestration with ID = '{instanceId}' is already running.");
            }

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration

        }
    }
}
