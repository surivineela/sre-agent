using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Plugins.Definitions;

namespace Agent.Plugins.Mocks
{
    public class MockApprovalPlugin : IApprovalPlugin
    {
        public readonly List<string> ApprovedOperations = new List<string>();

        public Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId)
        {
            throw new NotImplementedException();
            //Approvals[approvalId] = new LongRunningOperationStatus(approvalId, "Approval pending");
        }
    }
}
