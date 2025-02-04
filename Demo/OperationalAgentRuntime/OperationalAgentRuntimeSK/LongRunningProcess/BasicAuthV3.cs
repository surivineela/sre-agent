using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OperationalAgentCore;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Tools;
using TeamsMessage = OperationalAgentCore.TeamsMessage;

namespace OperationalAgentRuntime.Skills.DisableBasicAuth
{

    /// <summary>
    /// ⚠️⚠️⚠️ WARNING - this code has NOT been updated from the learnings of implementing MonitorTls. It would be better to refactor MonitorTls's common functionality out and then rework this to use it ⚠️⚠️⚠️
    /// </summary>
    public class BasicAuthV3
    {
        private readonly IChatClient chatClient;
        private readonly TeamsConnector teamsConnector;
        private readonly AzureSettings _azureSettings;
        private EntityInstanceId historyEntity = new EntityInstanceId("ChatHistoryEntity", "BasicAuth");

        public BasicAuthV3(IChatClient chatClient, TeamsConnector teamsConnector, IConfiguration configuration)
        {
            this.chatClient = chatClient;
            this.teamsConnector = teamsConnector;
            _azureSettings = configuration.GetSection("Azure").Get<AzureSettings>();
        }

        [Function(nameof(BasicAuthV3))]
        public async Task RunBasicAuthV3Async(
            [OrchestrationTrigger] TaskOrchestrationContext context,
            List<BasicAuthStatus> appsInViolation
        )
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(BasicAuthV3));

            await context.CallActivityAsync<string>(nameof(MakePlanAsync), appsInViolation);

            string htmlTable = HtmlHelpers.GenerateHtmlTableForBasicAuth(appsInViolation);
            string introMessage = await context.CallActivityAsync<string>(nameof(GenerateIntroMessage));
            await context.CallActivityAsync<bool>(nameof(PostMessageToTeams), new TeamsMessage($"{introMessage} {htmlTable}"));

            await context.CallActivityAsync(nameof(StartTrackingBasicAuthOperation), appsInViolation);

            var approvalOptions = new SubOrchestrationOptions(new TaskOptions(), "CheckAndDisableBasicAuth_instance");
            var approvalResult = await context.CallSubOrchestratorAsync<ApprovalResult>(nameof(GetApprovalAsync), approvalOptions);
            if (approvalResult.Approved == false)
            {
                logger.LogWarning("Operation was not approved, aborting.");
                return;
            }

            await context.CallActivityAsync(nameof(TrackStatusUpdate), new BasicAuthTrackedActionUpdate { Apps = appsInViolation, Status = "InProgress" });


            var waitTool = new WaitFunctionTool(context, logger);
            int stepCounter = 0;

            while (true)
            {
                stepCounter++;
                var historyRaw = await context.Entities.CallEntityAsync<string>(historyEntity, "get");
                var history = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(historyRaw);

                var nextAction = await context.CallActivityAsync<NextAction>(nameof(ExecuteNextActionAsync), new NextActionInput { OperationId = approvalResult.OperationId,  ChatMessages = history, StepCounter = stepCounter });
                if (!string.IsNullOrEmpty(nextAction.FunctionCallContentRaw))
                {
                    var functionCallContent = System.Text.Json.JsonSerializer.Deserialize<FunctionCallContent[]>(nextAction.FunctionCallContentRaw);
                    var call = functionCallContent.Single();

                    if(call.Name == "Wait")
                    {
                        var waitTask = waitTool.Wait(int.Parse(call.Arguments["seconds"].ToString()));
                        var planUpdateTask = context.WaitForExternalEvent<string>("PlanUpdateEvent");

                        if (planUpdateTask == await Task.WhenAny(waitTask, planUpdateTask))
                        {
                            await context.Entities.CallEntityAsync(historyEntity, "appendUser", planUpdateTask.Result);
                        }                        
                    }
                    else if(call.Name == "MarkPlanComplete")
                    {
                        await context.CallActivityAsync(nameof(PostMessageToTeams), new TeamsMessage(call.Arguments["message"].ToString()));
                        // agent is done
                        break;
                    }
                    else                    
                    {
                        //todo, allow running orchestrations
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    await context.CallActivityAsync(nameof(PostMessageToTeams), new TeamsMessage(nextAction.ChatMessage.Text));
                }
            }
        }

        public class BasicAuthTrackedActionUpdate
        {
            public List<BasicAuthStatus> Apps { get; set; }
            public string? Status { get; set; }
        }


