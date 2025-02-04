using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using OperationalAgentRuntime.Models;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Tools;
using OperationalAgentRuntime.Helpers;
using System.Threading;
using Microsoft.DurableTask.Entities;
using OperationalAgentRuntime.State;
using HandlebarsDotNet;

namespace OperationalAgentRuntime.Skills.DisableBasicAuth
{
    public class BasicAuthV3
    {
        private readonly IChatClient chatClient;
        private EntityInstanceId historyEntity = new EntityInstanceId("ChatHistoryEntity", "BasicAuth");

        public BasicAuthV3(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        [Function(nameof(BasicAuthV3))]
        public async Task RunBasicAuthV3Async(
            [OrchestrationTrigger] TaskOrchestrationContext context,
            List<BasicAuthStatus> appsInViolation
        )
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(BasicAuthV3));

            var introMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are an AI assistant that helps users generate user friendly messages"),
                    new ChatMessage(ChatRole.User, "Write a user friendly two line message to tell user that you found a list of App services which have basic auth enabled and its not recommended for secure apps. Its fine to say Hi but Do not write Thanks or Best regards. Also dont write feel free to reach out. Also say I not we."),
                };

            string openAIResponse = await context.CallActivityAsync<string>(nameof(BasicSkills.GetOpenAIResponse), introMessages);
            string htmlTable = HtmlHelpers.GenerateHtmlTableForBasicAuth(appsInViolation);
            await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"{openAIResponse} {htmlTable}"));

            var approvalOptions = new SubOrchestrationOptions(new TaskOptions(), "CheckAndDisableBasicAuth_instance");
            var approvalResult = await context.CallSubOrchestratorAsync<ApprovalResult>(nameof(GetApprovalAsync), approvalOptions);
            if(approvalResult.Approved == false)
            {
                logger.LogWarning("Operation was not approved, aborting.");
                return;
            }

            await context.CallActivityAsync<string>(nameof(MakePlanAsync), appsInViolation);

            var waitTool = new WaitFunctionTool(context);
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
                        await context.CallActivityAsync(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(call.Arguments["message"].ToString()));
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
                    await context.CallActivityAsync(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(nextAction.ChatMessage.Text));
                }
            }
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

            var armTool = new ArmFunctionTool();
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

        public class PlanningResult
        {
            public string Plan { get; set; }
            public List<ChatMessage> ChatMessages { get; set; }
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

            var armTool = new ArmFunctionTool(client, input.OperationId);
            var waitTool = new WaitFunctionTool(null);
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

        public class ApprovalResult
        {
            public bool Approved { get; set; }
            public string OperationId { get; set; }
        }

        [Function(nameof(GetApprovalAsync))]
        public async Task<ApprovalResult> GetApprovalAsync(
    [OrchestrationTrigger] TaskOrchestrationContext context
)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(GetApprovalAsync));

            string approvalLink = string.Format(Environment.GetEnvironmentVariable("ApprovalUrl"), context.InstanceId);
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
                            Annotations = [$"Triggered by approval link"],
                            Approver = approvalEvent.DecisionMakerName,
                            CreatedTime = context.CurrentUtcDateTime,
                        };
                        await TrackedAgentOperationActionHelper.AddOperation(context, currentOperation);

                        await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Approval Received by {decisionMaker}. I'll continue to disable basic authentication for these applications in a safe manner and will notify you once I am done."));
                        return new ApprovalResult { Approved = true,  OperationId = currentOperation.Id.ToString()};
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
            var data = JsonConvert.DeserializeObject<InputMessage>(requestBody);
            
            //await TrackedActionHelper.TrackAsUser(client, data.Content);

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(RunBasicAuthV3Async), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
