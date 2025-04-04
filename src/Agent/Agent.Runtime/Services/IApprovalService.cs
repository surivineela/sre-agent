// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services
{
    public interface IApprovalService
    {
        Task SubmitApprovalDecision(string approvalId, string user, ApprovalDecision status, ThreadContext? threadContext, string orchestrationId);
    }
}

