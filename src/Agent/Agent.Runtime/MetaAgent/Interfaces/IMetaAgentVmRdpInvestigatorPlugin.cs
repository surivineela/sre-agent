using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;

namespace Agent.Runtime.MetaAgent
{
    /// <summary>
    /// Interface for VmRdpInvestigatorPlugin
    /// </summary>
    public interface IMetaAgentVmRdpInvestigatorPlugin
    {
        /// <summary>
        /// Gets or sets the thread context
        /// </summary>
        public ThreadContext? Context { get; set; }

        /// <summary>
        /// Lists VM RDP investigator workflows
        /// </summary>
        /// <returns>List of VM RDP investigator workflows</returns>
        Task<IReadOnlyList<WorkflowMetadata<VmRdpInvestigatorAgentInput>>> ListVmRdpInvestigateWorkflows();

        /// <summary>
        /// Starts the VM RDP Investigator Agent
        /// </summary>
        /// <param name="input">The input data for the agent</param>
        /// <returns>Result of starting the agent</returns>
        Task<string> StartVMRdpInvestigatorAgent(VmRdpInvestigatorAgentInput input);
    }
}
