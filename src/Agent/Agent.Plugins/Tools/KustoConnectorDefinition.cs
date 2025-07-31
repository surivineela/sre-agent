// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework.Reasoning.Models;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Tools
{
    //[DataConnector("Kusto")]
    public class KustoConnector : DataConnectorDefinitionBase
    {
        [YamlMember(Alias = "cluster_url")]
        public string ClusterUrl { get; set; } = default!;

        [YamlMember(Alias = "database")]
        public string Database { get; set; } = default!;

        [YamlMember(Alias = "cluster_hint")]
        public string? ClusterHint { get; set; }

        [YamlMember(Alias = "regional_cluster_groups")]
        public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new();

        public KustoConnector()
        { }

        public override void Validate()
        {
        }
    }
}
