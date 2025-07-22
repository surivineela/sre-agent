using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Configuration;
public class IcmAgentSettings
{
    public string IcmKustoClusterUri {  get; set; } = string.Empty;
    public string IcmKustoDataBase { get; set; } = string.Empty;
}
