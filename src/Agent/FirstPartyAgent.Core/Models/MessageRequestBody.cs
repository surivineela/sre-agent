using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Models
{
    public class MessageRequestBody
    {
        public string Sender { get; set; }
        public string Message { get; set; }
        public string AgentMode { get; set; }
        public string SessionId { get; set; }
        public Dictionary<string, string> PromptReplacements { get; set; } = new Dictionary<string, string>();
    }
}
