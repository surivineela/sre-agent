using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;
public class CheckApprovalActivityOutput
{
    public string ApprovalId { get; set; } = string.Empty;
    public ToolApprovalStatus ApprovalStatus { get; set; } = ToolApprovalStatus.Pending;
    public string? OboToken { get; set; }
}
