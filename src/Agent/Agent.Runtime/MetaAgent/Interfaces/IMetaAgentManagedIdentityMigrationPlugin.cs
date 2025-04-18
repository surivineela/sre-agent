using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for ManagedIdentityMigrationPlugin
    /// </summary>
    public interface IMetaAgentManagedIdentityMigrationPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists managed identity migration workflows
        /// </summary>
        /// <returns>List of managed identity migration workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<ManagedIdentityMigrationInput>>> ListManagedIdentityMigrations();

        /// <summary>
        /// Starts the Managed Identity Migration Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartManagedIdentityMigrationAgent(ManagedIdentityMigrationInput input);
    }
}
