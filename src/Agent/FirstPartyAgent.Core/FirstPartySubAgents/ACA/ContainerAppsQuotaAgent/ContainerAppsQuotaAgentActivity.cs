using System.ComponentModel;
using Agent.Plugins;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using FirstPartyAgent.Plugins.Definitions;

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
            var resourceBullets = Resources.Select(r => $"{r.ResourceId}");
            return $"""
                I will update the Icm to process the quota request: 
                  {string.Join(Environment.NewLine, resourceBullets)}
                """;
        }

    }

    [DurableTask]
    public class ContainerAppsQuotaAgentActivity : SimpleResourceSubAgentActivityBase<ContainerAppsQuotaAgentActivityInput>
    {
        public ContainerAppsQuotaAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }
        public override string ResourceTypeName { get; } = "ContainerAppsQuotaRequest";

        public override string[] ToolNames { get; } = [
            nameof(ContainerAppsPluginDefinition.ValidateQuotaRequest),
            nameof(ContainerAppsPluginDefinition.SetSubscriptionQuota),
            nameof(ContainerAppsPluginDefinition.GetSubscriptionDetail),
            nameof(ContainerAppsPluginDefinition.GetSubscriptionUsage),
            nameof(IcmPluginDefinition.GetIncidentInfo),
            nameof(IcmPluginDefinition.AddDiscussionEntry),
            nameof(IcmPluginDefinition.ResolveIncident),
            nameof(ControlFlowPluginDefinition.Wait),
            nameof(ControlFlowPluginDefinition.MarkPlanComplete),
            nameof(ControlFlowPluginDefinition.NotifyUser),
            nameof(ControlFlowPluginDefinition.AskUserForInput),
        ];

        public override string ActionToTake(ContainerAppsQuotaAgentActivityInput input)
        {
            return $"Extract quota request information from the Incident, and process it.";
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
