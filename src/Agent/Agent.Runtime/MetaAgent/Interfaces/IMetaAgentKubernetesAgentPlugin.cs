using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for KubernetesAgentPlugin
    /// </summary>
    public interface IMetaAgentKubernetesAgentPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public ThreadContext? Context { get; set; }

        /// <summary>
        /// Lists Kubernetes agent workflows
        /// </summary>
        /// <returns>List of Kubernetes agent workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<string>>> ListKubernetesAgentWorkflow();

        /// <summary>
        /// Starts the Kubernetes Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartKubernetesAgentWorkflow(string input);
    }
}
