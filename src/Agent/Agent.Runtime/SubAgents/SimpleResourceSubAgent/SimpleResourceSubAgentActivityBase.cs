using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents
{
    /// <summary>
    /// A generic record to store information about a resource we want to act upon
    /// </summary>
    public record SimpleResourceSubAgentResourceInformation(string ResourceId, string Name, string Location);

    /// <summary>
    /// The input required by this subagent's planning activity. This is generally the input required to do work, including the
    /// list of resources to act upon, and what to do to them.
    /// </summary>
    /// <remarks>
    /// As an example, if your agent could change perms on a resource, your override could add a field for the new perms.
    /// </remarks>
    public abstract record SimpleResourceSubAgentActivityInput(List<SimpleResourceSubAgentResourceInformation> Resources);

    /// <summary>
    /// The base class for the planning activity of a subagent. This is where the agent will generate a plan to act on the resources.
    /// For a normal agent that acts on one or more resources, you should only need to override <see cref="GetPromptText"/> to provide
    /// the system prompt for your agent.
    /// </summary>
    [DurableTask]
    public abstract class SimpleResourceSubAgentActivityBase<TPlanInput> : TaskActivity<TPlanInput, List<ChatMessage>> 
        where TPlanInput : SimpleResourceSubAgentActivityInput
    {
        private readonly IChatClient chatClient;

        public SimpleResourceSubAgentActivityBase(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        /// <summary>
        /// Override this to return the full prompt for your agent. This is the system prompt that will be used to
        /// perform all the logic.
        /// </summary>
        /// <remarks>
        /// As a recommendation, you can use a file in the same directory as this class to store the prompt and read
        /// it here like this:
        ///   var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof([MyAgent]), "Filename.txt");
        ///   var systemPrompt = File.ReadAllText(path);
        /// </remarks>
        public abstract string GetPromptText();

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, TPlanInput input)
        {
            var resourcesStr = string.Join(Environment.NewLine,
                input.Resources.Select(x => $"{x.ResourceId} should have all changes made."));

            var userMessage = $"Here are the resources that need updating: {resourcesStr}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, GetPromptText()),
                new ChatMessage(ChatRole.User, userMessage)
                ];

            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            return messages;
        }
    }
}
