using Agent.Runtime.SubAgents.AzureSqlServerAgent;
using Agent.Runtime.SubAgents.EventHubAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for EventHubPlugin
    /// </summary>
    public interface IMetaAgentEventHubPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists Event Hub workflows
        /// </summary>
        /// <returns>List of Event Hub workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<EventHubAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the Event Hub Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(EventHubAgentActivityInput input);
    }
}
