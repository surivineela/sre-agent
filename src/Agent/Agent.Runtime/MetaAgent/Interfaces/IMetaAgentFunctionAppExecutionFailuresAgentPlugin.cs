using System;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for FunctionAppExecutionFailuresAgentPlugin
    /// </summary>
    public interface IMetaAgentFunctionAppExecutionFailuresAgentPlugin
    {
        /// <summary>
        /// Gets or sets the thread context.
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Starts the Function App Execution Failures Agent
        /// </summary>
        /// <param name="functionAppResourceId">Resource ID of the function app to investigate</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartFunctionAppExecutionFailuresAgent(string functionAppResourceId);
    }
}
