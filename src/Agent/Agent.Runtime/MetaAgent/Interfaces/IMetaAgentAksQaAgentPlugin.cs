using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for AksQaAgentPlugin
    /// </summary>
    public interface IMetaAgentAksQaAgentPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists Aks Qa Agent workflows
        /// </summary>
        /// <returns>List of Aks Qa Agent workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<string>>> ListAksQaAgentWorkflow();

        /// <summary>
        /// Starts the Aks Qa Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAksQaAgent(string input);
    }
}
