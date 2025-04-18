using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.RdpInvestigatorAgent;
public sealed record VmRdpInvestigatorAgentInput(
    [Description("Full azure resource id of the Azure virtual machine resource needs to be investigated. Should restart with /subscriptions/...")] string VirtualMachineResourceId,
    [Description("Signature of a list of tools available for the agent to use")] IReadOnlyList<string> ToolSignatures,
    [Description("Thread Id")] Guid ThreadId);
