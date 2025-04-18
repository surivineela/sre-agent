using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for FunctionAppConnectivityPlugin
    /// </summary>
    public interface IMetaAgentFunctionAppConnectivityPlugin
    {
        /// <summary>
        /// Gets or sets the thread context.
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Starts the Function App Connectivity Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartFunctionAppConnectivityAgent(FunctionAppConnectivityAgentInput input);
    }
}
