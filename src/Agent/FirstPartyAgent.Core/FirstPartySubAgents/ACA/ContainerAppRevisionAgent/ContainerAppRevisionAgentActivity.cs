using System.ComponentModel;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    // [MENDATORY]
    public record ContainerAppRevisionAgentActivityInput(
    [Description("The list of Azure container apps revision Resources (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
    )
    : SimpleResourceSubAgentActivityInput(Resources)
    {
        public ContainerAppRevisionAgentActivityInput()
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
    public class ContainerAppRevisionAgentActivity : SimpleResourceSubAgentActivityBase<ContainerAppRevisionAgentActivityInput>
    {
        public ContainerAppRevisionAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName => "Revisions";

        public override string[] ToolNames => new string[] {};

        public override string ActionToTake(ContainerAppRevisionAgentActivityInput input)
        {
            throw new NotImplementedException();
        }

        public override string GetPromptText(ContainerAppRevisionAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppRevisionAgent), "ContainerAppRevisionAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
