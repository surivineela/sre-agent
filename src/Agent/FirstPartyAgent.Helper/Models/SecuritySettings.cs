using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Helper.Models;
public class SecuritySettings
{
    public List<string> AllowedAppIds { get; set; }
    public string ClientId { get; set; }
    public string Authority { get; set; }
    public string TenantId { get; set; }
}