        [Function(nameof(StartTrackingBasicAuthOperation))]
        public async Task StartTrackingBasicAuthOperation(
            [ActivityTrigger] List<BasicAuthStatus> appsInViolation
        )
        {
            foreach (var app in appsInViolation)
            {
                TrackedActionHelper.TrackAction(
                    "BasicAuthWorker",
                    app.ResourceId,
                    ActionType.AppStateTracking, //probably should be remediation? But it gets filtered out later if so
                    "Disabling basic auth",
                    new Dictionary<string, string> { { "name", app.Name } }
                );

                TrackedActionHelper.UpdateActionStatus(app.ResourceId, ActionStatus.RequiresApproval);
            }
        }

        [Function(nameof(TrackStatusUpdate))]
        public async Task TrackStatusUpdate(
            [ActivityTrigger] BasicAuthTrackedActionUpdate update
        )
        {
            foreach (var app in update.Apps)
            {
                TrackedActionHelper.UpdateActionStatus(app.ResourceId, ActionStatus.InProgress);
            }
        }

        [Function(nameof(PostMessageToTeams))]
        public async Task<bool> PostMessageToTeams([ActivityTrigger] TeamsMessage teamsMessage, [DurableClient] DurableTaskClient client, FunctionContext executionContext)
        {
            bool result = await this.teamsConnector.PostMessageAsync(teamsMessage);

            //if (result) {
            //    await TrackedActionHelper.TrackAsAssistant(client, teamsMessage.Content);
            //}

            //TODO : Need to figure out a better way to preserve multiple message orderings. Putting an artificial delay for now.
            await Task.Delay(2000);
            return result;
        }

        [Function(nameof(GenerateIntroMessage))]
        public async Task<string> GenerateIntroMessage(       
            [ActivityTrigger] string input,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var introMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                    new ChatMessage(ChatRole.User, "Write a user friendly two line message to tell user that you found a list of App services which have basic auth enabled and its not recommended for secure apps. Its fine to say Hi but Do not write Thanks or Best regards. Also dont write feel free to reach out. Also say I not we."),
            };

