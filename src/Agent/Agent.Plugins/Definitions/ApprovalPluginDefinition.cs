using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    public class ApprovalPluginDefinition
    {
        private readonly IApprovalPlugin _approvalPlugin;
        public ApprovalPluginDefinition(IApprovalPlugin approvalPlugin)
        {
            _approvalPlugin = approvalPlugin;
        }

        [Description("Starts an approval flow. You will be notified when the approval happens.")]
        public async Task<LongRunningOperationStatus> StartApprovalFlow(
            [Description("Short name for operation for which the approval is being asked for. For a TLS update, use the operationName `UpdateTls`")]
            string operationName,
            [Description("The concise description of what the operation is doing to be displayed on the approval page")]
            string description)
        {
            return await _approvalPlugin.StartApprovalFlow(operationName, description);
        }
    }
}
