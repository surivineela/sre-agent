// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Core.Models;

namespace FirstPartyAgent.Core.Configuration
{
    public class KustoSettings
    {
        public KustoAuthSettings Auth { get; set; } = new();
        public IReadOnlyCollection<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = [];
    }

    public class KustoRegionalGroupSettings
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyCollection<KustoCluster> Regions { get; set; } = [];
    }

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

