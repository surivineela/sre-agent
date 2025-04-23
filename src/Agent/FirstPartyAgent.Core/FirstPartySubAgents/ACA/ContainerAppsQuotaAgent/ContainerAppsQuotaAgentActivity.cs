using System.ComponentModel;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    public record ContainerAppsQuotaAgentActivityInput(
        [Description("The IncidentId of the Azure Container Apps Quota request incident")]
    List<SimpleResourceSubAgentResourceInformation> Resources)
        : SimpleResourceSubAgentActivityInput(Resources)
    {
        public ContainerAppsQuotaAgentActivityInput()
            : this(new List<SimpleResourceSubAgentResourceInformation>())
        {
        }
        public override string GetPlanText()
        {
            throw new NotImplementedException();
        }

    }

    [DurableTask]
    public class ContainerAppsQuotaAgentActivity : SimpleResourceSubAgentActivityBase<ContainerAppsQuotaAgentActivityInput>
    {
        public ContainerAppsQuotaAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }
        public override string ResourceTypeName => throw new NotImplementedException();

        public override string[] ToolNames => throw new NotImplementedException();

        public override string ActionToTake(ContainerAppsQuotaAgentActivityInput input)
        {
            throw new NotImplementedException();
        }

        public override string GetPromptText(ContainerAppsQuotaAgentActivityInput input)
        {
            // Read the system prompt from a file
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppsQuotaAgent), "ContainerAppsQuotaAgent.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
