using Agent.Core.Models;
using Agent.Core.Models.Api.v1;


namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for AppReliabilityPlugin
    /// </summary>
    public interface IMetaAgentAppReliabilityPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists app reliability workflows
        /// </summary>
        /// <returns>List of app reliability workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<AppReliabilityInput>>> ListAppReliabilityWorkflows();

        /// <summary>
        /// Starts the App Reliability Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAppReliabilityAgent(AppReliabilityInput input, Guid threadId);
    }
}
