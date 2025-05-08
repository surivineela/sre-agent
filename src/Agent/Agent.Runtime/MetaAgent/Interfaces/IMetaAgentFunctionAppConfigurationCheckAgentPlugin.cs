using Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for FunctionAppConfigurationCheckPlugin
    /// </summary>
    public interface IMetaAgentFunctionAppConfigurationCheckAgentPlugin
    {
        /// <summary>
        /// Gets or sets the thread context.
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Starts the Function App Configuration Check Agent
        /// </summary>
        /// <param name="functionAppResourceId">The Azure resource ID of the Function App to check</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartFunctionAppConfigurationCheckAgent(string functionAppResourceId);
    }
}
