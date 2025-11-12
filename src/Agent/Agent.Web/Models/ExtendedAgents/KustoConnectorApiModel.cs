// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using YamlDotNet.Serialization;

namespace Agent.Web.Models.ExtendedAgents;

public class KustoConnectorApiModel : ExtendedAgentConnectorApiModel
{
    [YamlMember(Alias = "cluster_url")]
    public string ClusterUri { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    [YamlMember(Alias = "cluster_hint")]
    public string? ClusterHint { get; set; }
    [YamlMember(Alias = "regional_cluster_groups")]

    public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new();
}
