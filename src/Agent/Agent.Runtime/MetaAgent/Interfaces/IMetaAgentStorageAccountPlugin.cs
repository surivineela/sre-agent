using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.StorageAccountAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for StorageAccountPlugin
    /// </summary>
    public interface IMetaAgentStorageAccountPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists storage account workflows
        /// </summary>
        /// <returns>List of storage account workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<StorageAccountAgentActivityInput>>> ListWorkflowsAsync();

        /// <summary>
        /// Starts the Storage Account Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAgentAsync(StorageAccountAgentActivityInput input);
    }
}
