using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for AppCodeAnalysisPlugin
    /// </summary>
    public interface IMetaAgentAppCodeAnalysisPlugin
    {
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists app code analysis workflows
        /// </summary>
        /// <returns>List of app code analysis workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<AppCodeAnalysisInput>>> ListAppCodeAnalysisWorkflows();

        /// <summary>
        /// Starts the App Code Analysis Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAppCodeAnalysisAgent(AppCodeAnalysisInput input, Guid threadId);
    }
}
