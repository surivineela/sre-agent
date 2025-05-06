using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Definitions;
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

        public override string GetPlanText()
        {
            var resourceBullets = Resources.Select(r => $"\t- {r.ResourceId}");
            return $"""
                I can update the resources below to set their local-auth login support to {CosmosDbSetLocalAuthSupport}.
                I will update them one at a time, waiting 30 seconds between each one.

                  {string.Join(Environment.NewLine, resourceBullets)}

                Would you like me to proceed as planned above? I can trigger an approval flow.
                """;
        }
    }

    [DurableTask]
    public class CosmosDbAgentActivity : SimpleResourceSubAgentActivityBase<CosmosDbAgentActivityInput>
    {
        public CosmosDbAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName { get; } = "CosmosDB";

        public override string ActionToTake(CosmosDbAgentActivityInput input) =>
            input.CosmosDbSetLocalAuthSupport == FeatureState.Enabled
            ? "enable local auth support"
            : "disable local auth support";

        public override string[] ToolNames { get; } = [
            nameof(IRemediationPlugin.CosmosDbSetKeyBasedAuthenticationSupport),
            nameof(ControlFlowPluginDefinition.Wait)
        ];
    }
}
