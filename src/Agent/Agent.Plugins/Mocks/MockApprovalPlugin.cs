// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Definitions;

namespace Agent.Plugins.Mocks
{
    public class MockApprovalPlugin : IApprovalPlugin
    {
        public readonly List<string> ApprovedOperations = new List<string>();

        public Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId, string description)
        {
            throw new NotImplementedException();
            //Approvals[approvalId] = new LongRunningOperationStatus(approvalId, "Approval pending");
        }
    }
}

