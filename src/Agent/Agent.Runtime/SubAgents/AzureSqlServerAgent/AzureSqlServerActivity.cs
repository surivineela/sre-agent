using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.AzureSqlServerAgent
{

    public record AzureSqlServerAgentActivityInput(
        [Description("Into what state should we put key-based local auth access for these AzureSqlServers?")]
        FeatureState AzureSqlServerSetLocalAuthSupport,
        [Description("The list of AzureSqlServers (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
        )
        : SimpleResourceSubAgentActivityInput(Resources)
    {
        public AzureSqlServerAgentActivityInput()
            : this(
                FeatureState.Disabled,
                new List<SimpleResourceSubAgentResourceInformation>())
        {
        }

        public override string GetPlanText()
        {
            var resourceBullets = Resources.Select(r => $"\t- {r.ResourceId}");
            return $"""
                I can update the resources below to set their local-auth login support to {AzureSqlServerSetLocalAuthSupport}.
                I will update them one at a time, waiting 30 seconds between each one.

                  {string.Join(Environment.NewLine, resourceBullets)}

                Would you like me to proceed as planned above? I can trigger an approval flow.
                """;
        }
    }

    [DurableTask]
    public class AzureSqlServerActivity : SimpleResourceSubAgentActivityBase<AzureSqlServerAgentActivityInput>
    {
        public AzureSqlServerActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName { get; } = "AzureSqlServer";

        public override string ActionToTake(AzureSqlServerAgentActivityInput input) =>
            input.AzureSqlServerSetLocalAuthSupport == FeatureState.Enabled
            ? "enable local auth support"
            : "disable local auth support";

        public override string[] ToolNames { get; } = [
            nameof(IRemediationPlugin.AzureSqlServerSetLocalAuthSupport),
            nameof(ControlFlowPluginDefinition.Wait)
        ];
    }
}
