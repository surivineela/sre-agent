using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;

namespace Agent.Runtime.MetaAgent;
public class VmRdpInvestigatorPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly VmRdpInvestigatorAgentFactory _vmRdpInvestigatorAgentFactory;

    public ThreadContext? Context { get; set; }

    public VmRdpInvestigatorPlugin(
        DurableTaskClient durableTaskClient,
        VmRdpInvestigatorAgentFactory vmRdpInvestigatorAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _vmRdpInvestigatorAgentFactory = vmRdpInvestigatorAgentFactory;
    }

    [KernelFunction("list_vm_rdp_failure_investigate_workflow")]
    [Description("List the information of started VM RDP investigation workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<VmRdpInvestigatorAgentInput>>> ListVmRdpInvestigateWorkflows()
    {
        var list = new List<WorkflowMetadata<VmRdpInvestigatorAgentInput>>();

        try
        {
            await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
            {
                try
                {
                    var input = _vmRdpInvestigatorAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
                    list.Add(new WorkflowMetadata<VmRdpInvestigatorAgentInput>(
                        WorkflowInstanceId: instance.InstanceId,
                        Input: input));
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }
        }
        catch
        {
            // Ignore errors while fetching instances
        }

        return list;
    }

    [KernelFunction("start_vm_rdp_failure_investigate_workflow")]
    [Description("Start the workflow to investigate RDP failures to an Azure Virtual Machine.")]
    public async Task<string> StartVMRdpInvestigatorAgent(
        [Description("Inputs to the agent that includes arm resource id for the VM to investigate, a list of tools and thead context.")] VmRdpInvestigatorAgentInput input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }
        
        var instanceId = await _vmRdpInvestigatorAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to investigate RDP failures to VM: {instanceId}";
    }
}
