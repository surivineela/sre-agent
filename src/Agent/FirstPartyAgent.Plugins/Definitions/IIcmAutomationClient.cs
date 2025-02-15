using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Plugins
{
    // Remove after IcmAutomationClient is fully migrated
    public interface IIcmAutomationClient
    {
        public Task<(bool, T?)> TriggerIcmWorkflowWithResponse<T>(string workflowName, object? body = null, string triggerName = "manual");
    }
}
