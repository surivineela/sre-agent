using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models.Api.v1
{
    public enum ApprovalDecision
    {
        Pending,
        Approved,
        Rejected
    }

    public record Approval(
        string Id,
        string Title,
        ApprovalDecision Status,
        DateTime CreatedTimestamp,
        DateTime? DecisionTimestamp,
        string? decisionUserId
        )
    {
    }
}
