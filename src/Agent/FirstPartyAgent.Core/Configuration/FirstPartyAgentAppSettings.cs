using Agent.Core.Configuration;
using FirstPartyAgent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class FirstPartyAgentAppSettings : AppSettings
    {
        public AgentMode AgentMode { get; set; } = AgentMode.None;
    }
}
