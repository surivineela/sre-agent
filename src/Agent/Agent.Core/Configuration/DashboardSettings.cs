// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    public class DashboardSettings
    {
        public string GrafanaUrl { get; set; } = string.Empty;
        public string GrafanaApiKey { get; set; } = string.Empty;

        public string PrometheusUrl { get; set; } = string.Empty;

        public string GrafanaDataSourceName { get; set; } = string.Empty;

        public string MermaidServerAPI { get; set; } = string.Empty;

        // Azure monitor workspace metrics ingestion endpoint
        public string MetricsIngestionEndpoint { get; set; } = string.Empty;

        // 'system' for system managed identity
        // or resource id of user assigned managed identity that has access to the prometheus metrics ingestion endpoint
        public string? Identity { get; set; }
    }
}
