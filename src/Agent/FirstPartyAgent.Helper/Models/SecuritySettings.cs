using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Helper.Models;
public class SecuritySettings
{
    public required List<string> AllowedAppIds { get; set; }
    public required string ClientId { get; set; }
    public required string Authority { get; set; }
    public required string TenantId { get; set; }
}
