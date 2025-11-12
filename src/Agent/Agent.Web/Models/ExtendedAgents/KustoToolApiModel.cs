// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using Agent.Data.DataModels;

namespace Agent.Web.Models.ExtendedAgents;

public class KustoToolApiModel : ExtendedAgentToolApiModel
{
    public KustoExecutionMode Mode { get; set; } = KustoExecutionMode.Function;
    public string? Function { get; set; }
    public string? Query { get; set; }
    public string? File { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? ClusterHint { get; set; }
    public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new List<KustoRegionalGroupSettings>();
    public string ClusterUri { get; set; } = string.Empty;
    public KustoDisplayOptionsDefinition? DisplayOptions { get; set; }
}
