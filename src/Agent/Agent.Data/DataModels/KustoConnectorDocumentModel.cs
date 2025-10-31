// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Data.DataModels;

public record KustoConnectorDocumentModel : ConnectorDocumentModel
{
    public KustoConnectorDocumentModel(ResourceMetadata metadata, KustoConnectorSpec spec)
        : base(metadata, spec)
    {
    }

    public new KustoConnectorSpec Spec => (KustoConnectorSpec)base.Spec;
}

/// <summary>
/// Kusto-specific connector spec
/// </summary>
public class KustoConnectorSpec : ConnectorSpec
{
    public string ClusterUrl { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public string? ClusterHint { get; set; }

    public List<KustoRegionalGroupSettings>? RegionalClusterGroups { get; set; }
}
