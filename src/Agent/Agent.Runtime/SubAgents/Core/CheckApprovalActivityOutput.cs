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
    public Guid? ApprovalId { get; set; }
    public ToolApprovalStatus ApprovalStatus { get; set; } = ToolApprovalStatus.Pending;
}
