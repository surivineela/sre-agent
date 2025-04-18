using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for ContainerAppsRemediationPlugin
    /// </summary>
    public interface IMetaAgentContainerAppsRemediationPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists container apps remediation workflows
        /// </summary>
        /// <returns>List of container apps remediation workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<string>>> ListContainerAppsRemediationWorkflows();

        /// <summary>
        /// Starts the Container Apps Remediation Agent
        /// </summary>
        /// <param name="input">The input string containing resource information</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartContainerAppsRemediationAgent(string input);
    }
}
