using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for FunctionAppDeploymentChecksPlugin
    /// </summary>
    public interface IMetaAgentFunctionAppDeploymentChecksAgentPlugin
    {
        /// <summary>
        /// Gets or sets the thread context.
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Starts the Function App Deployment Checks Agent
        /// </summary>
        /// <param name="functionAppResourceId">The Azure resource ID of the Function App to check</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartFunctionAppDeploymentChecksAgent(string functionAppResourceId);
    }
}
