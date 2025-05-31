// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Agent.Framework;

namespace Agent.Plugins
{
    [AgentToolPlugin]
    public class GrafanaPluginDefinition
    {
        private readonly IGrafanaPlugin _grafanaPlugin;

        public GrafanaPluginDefinition(IGrafanaPlugin grafanaPlugin)
        {
            _grafanaPlugin = grafanaPlugin;
        }

        [Description("Captures a screenshot of a Grafana dashboard and returns a base64 string for the image. You must render the dashboard screenshot to the user by calling NotifyUser and including base64 returned from this tool as <img src=\"data:image/png;base64,<base-64-string returned>\" alt=\"Base64 Image\">\r\n")]
        public async Task<byte[]> CaptureScreenshot(
            [Description("The UID of the dashboard to capture")]
            string dashboardUid,

            [Description("The width of the screenshot in pixels")]
            int width = 1920,

            [Description("The height of the screenshot in pixels")]
            int height = 1080)
        {
            return await _grafanaPlugin.CaptureScreenshot(dashboardUid, width, height);
        }

        // Combined method
        [Description("Publishes a dashboard with a linked Prometheus data source in a single operation")]
        public async Task<string> PublishDashboardWithPrometheusDataSource(
            [Description("The dashboard JSON definition")]
            string dashboardJson,

            [Description("Whether to set this as the default data source")]
            bool isDefault = false)
        {
            return await _grafanaPlugin.PublishDashboardWithPrometheusDataSource(
                dashboardJson, "KnowledgeGraph", isDefault);
        }

        [Description("Modifies an existing Grafana dashboard based on user-requested changes or creates a new one from a template. Dashboard can be specified by name or UID.")]
        [RequiresApproval(useOboToken: false)]
        public async Task<string> ModifyGrafanaDashboard(
            [Description("Description of changes the user wants")]
            string description,

            [Description("Name of the target dashboard")]
            string dashboardName,

            [Description("Optional dashboard UID - will look up by name if not provided")]
            string existingDashboardUid = null)
        {
            return await _grafanaPlugin.ModifyGrafanaDashboard(
                description,
                dashboardName,
                existingDashboardUid);
        }
    }
}

