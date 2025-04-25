using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;
public class CheckApprovalActivityInput
{
    public IReadOnlyList<string> ToolSignatures { get; set; } = new List<string>();
    public string ThreadId { get; set; } = string.Empty;
    public string OrchestrationId { get; set; } = string.Empty;
    public FunctionCallContent? FunctionCall { get; set; }
}
