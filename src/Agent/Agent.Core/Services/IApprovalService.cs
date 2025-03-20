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
        Task SubmitApprovalDecision(string approvalId, string user, ApprovalDecision status);
    }
}
