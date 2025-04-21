using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Runtime.SubAgents.CosmosDbAgent;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
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

        public override string GetPlanText()
        {
            var resourceBullets = Resources.Select(r => $"\t- {r.ResourceId}");
            return $"""
                I can update the resources below to set their key-based auth to {KeyBasedAccessDesiredState}
                and their blob public-access support to {BlobPublicAccessDesiredState}.
                I will update them one at a time, waiting 30 seconds between each one.

                  {string.Join(Environment.NewLine, resourceBullets)}

                Would you like me to proceed as planned above? I can trigger an approval flow.
                """;
        }
    }

    [DurableTask]
    public class StorageAccountAgentActivity : SimpleResourceSubAgentActivityBase<StorageAccountAgentActivityInput>
    {
        public StorageAccountAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName { get; } = "storage account";

        public override string ActionToTake(StorageAccountAgentActivityInput input)
        {
            var result = new StringBuilder();
            result.Append(input.KeyBasedAccessDesiredState == FeatureState.Enabled
                ? "enable key based access"
                : "disable key based access"
                );
            result.Append(input.BlobPublicAccessDesiredState == FeatureState.Enabled
                ? "and enable blob public access"
                : "and disable blob public access"
                );
            return result.ToString();
        }

        public override string[] ToolNames { get; } = [
            nameof(IRemediationPlugin.StorageAccountSetSharedKeySupport),
            nameof(IRemediationPlugin.StorageAccountSetContainerPublicAccess),
            nameof(ControlFlowPluginDefinition.Wait)];
    }
}
