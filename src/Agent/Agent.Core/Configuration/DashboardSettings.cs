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
    }
}
