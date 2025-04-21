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
    public abstract record SimpleResourceSubAgentActivityInput(List<SimpleResourceSubAgentResourceInformation> Resources)
    {
        /// <summary>
        /// A human-readable message that describes the changes that will be made to the resources if this run goes through.
        /// </summary>
        /// <remarks>Example:
        ///     I can update the resources below to disable key-based access. I will update them one at a time, waiting 30 seconds
        ///     between each app and monitor its health during that time:
        ///       Resource1
        ///       Resource2
        ///       Resource3
        ///     Would you like me to proceed as planned above? I can trigger an approval flow.
        /// </remarks>
        public abstract string GetPlanText();
    }

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
        public virtual string GetPromptText(TPlanInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), "SimpleResourceSubAgent", "SimpleResourceAgentDefaultPlan.txt");
            var systemPrompt = File.ReadAllText(path)
                .Replace("{{ResourceType}}", ResourceTypeName)
                .Replace("{{ActionToTake}}", ActionToTake(input))
                .Replace("{{ToolsList}}", string.Join("\n- ", ToolNames)); //TODO: Fix

            return systemPrompt;
        }

        /// <summary>
        /// The name of the resource being modified. Examples: "CosmosDB", "SQLServer", etc.
        /// </summary>
        /// <remarks>This is used when generating the system prompt.</remarks>
        public abstract string ResourceTypeName { get; }

        /// <summary>
        /// This is a loose string that describes the action that will be taken on the resource.
        /// It should correspond to the actual action that will be taken depending on the user's input.
        /// For example: "disable key-based auth" or "enable TLS 1.2" or "enable encryption".
        /// </summary>
        /// <remarks>This is used when generating the system prompt.</remarks>
        public abstract string ActionToTake(TPlanInput input);

        /// <summary>
        /// This should be the list of tool names that are used to perform the main action being
        /// described in the prompt and requested by the user. Don't be tempted to list out all
        /// the tools as done in the factory class.
        /// For example: ["CosmosDbSetLocalAuthSupport", "Wait"]
        /// </summary>
        /// <remarks>This is used when generating the system prompt.</remarks>
        public abstract string[] ToolNames { get; }


        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, TPlanInput input)
        {
            var resourcesStr = string.Join(Environment.NewLine,
                input.Resources.Select(x => $"{x.ResourceId} should have all changes made."));

            var userMessage = $"Here are the resources that need updating: {resourcesStr}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, GetPromptText(input)),
                new ChatMessage(ChatRole.User, userMessage)
                ];

            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            return messages;
        }
    }
}
