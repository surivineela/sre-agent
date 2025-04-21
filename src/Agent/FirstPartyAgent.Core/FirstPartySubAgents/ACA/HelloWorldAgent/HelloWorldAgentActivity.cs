using System.ComponentModel;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    // [MENDATORY]
    public record HelloWorldAgentActivityInput(
    [Description("The list of Azure hello world Resources (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
    )
    : SimpleResourceSubAgentActivityInput(Resources)
    {
        public HelloWorldAgentActivityInput()
            : this(new List<SimpleResourceSubAgentResourceInformation>())
        {
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class HelloWorldAgentActivity : SimpleResourceSubAgentActivityBase<HelloWorldAgentActivityInput>
    {
        public HelloWorldAgentActivity(IChatClient chatClient) : base(chatClient)
        {
        }

        public override string GetPromptText(HelloWorldAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(HelloWorldAgent), "HelloWorldAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
