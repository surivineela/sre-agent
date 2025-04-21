using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.EventHubAgent
{
    public record EventHubAgentActivityInput(
        [Description("Into what state should we put key-based access for these event hubs?")]
        FeatureState EventHubSetLocalAuthSupport,
        [Description("The list of event hubs (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
        )
        : SimpleResourceSubAgentActivityInput(Resources)
    {
        public EventHubAgentActivityInput()
            : this(
                FeatureState.Disabled,
                new List<SimpleResourceSubAgentResourceInformation>())
        {
        }

        public override string GetPlanText()
        {
            var resourceBullets = Resources.Select(r => $"\t- {r.ResourceId}");
            return $"""
                I can update the resources below to set their key-based auth to {EventHubSetLocalAuthSupport}
                I will update them one at a time, waiting 30 seconds between each one.

                  {string.Join(Environment.NewLine, resourceBullets)}

                Would you like me to proceed as planned above? I can trigger an approval flow.
                """;
        }
    }

    [DurableTask]
    public class EventHubAgentActivity : SimpleResourceSubAgentActivityBase<EventHubAgentActivityInput>
    {
        public EventHubAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName { get; } = "event hub";

        public override string ActionToTake(EventHubAgentActivityInput input)
        {
            var result = new StringBuilder();
            result.Append(input.EventHubSetLocalAuthSupport == FeatureState.Enabled
                ? "enable key based access"
                : "disable key based access"
                );
            return result.ToString();
        }

        public override string[] ToolNames { get; } = [
            nameof(IRemediationPlugin.EventHubSetLocalAuthSupport),
            nameof(ControlFlowPluginDefinition.Wait)];
    }
}
