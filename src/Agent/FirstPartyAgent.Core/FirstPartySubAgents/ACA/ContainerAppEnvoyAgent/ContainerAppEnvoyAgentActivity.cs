using System.ComponentModel;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    // [MENDATORY]
    public record ContainerAppEnvoyAgentActivityInput(
    [Description("The list of Azure container apps Enovy Resources (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
    )
    : SimpleResourceSubAgentActivityInput(Resources)
    {
        public ContainerAppEnvoyAgentActivityInput()
            : this(new List<SimpleResourceSubAgentResourceInformation>())
        {
        }

        public override string GetPlanText()
        {
            return string.Empty;
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppEnvoyAgentActivity : SimpleResourceSubAgentActivityBase<ContainerAppEnvoyAgentActivityInput>
    {
        public ContainerAppEnvoyAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName => "ContainerAppsEnvoy";

        public override string[] ToolNames => new string[] { };

        public override string ActionToTake(ContainerAppEnvoyAgentActivityInput input)
        {
            throw new NotImplementedException();
        }

        public override string GetPromptText(ContainerAppEnvoyAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppEnvoyAgent), "ContainerAppEnvoyAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
