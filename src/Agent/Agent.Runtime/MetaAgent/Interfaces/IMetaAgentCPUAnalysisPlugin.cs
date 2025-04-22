using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for CPUAnalysisPlugin
    /// </summary>
    public interface IMetaAgentCPUAnalysisPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists CPU analysis workflows
        /// </summary>
        /// <returns>List of CPU analysis workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<CPUAnalysisInput>>> ListCPUAnalysisWorkflows();

        /// <summary>
        /// Starts the CPU Analysis Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartCPUAnalysisAgent(CPUAnalysisInput input);
    }
}
