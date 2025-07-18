// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agent.Core.Configuration
{
    public class KustoConfiguration
    {
        public bool Enabled { get; set; }

        public KustoAuthSettings Auth { get; set; } = new();

        public IReadOnlyCollection<KustoRegionalGroupConfiguration> RegionalClusterGroups { get; set; } = [];
    }

    public class KustoRegionalGroupConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyCollection<KustoClusterRegionConfiguration> Regions { get; set; } = [];
    }

    public class KustoAuthSettings
    {
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public KustoAuthType AuthenticationType { get; set; }
        public string Authority { get; set; } = string.Empty;
        public string AuthorityHost { get; set; } = string.Empty;
        public string ApplicationClientId { get; set; } = string.Empty;
        public string ApplicationCertificate { get; set; } = string.Empty;
        public string ManagedIdentityClientId { get; set; } = string.Empty;
        public string ManagedIdentityResourceId { get; set; } = string.Empty;
    }

    public class KustoClusterRegionConfiguration
    {
        [Required]
        public string Region { get; set; } = string.Empty;
        [Required]
        public string ClusterUri { get; set; } = string.Empty;
        [Required]
        public string Database { get; set; } = string.Empty;
    }

    public enum KustoAuthType
    {
        ManagedIdentity,
        UAMI,
        App,
        User, // for testing
    }
}

