using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Models
{
    public class AgentMessageHandlingInput
    {
        public string Message { get; set; }
        public List<AzureSubscription> Subscriptions { get; set; }
    }
}
