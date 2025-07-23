// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Kusto
{
    public class KustoCluster
    {
        [Required]
        [YamlMember(Alias = "region")]
        public string? Region { get; set; }

        [Required]
        [YamlMember(Alias = "clusterUri")]
        public string? ClusterUri { get; set; }

        [Required]
        [YamlMember(Alias = "database")]
        public string? Database { get; set; }
    }
}
