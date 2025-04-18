using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for ContainerImageTroubleshooterPlugin
    /// </summary>
    public interface IMetaAgentContainerImageTroubleshooterPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public Guid? ThreadId { get; set; }

        /// <summary>
        /// Lists container image troubleshooter workflows
        /// </summary>
        /// <returns>List of container image troubleshooter workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<ContainerImagePullFailureInput>>> ListContainerImagePullWorkflows();

        /// <summary>
        /// Starts the Container Image Troubleshooter Agent
        /// </summary>
        /// <param name="resourceId">The resource ID</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartContainerImagePullAgent(string resourceId);
    }
}
