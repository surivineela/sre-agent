using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.AppServiceRemediation;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for AppServicePlugin
    /// </summary>
    public interface IMetaAgentAppServiceRemediationPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public ThreadContext? Context { get; set; }

        /// <summary>
        /// Lists app service workflows
        /// </summary>
        /// <returns>List of app service workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<AppServiceRemediationInput>>> ListAppServiceRemediationWorkflows();

        /// <summary>
        /// Starts the App Service Agent
        /// </summary>
        /// <param name="resourceId">The resource ID</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartAppServiceRemediationAgent(AppServiceRemediationInput input);
    }
}
