using Agent.Runtime.SubAgents.LocalAuthAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for LocalAuthAgent
    /// </summary>
    public interface IMetaAgentLocalAuthPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists workflows
        /// </summary>
        /// <returns>List of workflows for this agent</returns>
        Task<IReadOnlyList<WorkflowMetadata<LocalAuthAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the Cosmos DB Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(LocalAuthAgentActivityInput input);
    }
}
