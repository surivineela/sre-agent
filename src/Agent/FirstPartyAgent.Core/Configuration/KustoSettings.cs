// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using System.ComponentModel.DataAnnotations;

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

