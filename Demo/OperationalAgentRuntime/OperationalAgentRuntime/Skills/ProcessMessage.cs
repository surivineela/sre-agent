using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Skills
{
    public static class ProcessMessage
    {
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
            var currentValue = await context.Entities.CallEntityAsync<List<AzureSubscription>>(resourceMemoryEntity, "Get");
            
            switch (intent.ToLower())
            {
                case "addsubscriptionstoagent":
                    await context.CallSubOrchestratorAsync(nameof(AddResourcesToAgent.AddSubscriptionsToAgent), messageContent);
                    break;
                case "addappstoagent":
                case "disablebasicauth":
                case "rebootapps":
                default:
                    break;
            }
        }

        [Function("ProcessMessageFunction_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ProcessMessageFunction_HttpStart");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<InputMessage>(requestBody);

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ProcessMessage), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
