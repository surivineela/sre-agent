using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Skills
{
    public static class AddResourcesToAgent
    {
        [Function(nameof(AddSubscriptionsToAgent))]
        public static async Task AddSubscriptionsToAgent([OrchestrationTrigger] TaskOrchestrationContext context, string messageContent)
        {
            ILogger logger = context.CreateReplaySafeLogger("AddSubscriptionsToAgent");

            var currentOperation = new TrackedAgentOperation()
            {
                Id = context.NewGuid(),
                OperationName = "AddSubscriptionsToAgent",
                Annotations = [$"Triggered by message '{messageContent}'"],
                Approver = "",
                CreatedTime = context.CurrentUtcDateTime,
            };
            
            await TrackedAgentOperationActionHelper.AddOperation(context, currentOperation);

            try
            {
                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, await context.CallActivityAsync<string>(nameof(BasicSkills.ReadFileContent), "skills\\AddResourcesToAgent\\subprompt.txt")),
                    new ChatMessage(ChatRole.User, messageContent),
                };

                string response = await context.CallActivityAsync<string>(nameof(BasicSkills.GetOpenAIResponse), messages);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response);

                if (responseObject == null) return;

                string replyMessage = responseObject?["replyMessage"] ?? string.Empty;
                var subsList = ((IEnumerable<dynamic>)responseObject.subscriptions).Select(s => (string)s).ToList();

                if (!string.IsNullOrWhiteSpace(replyMessage))
                {
                    bool result = await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage(replyMessage));
                    await context.CreateTimer(TimeSpan.FromSeconds(5), default);
                    logger.LogInformation($"teams post status: {result}");
                }

                var userSubs = await context.CallActivityAsync<List<AzureSubscription>>(nameof(BasicSkills.GetSubscriptions));
                List<AzureSubscription> subsToAdd = new List<AzureSubscription>();
                List<string> subsToIgnore = new List<string>();

                foreach (var sub in subsList)
                {
                    if (sub.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        subsToAdd.AddRange(userSubs);
                        break;
                    }

                    var userSub = userSubs.FirstOrDefault(p => p.Name.Equals(sub, StringComparison.OrdinalIgnoreCase) || p.Id.Equals(sub, StringComparison.OrdinalIgnoreCase));
                    if (userSub != null) subsToAdd.Add(userSub);
                    else subsToIgnore.Add(sub);
                }

                if (subsToAdd != null && subsToAdd.Any())
                {
                    string partMessage = subsToAdd.Count == 1 ? "subscription " : $"{subsToAdd.Count()} subscriptions including ";
                    await context.CallActivityAsync<bool>(nameof(BasicSkills.PostMessageToTeams), new TeamsMessage($"Ok.. I have verified my access to {partMessage} <b>{subsToAdd.First().Name} ({subsToAdd.First().Id})</b> and started ingesting resource information."));
                    var resourceMemoryEntity = new EntityInstanceId("ResourceMemory", "SREResourceMemory");

                    foreach (var sub in subsToAdd)
                    {
                        List<string> resourcesInSub = await context.CallActivityAsync<List<string>>(nameof(BasicSkills.GetAllResources), sub.Id);
                        var appServiceResources = resourcesInSub.Where(p=>p.Contains("/microsoft.web/sites/", StringComparison.OrdinalIgnoreCase)).ToList();
                        await context.Entities.SignalEntityAsync(resourceMemoryEntity, "Add", new AzureSubscription(sub.Id, sub.Name, appServiceResources));
                    }
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }
    }
}
