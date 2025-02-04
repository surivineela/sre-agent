using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OperationalAgentCore;
using OperationalAgentCore.Models;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Skills.DisableBasicAuth;
using OperationalAgentRuntime.Tools;
using static OperationalAgentRuntime.Skills.DisableBasicAuth.BasicAuthV3;

namespace OperationalAgentRuntimeSK.LongRunningProcess
{
    public class MonitorTls
    {
        private readonly IChatClient chatClient;
        private readonly TeamsConnector teamsConnector;
        private readonly AzureSettings _azureSettings;
        private EntityInstanceId historyEntity = new EntityInstanceId("ChatHistoryEntity", "MonitorTls");

        private const string PlanUpdateEventName = "PlanUpdateEvent";

        private string bicepExample = """
            ### Best Practices - update your deployment pipeline ###
            If you have an automated deployment pipeline (Azure DevOps, GitHub Actions, etc), you should consider updating your deployment scripts to include the minimum TLS setting. 
                                
            For example in a Bicep template, you can set the minimum TLS version with a siteConfig property:
            ```bicep
            resource appService 'Microsoft.Web/sites@2021-02-01' = {
                name: 'myAppService'
                location: 'West US'
                properties: {
                    siteConfig: {
                        minTlsVersion: '1.2'
                    }
                }
            }

            ```

            ### Best Practices - Azure Policy ###
            Consider using Azure Policy to enforce a minimum TLS version across your Azure subscriptions.
            For a complete list of supported policy definitions, see:
            https://learn.microsoft.com/en-us/azure/app-service/policy-reference
            """;

        public MonitorTls(IChatClient chatClient, TeamsConnector teamsConnector, IConfiguration configuration)
        {
            this.chatClient = chatClient;
            this.teamsConnector = teamsConnector;
            _azureSettings = configuration.GetSection("Azure").Get<AzureSettings>();
        }

        public class MonitorTlsInput
        {
            public string DesiredVersion { get; set; }
            
            public List<TlsStatus> AppsInViolation { get; set; }
        }

