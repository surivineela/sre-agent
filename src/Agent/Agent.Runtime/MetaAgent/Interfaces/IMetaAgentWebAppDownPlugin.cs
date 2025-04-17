using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for WebAppDownPlugin
    /// </summary>
    public interface IMetaAgentWebAppDownPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public ThreadContext? Context { get; set; }

        /// <summary>
        /// Lists WebApp down workflows
        /// </summary>
        /// <returns>List of WebApp down workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<WebAppDownInput>>> ListWebAppDownWorkflows();

        /// <summary>
        /// Starts the WebApp Down Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <param name="context">The thread context</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartWebAppDownAgent(WebAppDownInput input, ThreadContext context);
    }
}
