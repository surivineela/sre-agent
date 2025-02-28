using FirstPartyAgent.ACA.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration
{
    public class ACASettings
    {
        public SREAgentSettings SREAgentSettings { get; set; } = new();
    }
}
