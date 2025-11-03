// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Plugins.Connector
{
    /// <summary>
    /// Connector for Geneva metrics (MDM) provider
    /// </summary>
    public class GenevaMetricsConnector : DataConnectorDefinitionBase
    {
        /// <summary>
        /// The monitoring account to query metrics from
        /// </summary>
        [YamlMember(Alias = "monitoring_account")]
        public string MonitoringAccount { get; set; } = default!;

        /// <summary>
        /// The metric namespace within the monitoring account
        /// </summary>
        [YamlMember(Alias = "metric_namespace")]
        public string MetricNamespace { get; set; } = default!;

        public GenevaMetricsConnector()
        { }

        public override void ConfigureFromDataSource(string dataSource)
        {
            // Parse dataSource in format "MonitoringAccount/MetricNamespace"
            if (!string.IsNullOrWhiteSpace(dataSource))
            {
                var parts = dataSource.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    MonitoringAccount = parts[0];
                }
                if (parts.Length >= 2)
                {
                    MetricNamespace = parts[1];
                }
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrWhiteSpace(MonitoringAccount))
            {
                throw new ArgumentException("MonitoringAccount cannot be null or whitespace.", nameof(MonitoringAccount));
            }

            if (string.IsNullOrWhiteSpace(MetricNamespace))
            {
                throw new ArgumentException("MetricNamespace cannot be null or whitespace.", nameof(MetricNamespace));
            }
        }
    }
}
