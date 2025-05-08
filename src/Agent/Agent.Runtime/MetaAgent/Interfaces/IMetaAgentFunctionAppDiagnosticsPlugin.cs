using System;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for FunctionAppDiagnosticsPlugin
    /// </summary>
    public interface IMetaAgentFunctionAppDiagnosticsPlugin
    {
        /// <summary>
        /// Gets or sets the thread context.
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Starts the Function App Diagnostic Agent
        /// </summary>
        /// <param name="functionAppResourceId">The resource ID of the Function App to investigate</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartFunctionAppDiagnosticsAgent(string functionAppResourceId);
    }
}
