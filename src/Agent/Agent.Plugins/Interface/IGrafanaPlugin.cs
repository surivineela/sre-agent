// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface IGrafanaPlugin
    {
        Task<byte[]> CaptureScreenshot(string dashboardUid, int width = 1920, int height = 1080);

        // Keep individual methods for flexibility
        Task<string> PublishDashboard(string dashboardJson, bool overwrite = true);
        Task<string> SetupPrometheusDataSource(string dataSourceName, bool isDefault = false);
        Task<string> LinkDataSourceToDashboard(string dashboardUid, string dataSourceUid);
        Task<string> ModifyGrafanaDashboard(string description, string dashboardName, string existingDashboardUid = "");
        // Combined method that handles the complete workflow
        Task<string> PublishDashboardWithPrometheusDataSource(
            string dashboardJson,
            string dataSourceName,
            bool isDefault = false);
    }
}