        [Function(nameof(MonitorTls))]
        public async Task RunMonitorTlsAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context,
            MonitorTlsInput input)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(MonitorTls));

            await context.CallActivityAsync<string>(nameof(MakeTlsPlanAsync), input);

            string htmlTable = HtmlHelpers.GenerateHtmlTableForTls(input.AppsInViolation, new Version(input.DesiredVersion));
            //string introMessage = await context.CallActivityAsync<string>(nameof(GenerateTlsIntroMessage));
            string introMessage = """
                Hi there! I found Web Apps / Function Apps that are allowing TLS connections below the recommended minimum version. For more information on Microsoft's cryptographic recommendations see:
                https://learn.microsoft.com/en-us/security/engineering/cryptographic-recommendations#tlsssl-versions
                """;

            await context.CallActivityAsync<bool>(nameof(PostTlsMessageToTeams), new TeamsMessage($"{introMessage} {htmlTable}"));

            await context.CallActivityAsync(nameof(StartTrackingTlsOperation), input.AppsInViolation);

            var planUpdateTask = context.WaitForExternalEvent<string>(PlanUpdateEventName);

            var approvalOptions = new SubOrchestrationOptions(new TaskOptions(), "ApproveTLSUpdate_instance");
            var approvalResult = await context.CallSubOrchestratorAsync<ApprovalResult>(nameof(GetTLSApprovalAsync), approvalOptions);
            if (approvalResult.Approved == false)
            {
                logger.LogWarning("Operation was not approved, aborting.");
                return;
            }

            if (await ApplyAnyPlanUpdate(planUpdateTask, context, logger))
            {
                planUpdateTask = context.WaitForExternalEvent<string>(PlanUpdateEventName);
            }

            var historyRaw = await context.Entities.CallEntityAsync<string>(historyEntity, "get");
            var history = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(historyRaw);
            await context.CallActivityAsync(nameof(SendSummaryAndStart), new NextActionInput { ChatMessages = history });
            await context.CallActivityAsync(nameof(TrackTlsStatusUpdate), new TlsTrackedActionUpdate { Apps = input.AppsInViolation, ActionStatus = ActionStatus.InProgress });
            
            int stepCounter = 0;
            int attempts = 0;
            while (attempts < 5)
            {
                stepCounter++;

                if (await ApplyAnyPlanUpdate(planUpdateTask, context, logger))
                {
                    planUpdateTask = context.WaitForExternalEvent<string>(PlanUpdateEventName);
                }

                historyRaw = await context.Entities.CallEntityAsync<string>(historyEntity, "get");
                history = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(historyRaw);

                try
                {
                    var nextAction = await context.CallActivityAsync<NextAction>(nameof(ExecuteNextActionTLSAsync), new NextActionInput { OperationId = approvalResult.OperationId, ChatMessages = history, StepCounter = stepCounter });
                    if (!string.IsNullOrEmpty(nextAction.FunctionCallContentRaw))
                    {
                        var functionCallContent = System.Text.Json.JsonSerializer.Deserialize<FunctionCallContent[]>(nextAction.FunctionCallContentRaw);
                        var call = functionCallContent.Single();

                        if (call.Name == "Wait")
                        {
                            using var cts = new CancellationTokenSource();
                            var waitTool = new WaitFunctionTool(context, logger, cts);
                            var waitTask = waitTool.Wait(int.Parse(call.Arguments["seconds"].ToString()));
                            var completedTask = await Task.WhenAny(waitTask, planUpdateTask);

                            if (completedTask == planUpdateTask)
                            {
                                if (await ApplyAnyPlanUpdate(planUpdateTask, context, logger))
                                {
                                    planUpdateTask = context.WaitForExternalEvent<string>(PlanUpdateEventName);
                                }

                                cts.Cancel();
                            }
                        }
                        else if (call.Name == "MarkPlanComplete")
                        {
                            if (call.Arguments.TryGetValue("message", out object? msg))
                            {
                                if (!string.IsNullOrEmpty(msg?.ToString()))
                                {
                                    await context.CallActivityAsync(nameof(PostTlsMessageToTeams), new TeamsMessage(call.Arguments["message"].ToString()));
                                }
                            }

                            await context.CallActivityAsync(nameof(PostTlsMessageToTeams), new TeamsMessage(bicepExample));

                            // agent is done
                            return;
                        }
                        else
                        {
                            //todo, allow running orchestrations
                            logger.LogWarning("How did we get here.. there are no other orchestration level function calls yet");
                        }
                    }
                    else if (nextAction.ChatMessage != null)
                    {
                        await context.CallActivityAsync(nameof(PostTlsMessageToTeams), new TeamsMessage(nextAction.ChatMessage.Text));
                    }
                    else
                    {
                        logger.LogWarning("No next action or chat message returned... there must be some edge case we're not handling.");
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error executing next action. Retrying...");
                    attempts++;
                }
            }
        }

        private async Task<bool> ApplyAnyPlanUpdate(Task<string> planUpdateTask, TaskOrchestrationContext context, ILogger logger)
        {
            if (planUpdateTask.IsCompleted)
            {
                var planUpdate = await planUpdateTask;
                logger.LogInformation($"TLS plan update was recieved: {planUpdate}");
                await context.Entities.CallEntityAsync(historyEntity, "appendUser", planUpdate);
                return true;
            }

            return false;
        }

        [Function(nameof(MakeTlsPlanAsync))]
        public async Task MakeTlsPlanAsync(
            [ActivityTrigger] MonitorTlsInput input,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(MakeTlsPlanAsync));

            List<ChatMessage> chatMessages =
            [
                new ChatMessage(ChatRole.System, $"""
                You are an operations agent that writes and executes detailed plans based on a specified goal. Use "I" not "we".
                The user will provide the resources that your plan should operate on.
                Your first response must be JUST the plan, do NOT include anything else such as 'here is the updated plan' or other commentary.
                After providing the initial plan, the user will prompt you to move to execution mode, at which point you can freely use tools and send the user messages.

                ** Plan Outline **
                The initial plan you create should be to update each of the apps to require the minimum TLS version {input.DesiredVersion} one by one with 30 seconds of delay in between. 
                Write the plan to use the provided tools, being explicit about the tool that should be used for reach relevant step.                
                The plan should include sending the user status updates as you proceed. 
                During the delay between each app, you should poll the traffic volume of the app every 10 seconds for anomalies such as a massive drop in traffic due to clients failing to connect.
                If the user requests any updates to the plan, respond with a brief summary of the new plan, noting the relevant changes.
                As your final action, mark the plan as complete(even if it was abandoned), including a summary of what happened.

                ** Anomaly Detection **
                When you evaluate for an anomaly, consider the point in time the TLS version was updated - you are looking for a difference between the before and after. 
                Its possible the resource was not getting any traffic before the update, in which case there is no anomaly to detect.
                If you detect an anomaly, you should notify the user and let them know you are going to perform a rollback, then perform the rollback by updating the minimum TLS version to the previous one.
                Once the rollback is complete, you can proceed with the next app in the plan. A single rollback is not sufficient to give up on the plan.
                However if multiple rollbacks are required, abandon the remainder of the plan.                

                ** Summary Guidelines **
                Your final summary should be clearly formatted so its easy to understand what happened for each app and use emojis to make it easier to scan.

                **Sending Status Updates**
                You are encouraged to start these status updates with a single appropriate emoji, some examples: ▶️ for starting, ✅ for success, ⚠️ for a problem, 🔄 for a rollback. 
                These updates should summarize the current progress and include relevant details such as resource names.
                For any update where a change was made, it is important to include a UTC timestamp e.g. `2025-01-30T01:45:13Z`.

                ### Formatting Guidelines
                Your messages will be sent via Microsoft Teams, without using adaptive cards. 
                Note that the below guidelines use backticks to be clear about the referenced text. The only scenario you should put backticks in your response is for the code block case outlined below.
                Follow these guidelines:                

                - Allowed Markdown Syntax:
                  1. Bold: **bold text**  
                     - Use `**` around the text for bold (example: **This is bold**).
                     - In these guidelines, backticks around `**bold text**` are just for illustration; do not include backticks in your final output when generating bold text.
                  2. Italics: *italic text* or _italic text_  
                     - Use `*` or `_` around the text for italics (example: *This is italics* or _This is italics_).
                     - In these guidelines, backticks around `*italic text*` or `_italic text_` are for illustration only.
                  3. Underline: __underlined text__  
                  4. Strikethrough: ~~strikethrough text~~  
                  5. Headings:
                     - # Heading 1
                     - ## Heading 2
                     - ### Heading 3
                     (Note: Teams applies limited styling to headings.)
                  6. Bulleted Lists:  
                     - Use `- ` or `* ` at the start of each line (example: `- Item 1`).
                  7. Numbered Lists:  
                     - Use `1. `, `2. `, etc. (example: `1. First`, `2. Second`).
                  8. Blockquotes:  
                     - Begin a line with `> ` for quoted text.
                  9. Code Blocks:
                     - Use triple backticks to start and end the block (example below).  
                       ```
                       Your code here
                       ```                     

                - Disallowed or Unreliable Markdown:
                  1. Markdown Tables: `| Column | Column |`
                  2. Checklists: `- [ ] item`
                  3. HTML Tags: `<b>some text</b>`, `<br/>`, etc.
                  4. Images: `![alt text](imageURL)`
                  5. Any advanced GitHub-Flavored Markdown extensions (e.g., collapsible sections, footnotes, auto-linking).

                - Additional Requirements:
                  1. No HTML, no JSON, and no Adaptive Cards in the output—Markdown text only.
                
                """),
                new ChatMessage(ChatRole.User, $"""
                Here are the apps that need updating:
                {string.Join(Environment.NewLine, input.AppsInViolation.Select(x => $"{x.ResourceId} has a current minimum TLS version of {x.MinimumTlsVersion}"))}
                """)
            ];

            var armTool = new ArmFunctionTool(logger);
            var metricsPlugin = new MetricsPlugin();
            var waitTool = new WaitFunctionTool(null, logger);
            var tools = new List<AIFunction>
            {
                AIFunctionFactory.Create(armTool.SetMinimumTlsVersion),
                AIFunctionFactory.Create(metricsPlugin.GetSuccessfulRequestVolumeAsync),
                AIFunctionFactory.Create(waitTool.Wait),
            };

            // It might be better if we can build the metadata for the tools but pass it into the prompt, instead of as first class tools, since we dont want actually want a tool call here, just a detailed plan.
            var chatOptions = new ChatOptions { Tools = new List<AITool>(tools), ToolMode = ChatToolMode.Auto};

            var response = await chatClient.CompleteAsync(chatMessages, chatOptions);

            chatMessages.Add(response.Message);
            logger.LogInformation(response.Message.Text);

            // work around some issue with chat message serialization, do it manually
            await client.Entities.SignalEntityAsync(historyEntity, "set", System.Text.Json.JsonSerializer.Serialize(chatMessages));
        }

        [Function(nameof(SendSummaryAndStart))]
        public async Task SendSummaryAndStart(
            [ActivityTrigger] NextActionInput input,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var chatMessages = input.ChatMessages;

            chatMessages.Add(new ChatMessage(ChatRole.User, """
                Now that the plan is complete, can you send me a 3 sentence summary of the steps you'll take?
                """
            ));

            var response = await chatClient.CompleteAsync(chatMessages);
            chatMessages.Add(response.Message);

            if (!string.IsNullOrEmpty(response.Message.Text))
            {
                await PostTlsMessageToTeams(new TeamsMessage(response.Message.Text), client, executionContext);
            }

            chatMessages.Add(new ChatMessage(ChatRole.User, "Great, you can start executing the plan now."));


            await client.Entities.SignalEntityAsync(historyEntity, "set", System.Text.Json.JsonSerializer.Serialize(chatMessages));
        }


        [Function(nameof(GenerateTlsIntroMessage))]
        public async Task<string> GenerateTlsIntroMessage(
            [ActivityTrigger] string desiredVersion,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var introMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                    new ChatMessage(ChatRole.User, $"Write a user friendly two line message to tell user that you found a list of App services which accept TLS connections that are lower that the recommended minimum version of {desiredVersion}. Its fine to say Hi but Do not write Thanks or Best regards. Also dont write feel free to reach out. Also say I not we."),
            };

            var res = await chatClient.CompleteAsync(introMessages);
            return res.Message.Text;
        }

        [Function(nameof(PostTlsMessageToTeams))]
        public async Task<bool> PostTlsMessageToTeams(
            [ActivityTrigger] TeamsMessage teamsMessage, 
            [DurableClient] DurableTaskClient client, 
            FunctionContext executionContext)
        {
            

            await ChatHistoryPersistency.ChatHistoryTransition(history =>
            {
                history.AddAssistantMessage(teamsMessage.Content);
                return Task.FromResult(0);
            });

            bool result = await this.teamsConnector.PostMessageAsync(teamsMessage);

            //TODO : Need to figure out a better way to preserve multiple message orderings. Putting an artificial delay for now.
            await Task.Delay(1000);
            return result;
        }


        public class TlsTrackedActionUpdate
        {
            public List<TlsStatus> Apps { get; set; }
            public ActionStatus ActionStatus { get; set; }
        }

        [Function(nameof(StartTrackingTlsOperation))]
        public async Task StartTrackingTlsOperation(
            [ActivityTrigger] List<TlsStatus> appsInViolation)
        {            
            foreach (var app in appsInViolation)
            {                                
                TrackedActionHelper.TrackAction(
                    "TlsWorker",
                    app.ResourceId,
                    ActionType.AppStateTracking,
                    "Updating minimum TLS version",
                    new Dictionary<string, string> { { "name", app.Name }, { "currentTls", app.MinimumTlsVersion } }
                );

                TrackedActionHelper.UpdateActionStatus(app.ResourceId, ActionStatus.RequiresApproval);
            }
        }

        [Function(nameof(TrackTlsStatusUpdate))]
        public async Task TrackTlsStatusUpdate(
            [ActivityTrigger] TlsTrackedActionUpdate update)
        {
            foreach (var app in update.Apps)
            {
                TrackedActionHelper.UpdateActionStatus(app.ResourceId, update.ActionStatus);
            }
        }

        [Function(nameof(GetTLSApprovalAsync))]
        public async Task<ApprovalResult> GetTLSApprovalAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(GetTLSApprovalAsync));

            var approvalUrl = _azureSettings.ApprovalUrl;
            if (string.IsNullOrEmpty(approvalUrl))
            {
                throw new Exception("Approval URL is not set in the configuration.");
            }

            string approvalLink = string.Format(approvalUrl, context.InstanceId);
            await context.CallActivityAsync<bool>(nameof(PostTlsMessageToTeams), new TeamsMessage($"I can update these applications to require TLS 1.2 one at a time. I'll wait 30 seconds between each app and monitor its health during that time. Would you like me to proceed? <a href='{approvalLink}'>Click here to approve</a>"));

            using (var timeoutCts = new CancellationTokenSource())
            {
                int approvalTimeoutInSeconds = 3600;
                DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(approvalTimeoutInSeconds);
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);
                Task<ApprovalEventPayload> approvalEventTask = context.WaitForExternalEvent<ApprovalEventPayload>("ApproveUpdateMinimumTLSEvent");

                if (approvalEventTask == await Task.WhenAny(approvalEventTask, durableTimeout))
                {
                    timeoutCts.Cancel();
                    var approvalEvent = await approvalEventTask;

                    bool approvalResult = approvalEvent.ApprovalAction;
                    string decisionMaker = approvalEvent.DecisionMakerName;

                    logger.LogInformation($"approvalEvent : {approvalResult}");
                    if (approvalResult)
                    {
                        await context.CallActivityAsync<bool>(nameof(PostTlsMessageToTeams), new TeamsMessage($"Approval by **{decisionMaker}** recieved at {DateTime.UtcNow:o}, proceeding with TLS update. "));
                        return new ApprovalResult { Approved = true, OperationId = context.InstanceId.ToString() };
                    }
                    else
                    {
                        await context.CallActivityAsync<bool>(nameof(PostTlsMessageToTeams), new TeamsMessage($"Approval Denied by {decisionMaker}."));
                    }
                }
                else
                {
                    logger.LogInformation($"No approval received within {approvalTimeoutInSeconds} seconds.");
                }
            }

            return new ApprovalResult { Approved = false };
        }

        [Function(nameof(ExecuteNextActionTLSAsync))]
        public async Task<NextAction> ExecuteNextActionTLSAsync(
           [ActivityTrigger] NextActionInput input,
           [DurableClient] DurableTaskClient client,
           FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(ExecuteNextActionTLSAsync));

            // this loop is really generic - it should work for all sorts of flows, just need to specify the appropriate tools.

            var chatMessages = input.ChatMessages;

            var armTool = new ArmFunctionTool(logger);
            var waitTool = new WaitFunctionTool(null, logger);
            var metricsPlugin = new MetricsPlugin();
            var planTool = new PlanFunctionTool(client);
            var statusTool = new StatusUpdateFunctionTool(teamsConnector);
            var tools = new List<AIFunction>
            {
                //AIFunctionFactory.Create(armTool.GetAllSubscriptions),
                //AIFunctionFactory.Create(armTool.CheckBasicAuth),
                AIFunctionFactory.Create(armTool.SetMinimumTlsVersion),
                AIFunctionFactory.Create(metricsPlugin.GetSuccessfulRequestVolumeAsync),
                AIFunctionFactory.Create(waitTool.Wait),
                AIFunctionFactory.Create(planTool.MarkPlanComplete),
                AIFunctionFactory.Create(statusTool.SendStatusUpdate),
            };

            var chatOptions = new ChatOptions { Tools = new List<AITool>(tools), ToolMode = ChatToolMode.Auto };

            var result = new NextAction();

            // Don't want to allow too many operations within a single activity, better to go back to the orchestration level and checkpoint
            for (int i = 0; i < 5; i++)
            {
                var response = await chatClient.CompleteAsync(chatMessages, chatOptions);
                chatMessages.Add(response.Message);

                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    FunctionCallContent[] functionCallContents = response.Message.Contents.OfType<FunctionCallContent>().ToArray();

                    // I have no idea why this is necessary. During local testing, there is only one function call, but the deployed version sometimes has multiples. Need to investigate more.
                    List<FunctionCallContent> deferred = new List<FunctionCallContent>();

                    foreach (var call in functionCallContents)
                    {
                        if (call.Name == nameof(waitTool.Wait) || call.Name == nameof(planTool.MarkPlanComplete))
                        {
                            deferred.Add(call);
                            continue;
                        }
                        else
                        {
                            var tool = tools.First(x => x.Metadata.Name == call.Name);

                            try
                            {
                                var invokeResult = await tool.InvokeAsync(call.Arguments);
                                var resultContent = new FunctionResultContent(call.CallId, call.Name, invokeResult);
                                chatMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                            }
                            catch(Exception e)
                            {
                                logger.LogError(e, $"Error invoking tool {call.Name}");

                                // we could pass the error onto the model. In many cases, thats helpful, the model can use the error to figure out what to do next.
                                // But for the demo, I decided to retry without the model knowing, so we try to find the tool call message and remove it
                                // TODO - update this to pass the errors back to the model.
                                
                                var matchingCall = chatMessages.SingleOrDefault(x => x.Contents.OfType<FunctionCallContent>().Any(y => y.CallId == call.CallId));
                                if (matchingCall != null)
                                {
                                    chatMessages.Remove(matchingCall);
                                }
                                else
                                {
                                    var resultContent = new FunctionResultContent(call.CallId, call.Name, e.Message);
                                    chatMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                                }
                            }
                        }
                    }

                    var deferredCall = deferred.FirstOrDefault();
                    if (deferredCall != null)
                    {
                        var toolMsg = deferredCall.Name switch
                        {
                            nameof(waitTool.Wait) => "Wait complete",
                            nameof(planTool.MarkPlanComplete) => "Marked plan as complete",
                            _ => "Unknown tool call"
                        };
                        var resultContent = new FunctionResultContent(deferredCall.CallId, deferredCall.Name, toolMsg);
                        
                        // this is dodgy, we shouldn't be saying the wait happened before it actually happened - it could be interrupted.
                        chatMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));

                        result.ChatMessage = response.Message;
                        result.FunctionCallContentRaw = System.Text.Json.JsonSerializer.Serialize(new FunctionCallContent[] { deferredCall });
                        break;
                    }
                  
                }
                else
                {
                    result.ChatMessage = response.Message;
                    break;
                }
            }

            await client.Entities.SignalEntityAsync(historyEntity, "set", System.Text.Json.JsonSerializer.Serialize(chatMessages));
            return result;
        }



    }
}
