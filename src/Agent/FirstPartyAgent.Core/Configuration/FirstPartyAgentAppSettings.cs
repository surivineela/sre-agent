using Agent.Core.Configuration;
using FirstPartyAgent.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class FirstPartyAgentAppSettings
    {
        public string ApplicationName { get; set; }
        public string Environment { get; set; }
        public FirstPartyAgentCoreSettings Core { get; set; } = new();
    }

    public class FirstPartyAgentCoreSettings
    {
        public FirstPartyAgentAzureSettings Azure { get; set; } = new();
        
        [Required]
        public FirstPartyAgentExternalSettings External { get; set; } = new();
    }

    public class FirstPartyAgentAzureSettings
    {
        [Required]
        public OpenAISettings OpenAI { get; set; } = new();
    }
}
