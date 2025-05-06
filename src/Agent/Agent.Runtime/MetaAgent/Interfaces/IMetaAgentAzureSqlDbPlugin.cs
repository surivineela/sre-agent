using Agent.Runtime.SubAgents.AzureSqlServerAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for AzureSqlDbPlugin
    /// </summary>
    public interface IMetaAgentAzureSqlDbPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists AzureSqlDbPlugin workflows
        /// </summary>
        /// <returns>List of AzureSqlDbPlugin workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<AzureSqlServerAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the AzureSqlDbPlugin Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(AzureSqlServerAgentActivityInput input);
    }
}
