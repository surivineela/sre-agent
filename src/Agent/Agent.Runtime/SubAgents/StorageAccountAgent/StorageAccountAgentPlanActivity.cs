using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public record StorageAccountAgentPlanInput(
        [Description("Should we disable key-based access to these storage accounts?")]
        bool ShouldDisableKeyBasedAccess,
        [Description("Should we disable public blob access to these storage accounts?")]
        bool ShouldDisableBlobPublicAccess,
        [Description("The list of storage accounts to act on (as ARM resource IDs)")]
        List<StorageAccountAgentAccountStatus> Resources
    );
    public record StorageAccountAgentAccountStatus(string ResourceId, string Name, string Location);


    [DurableTask]
    public class StorageAccountAgentPlanActivity : TaskActivity<StorageAccountAgentPlanInput, List<ChatMessage>>
    {
        private readonly IChatClient chatClient;

        public StorageAccountAgentPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }
        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, StorageAccountAgentPlanInput input)
        {
            // should have keys disabled true/false
            var existingAccountDetails = string.Join(Environment.NewLine,
                input.Resources.Select(x => $"{x.ResourceId} should have all changes made."));

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(StorageAccountAgent), "StorageAccountAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            var userMessage = $"Here are the storage accounts that need updating: {existingAccountDetails}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
                ];

            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            return messages;
        }
    }
}
