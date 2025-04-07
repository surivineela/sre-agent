using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    [Description("Describes whether a feature is enabled or disabled")]
    public enum FeatureState {
        [Description("The feature should be enabled.")]
        Enabled,
        [Description("The feature should be disabled.")]
        Disabled
    }

    public record StorageAccountAgentActivityInput(
        [Description("Into what state should we put key-based access for these storage accounts?")]
        FeatureState KeyBasedAccessDesiredState,
        [Description("Into what state should we put public access to blobs for these storage accounts?")]
        FeatureState BlobPublicAccessDesiredState,
        [Description("The list of storage accounts (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
        )
        : SimpleResourceSubAgentActivityInput(Resources)
    {
        public StorageAccountAgentActivityInput()
            : this(
                FeatureState.Disabled,
                FeatureState.Disabled,
                new List<SimpleResourceSubAgentResourceInformation>())
        {
        }
    }

    [DurableTask]
    public class StorageAccountAgentActivity : SimpleResourceSubAgentActivityBase<StorageAccountAgentActivityInput>
    {
        public StorageAccountAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string GetPromptText()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(StorageAccountAgent), "StorageAccountAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path)
                .Replace("{{desiredStorageAccountKeyAccess}}", "Disabled")
                .Replace("{{desiredStorageAccountBlobPublicAccess}}", "Disabled");
            return systemPrompt;
        }
    }
}
