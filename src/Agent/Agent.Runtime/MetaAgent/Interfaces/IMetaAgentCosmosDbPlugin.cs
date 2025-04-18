using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.CosmosDbAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for CosmosDbPlugin
    /// </summary>
    public interface IMetaAgentCosmosDbPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists Cosmos DB workflows
        /// </summary>
        /// <returns>List of Cosmos DB workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<CosmosDbAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the Cosmos DB Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(CosmosDbAgentActivityInput input);
    }
}
