using FirstPartyAgent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Models
{
    public class AlertRequestBody
    {
        public string Source { get; set; }
        public string IncidentId { get; set; }
        public string? CustomMessage { get; set; }
        public string AgentMode { get; set; }
        public string? AlertId { get; set; }
        public string? AdditionalPayload { get; set; }
    }
}
