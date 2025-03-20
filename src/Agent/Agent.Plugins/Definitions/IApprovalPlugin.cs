using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    public interface IApprovalPlugin
    {
        Task<LongRunningOperationStatus> StartApprovalFlow(string approvalId, string description);
    }
}