            var res = await chatClient.CompleteAsync(introMessages);
            return res.Message.Text;
        }

        [Function(nameof(MakePlanAsync))]
        public async Task MakePlanAsync(
            [ActivityTrigger] List<BasicAuthStatus> violations,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(MakePlanAsync));

            List<ChatMessage> chatMessages =
            [
                new ChatMessage(ChatRole.System, "You create and execute detailed execution plans based on a specified goal. "),
                new ChatMessage(ChatRole.User, """
                    In your first response, I want you to make a detailed plan. You can see the tools that will have available so that you can write better plans, but in this first step you should not execute tools yourself. 
                    Respond with JUST the plan, do NOT include anything else such as 'here is the updated plan' or other commentary.
                    Please create a plan to disable basic auth for these apps one at a time with 30 seconds of delay between each one.
                    """),                
                new ChatMessage(ChatRole.User, string.Join(Environment.NewLine, violations.Select(x => x.ResourceId))),
            ];

            var armTool = new ArmFunctionTool(logger);
            var tools = new List<AIFunction>
            {
                //AIFunctionFactory.Create(armTool.GetAllSubscriptions),
                //AIFunctionFactory.Create(armTool.CheckBasicAuth),
                AIFunctionFactory.Create(armTool.DisableBasicAuth),
            };

            // It might be better if we can build the metadata for the tools but pass it into the prompt, instead of as first class tools, since we dont want actually want a tool call here, just a detailed plan.
            var chatOptions = new ChatOptions { Tools = new List<AITool>(tools), ToolMode = ChatToolMode.Auto }; 
            
            var response = await chatClient.CompleteAsync(chatMessages, chatOptions);
            if (response.FinishReason == ChatFinishReason.ToolCalls)
            {
                throw new Exception("The model made a tool call when it shouldn't have. Fix this later.");
            }

            chatMessages.Add(response.Message);
            logger.LogInformation(response.Message.Text);

            chatMessages.Add(new ChatMessage(ChatRole.User, "Now that the plan is complete, you can move into execution mode."));

            // work around some issue with chat message serialization, do it manually
            await client.Entities.SignalEntityAsync(historyEntity, "set", System.Text.Json.JsonSerializer.Serialize(chatMessages));
        }

        public class NextAction
        {
            public ChatMessage ChatMessage { get; set; }
            public string FunctionCallContentRaw { get; set; }
        }

        public class NextActionInput
        {
            public string OperationId { get; set; }
            public List<ChatMessage> ChatMessages { get; set; }
            public int StepCounter { get; set; }
        }

        [Function(nameof(ExecuteNextActionAsync))]
        public async Task<NextAction> ExecuteNextActionAsync(
            [ActivityTrigger] NextActionInput input,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(ExecuteNextActionAsync));

            // this loop is really generic - it should work for all sorts of flows, just need to specify the appropriate tools.

            var chatMessages = input.ChatMessages;            

            var armTool = new ArmFunctionTool(logger);
            var waitTool = new WaitFunctionTool(null, logger);
            var planTool = new PlanFunctionTool(client);
            var tools = new List<AIFunction>
            {
                //AIFunctionFactory.Create(armTool.GetAllSubscriptions),
                //AIFunctionFactory.Create(armTool.CheckBasicAuth),
                AIFunctionFactory.Create(armTool.DisableBasicAuth),
                AIFunctionFactory.Create(waitTool.Wait),
                AIFunctionFactory.Create(planTool.MarkPlanComplete),
            };

            var chatOptions = new ChatOptions { Tools = new List<AITool>(tools), ToolMode = ChatToolMode.Auto };

            var result = new NextAction();

            // Don't want to allow too many operations within a single activity, better to go back to the orchestration level and checkpoint
            for(int i = 0; i < 5; i++)
            {
                var response = await chatClient.CompleteAsync(chatMessages, chatOptions);
                chatMessages.Add(response.Message);

                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    FunctionCallContent[] functionCallContents = response.Message.Contents.OfType<FunctionCallContent>().ToArray();
                    var call = functionCallContents.Single();
                    if (call.Name == nameof(waitTool.Wait))
                    {

                        // would be better if we append this message after the wait is actually done, with the output from the wait call.
                        var resultContent = new FunctionResultContent(call.CallId, call.Name, "Wait complete.");
                        chatMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));

                        result.ChatMessage = response.Message;
                        result.FunctionCallContentRaw = System.Text.Json.JsonSerializer.Serialize(functionCallContents);
                        break;

                    }
                    else if (call.Name == nameof(planTool.MarkPlanComplete))
                    {
                        result.ChatMessage = response.Message;
                        result.FunctionCallContentRaw = System.Text.Json.JsonSerializer.Serialize(functionCallContents);
                        break;
                    }
                    else
                    {
                        var tool = tools.FirstOrDefault(x => x.Metadata.Name == call.Name);
                        var invokeResult = await tool.InvokeAsync(call.Arguments);
                        var resultContent = new FunctionResultContent(call.CallId, call.Name, invokeResult);
                        chatMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
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

        public class ApprovalEventPayload
        {
            public bool ApprovalAction { get; set; }
            public string DecisionMakerName { get; set; }
        }

        public class ApprovalResult
        {
            public bool Approved { get; set; }
            public string OperationId { get; set; }
        }

        [Function(nameof(GetApprovalAsync))]
        public async Task<ApprovalResult> GetApprovalAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(GetApprovalAsync));

            var approvalUrl = _azureSettings.ApprovalUrl;
            if (string.IsNullOrEmpty(approvalUrl))
            {
                throw new Exception("Approval URL is not set in the configuration.");
            }

            string approvalLink = string.Format(approvalUrl, context.InstanceId);
            await context.CallActivityAsync<bool>(nameof(PostMessageToTeams), new TeamsMessage($"I can disable basic authentication for these applications individually. Would you like me to proceed? <a href='{approvalLink}'>Click here to approve</a>"));

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
                        await context.CallActivityAsync<bool>(nameof(PostMessageToTeams), new TeamsMessage($"Approval Received by {decisionMaker}. I'll continue to disable basic authentication for these applications in a safe manner and will notify you once I am done."));
                        return new ApprovalResult { Approved = true,  OperationId = context.InstanceId.ToString()};
                    }
                    else
                    {
                        await context.CallActivityAsync<bool>(nameof(PostMessageToTeams), new TeamsMessage($"Approval Denied by {decisionMaker}. I will continue to monitor for any additional issues."));
                    }
                }
                else
                {
                    logger.LogInformation($"No approval received within {approvalTimeoutInSeconds} seconds.");
                }
            }

            return new ApprovalResult { Approved = false };
        }


        [Function(nameof(RunBasicAuthV3_HttpStart))]
        public static async Task<HttpResponseData> RunBasicAuthV3_HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(RunBasicAuthV3_HttpStart));

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<OperationalAgentCore.InputMessage>(requestBody);
            
            //await TrackedActionHelper.TrackAsUser(client, data.Content);

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(RunBasicAuthV3Async), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
