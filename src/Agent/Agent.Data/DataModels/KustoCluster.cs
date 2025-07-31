// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Agent.Data.DataModels
{
    public class KustoRegionalGroupSettings
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "regions")]
        public List<KustoCluster> Regions { get; set; } = new();
    }
    public class KustoCluster
    {
        [Required]
        [YamlMember(Alias = "region")]
        public string? Region { get; set; }

        [Required]
        [YamlMember(Alias = "cluster_uri")]
        public string? ClusterUri { get; set; }

        [Required]
        [YamlMember(Alias = "database")]
        public string? Database { get; set; }
    }
}
