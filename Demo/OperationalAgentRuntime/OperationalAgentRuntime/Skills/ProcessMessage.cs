using System.Diagnostics;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;
using OperationalAgentRuntime.Tools;

namespace OperationalAgentRuntime.Skills
{
    public class ProcessMessage
    {
        private readonly IChatClient chatClient;

        public ProcessMessage(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        [Function(nameof(ProcessMessage))]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context, InputMessage message)
        {
            ILogger logger = context.CreateReplaySafeLogger("ProcessMessage");
            var outputs = new List<string>();

            if (string.IsNullOrWhiteSpace(message?.Content))
                return;

            string messageContent = message.Content;
            logger.LogInformation(messageContent);

            string intent = await context.CallActivityAsync<string>(nameof(IntentClassification.ClassifyIntent), messageContent);

            var resourceMemoryEntity = new EntityInstanceId("ResourceMemory", "SREResourceMemory");
            var azureSubs = await context.Entities.CallEntityAsync<List<AzureSubscription>>(resourceMemoryEntity, "Get");

            var trackedActionsEntity = new EntityInstanceId("TrackedActionsMemory", "TrackedActionsMemoryV2");

            // not clear whether it makes sense to have both of these be separate - the former has the chat messages, the latter has the operations
            var history = await context.Entities.CallEntityAsync<List<TrackedAction>>(trackedActionsEntity, "Get");            
            var operations = await TrackedAgentOperationActionHelper.GetAllOperations(context);

            switch (intent.ToLower())
            {
                case "addsubscriptionstoagent":
                    // For now, this action of adding subscriptions is a "reset" of the previous state
                    await context.Entities.SignalEntityAsync(trackedActionsEntity, "delete");
                    await TrackedAgentOperationActionHelper.ResetAsync(context);

                    await context.CallSubOrchestratorAsync(nameof(AddResourcesToAgent.AddSubscriptionsToAgent), messageContent);
                    break;
                case "addappstoagent":
                case "disablebasicauth":
                case "rebootapps":
                    var appResourceId = messageContent.Split('@').Last();
                    var appName = appResourceId.Split("/").Last();
                    var result = await context.CallActivityAsync<bool>(nameof(BasicSkills.RestartWebApp), appResourceId);
                    Console.WriteLine($"{(result ? "Successfully" : "Unsuccessfully")} restarted {appName} at {appResourceId}");
                    break;
                default:
                    var input = new AgentMessageHandlingInput { Message = messageContent, Subscriptions = azureSubs, Actions = history, Operations = operations != null ? operations.Values.ToList() : new List<TrackedAgentOperation>() };
                    var res = await context.CallActivityAsync<string>(nameof(HandleChatMessageAsync), input);
                    Console.WriteLine(res);
                    await context.CallActivityAsync(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(res));
                    break;
            }
        }

        [Function(nameof(HandleChatMessageAsync))]
        public async Task<string> HandleChatMessageAsync(
            [ActivityTrigger] AgentMessageHandlingInput input,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("HandleMessage");

            var metricsTool = new MetricsFunctionTool();
            var tools = new List<AIFunction>
            {
                AIFunctionFactory.Create(metricsTool.GetAppAvailability),
                AIFunctionFactory.Create(metricsTool.GetMetricAsync)
            };

            var chatOptions = new ChatOptions { Tools = new List<AITool>(tools) };
                        
            //var resources = new StringBuilder();
            //foreach (var sub in input.Subscriptions)
            //{
            //    foreach (var r in sub.Resources)
            //    {
            //        resources.AppendLine(r);
            //    }
            //}

            List<ChatMessage> chatMessages = new List<ChatMessage>()
            {
                new ChatMessage(ChatRole.System, $"You are a helpful operations agent."),                
            };

            foreach(var action in input.Actions)
            {
                chatMessages.Add(new ChatMessage(action.Role, action.Content));
            }

            chatMessages.Add(new ChatMessage(ChatRole.System, "Latest status of tracked operations: " + Environment.NewLine + System.Text.Json.JsonSerializer.Serialize(input.Operations)));

            chatMessages.Add(new ChatMessage(ChatRole.User, input.Message));

            Debug.WriteLine(System.Text.Json.JsonSerializer.Serialize(chatMessages));

            var completion = await chatClient.CompleteAsync(chatMessages, chatOptions);
            return completion.Message.Text;

        }


        [Function("ProcessMessageFunction_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ProcessMessageFunction_HttpStart");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<InputMessage>(requestBody);

            await TrackedActionHelper.TrackAsUser(client, data.Content);

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ProcessMessage), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
