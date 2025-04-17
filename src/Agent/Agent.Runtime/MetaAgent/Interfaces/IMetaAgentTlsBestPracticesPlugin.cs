using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for TlsBestPracticesPlugin
    /// </summary>
    public interface IMetaAgentTlsBestPracticesPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public ThreadContext? Context { get; set; }

        /// <summary>
        /// Lists TLS best practices workflows
        /// </summary>
        /// <returns>List of TLS best practices workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<TlsBestPracticesInput>>> ListTlsBestPracticeWorkflows();

        /// <summary>
        /// Starts the TLS Best Practices Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartTlsBestPracticeAgent(TlsBestPracticesInput input);
    }
}
