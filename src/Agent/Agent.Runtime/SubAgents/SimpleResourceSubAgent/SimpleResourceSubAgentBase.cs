using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents
{
    /// <summary>
    /// Overall input for a run of this subagent.
    /// </summary>
    /// <param name="ActivityInput">The input required by the agent's plan/act phase</param>
    /// <param name="ToolSignatures">The tools it needs to do its thing</param>
    /// <param name="ThreadId">The conversation thread to which this applies</param>
    public abstract record SimpleResourceSubAgentInput<TActivityInput>(TActivityInput ActivityInput, IReadOnlyList<string> ToolSignatures, ThreadContext Context)
        where TActivityInput : SimpleResourceSubAgentActivityInput, new()
    {
        public SimpleResourceSubAgentInput() : this(new TActivityInput(), new List<string>(), new ThreadContext(Guid.Empty, AgentTypeEnum.DTS))
        {
        }
    }

    /// <summary>
    /// Implement this base class with your types in order to create a simple subagent that acts on a list of resources.
    /// You shouldn't need to override any methods for standard agents that act on resources.
    /// </summary>
    /// <remarks>
    /// For each of the type arguments, make sure to subclass with your own types, adding any info you need to the input types.
    /// </remarks>
    /// <typeparam name="TInput">The input type for this agent</typeparam>
    /// <typeparam name="TActivity">The activity for this agent's workflow</typeparam>
    /// <typeparam name="TActivityInput">The input type for the activity</typeparam>
    [DurableTask]
    public abstract class SimpleResourceSubAgentBase<TInput, TActivity, TActivityInput> : GenericAgentOrchestrator<TInput, string>
        where TInput : SimpleResourceSubAgentInput<TActivityInput>
        where TActivity : SimpleResourceSubAgentActivityBase<TActivityInput>
        where TActivityInput : SimpleResourceSubAgentActivityInput, new()
    {
        public async override Task<string> RunAsync(TaskOrchestrationContext context, TInput agentInput)
        {
            var log = context.CreateReplaySafeLogger(this.GetType().Name);

            // Initial planning phase: generate plan
            var chatHistory = await context.CallActivityAsync<List<Microsoft.Extensions.AI.ChatMessage>>(typeof(TActivity).Name, agentInput.ActivityInput);

            // Send a summary and start the execution
            chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                new GetNextActionInput
                {
                    ChatMessages = chatHistory,
                    StepCounter = 0,
                    ToolSignatures = [],
                });

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete
            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                agentInput.Context,
                log);

            return "success";
        }
    }
}
