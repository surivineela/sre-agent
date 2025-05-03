using System.ComponentModel;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.CorednsAgent
{
    public record CoreDnsResourceInformation : SimpleResourceSubAgentResourceInformation
    {
        [Description("The name of the managed Kubernetes cluster or azure container apps environment associated with the container app.")]
        public string? ManagedClusterName; // Example: "victoriouspond-6e0afa3a"

        [Description("The start of the time range for the analysis. This is also called the fromDate")]
        public DateTime FromDate;       // Example: 2025-04-01

        [Description("The end of the time range for the analysis. This is also called the toDate")]
        public DateTime ToDate;        // Example: 2025-04-29

        [Description("The Incident ID (IcM ID) associated with the issue.")]
        public string? IcmId;   // Example: "622811149"

        [Description("The Azure region where the container app is deployed.")]
        public string? Region;  // Example: "francecentral"

        public CoreDnsResourceInformation(string ResourceId, string Name, string Location) : base(ResourceId, Name, Location)
        {
        }
    }

    // [MENDATORY]
    public record ContainerAppCorednsAgentActivityInput(
    [Description("The list of Azure container apps Coredns Resources (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
    )
    : SimpleResourceSubAgentActivityInput(Resources)
    {
        public ContainerAppCorednsAgentActivityInput()
            : this(new List<SimpleResourceSubAgentResourceInformation>())
        {
        }

        public override string GetPlanText()
        {
            return "I am the **Container Apps Coredns Insights Agent**. I specialize in helping you diagnose and resolve issues with DNS resolution related issues.";
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppCorednsAgentActivity : SimpleResourceSubAgentActivityBase<ContainerAppCorednsAgentActivityInput>
    {
        public ContainerAppCorednsAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string ResourceTypeName => "Coredns";

        public override string[] ToolNames => new string[] {};

        public override string ActionToTake(ContainerAppCorednsAgentActivityInput input)
        {
            throw new NotImplementedException();
        }

        public override string GetPromptText(ContainerAppCorednsAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppCorednsAgent), "ContainerAppCorednsAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
