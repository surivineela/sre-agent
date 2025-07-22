// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Kusto;
using Agent.Runtime.Reasoning.Models;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Tools
{
    public class KustoRegionalGroupSettings
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "regions")]
        public List<KustoCluster> Regions { get; set; } = new();
    }

    public class KustoConnector : DataConnectorDefinitionBase
    {
        [YamlMember(Alias = "clusterUrl")]
        public string ClusterUrl { get; set; } = default!;

        [YamlMember(Alias = "database")]
        public string Database { get; set; } = default!;

        [YamlMember(Alias = "clusterHint")]
        public string? ClusterHint { get; set; }

        [YamlMember(Alias = "regionalClusterGroups")]
        public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new();

        public override void Validate()
        {
           
        }
    }

}
