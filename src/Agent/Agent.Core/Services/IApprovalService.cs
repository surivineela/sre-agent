using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models.Api.v1;

namespace Agent.Core.Services
{
    public interface IApprovalService
    {
        // TODO, add user information
        Task SubmitApprovalDecision(string approvalId, ApprovalDecision status);
    }
}
