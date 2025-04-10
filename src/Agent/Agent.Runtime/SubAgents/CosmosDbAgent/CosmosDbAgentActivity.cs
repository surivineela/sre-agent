using System.ComponentModel;
using Agent.Core.Helpers;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.CosmosDbAgent
{

    public record CosmosDbAgentActivityInput(
        [Description("Into what state should we put key-based local auth access for these cosmosDbs?")]
        FeatureState CosmosDbSetLocalAuthSupport,
        [Description("The list of CosmosDbs (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
        )
        : SimpleResourceSubAgentActivityInput(Resources)
    {
        public CosmosDbAgentActivityInput()
            : this(
                FeatureState.Disabled,
                new List<SimpleResourceSubAgentResourceInformation>())
        {
        }
    }

    [DurableTask]
    public class CosmosDbAgentActivity : SimpleResourceSubAgentActivityBase<CosmosDbAgentActivityInput>
    {
        public CosmosDbAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string GetPromptText(CosmosDbAgentActivityInput agentActivityInput)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(CosmosDbAgent), "CosmosDbAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path)
                .Replace("{{desiredLocalAuthSupport}}", agentActivityInput.CosmosDbSetLocalAuthSupport.ToString());
            return systemPrompt;
        }
    }
}
