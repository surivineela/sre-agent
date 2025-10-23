// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Connector
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

        public override void ConfigureFromDataSource(string dataSource)
        {
            if (Uri.TryCreate(dataSource, UriKind.Absolute, out var dsUri))
            {
                ClusterUrl = $"https://{dsUri.Host}";
                Database = dsUri.AbsolutePath.TrimStart('/');
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrWhiteSpace(Database))
            {
                throw new ArgumentException("Database cannot be null or whitespace.", nameof(Database));
            }

            if (string.IsNullOrWhiteSpace(ClusterUrl) || !Uri.TryCreate(ClusterUrl, UriKind.Absolute, out var clusterUri))
            {
                throw new ArgumentException("ClusterUrl must be a valid absolute URI.", nameof(ClusterUrl));
            }

            if (clusterUri.Scheme != Uri.UriSchemeHttps && clusterUri.Scheme != Uri.UriSchemeHttp)
            {
                throw new ArgumentException("ClusterUrl must use http or https scheme.", nameof(ClusterUrl));
            }
        }
    }
}
