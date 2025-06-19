using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration;
public class OneBranchApprovalServiceSettings
{
    public bool Enabled { get; set; } = false;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;

}
