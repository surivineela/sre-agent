using Agent.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Configuration
{
    public class KustoSettings
    {
        public KustoAuthSettings Auth { get; set; } = new();
        public KustoClusterSettings Clusters { get; set; } = new();
    }

    public class KustoClusterSettings : List<KustoCluster> { }

    public class KustoAuthSettings
    {
        [Required]
        public KustoAuthenticationType AuthenticationType { get; set; }
        public string Authority { get; set; } = string.Empty;
        public string AuthorityHost { get; set; } = string.Empty;
        public string ApplicationClientId { get; set; } = string.Empty;
        public string ApplicationCertificate { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
    }
}
