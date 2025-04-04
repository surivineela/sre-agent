// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    public interface IApprovalPlugin
    {
        Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId, string description);
    }
}

