using Agent.Runtime.SubAgents.AzureSqlServerAgent;
using Agent.Runtime.SubAgents.EventHubAgent;
using Agent.Runtime.SubAgents.ServiceBusAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for CosmosDbPlugin
    /// </summary>
    public interface IMetaAgentServiceBusPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists Cosmos DB workflows
        /// </summary>
        /// <returns>List of Cosmos DB workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<ServiceBusAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the Cosmos DB Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(ServiceBusAgentActivityInput input);
    }
}
