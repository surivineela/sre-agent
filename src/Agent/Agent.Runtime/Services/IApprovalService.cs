// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services
{
    public interface IApprovalService
    {
        Task<IList<Approval>> GetApprovals(Guid threadId);
        Task<Approval> GetApproval(Guid threadId, string approvalId);
        Task SubmitApprovalDecision(string approvalId, string user, ApprovalDecision status, Guid threadId, string? oboToken = null);
    }
}

