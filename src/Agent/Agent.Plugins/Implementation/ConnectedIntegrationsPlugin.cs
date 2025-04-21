// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Implementation;
public class ConnectedIntegrationsPlugin : IConnectedIntegrationsPlugin
{

    private readonly DashboardSettings _dashboard;
    private readonly IncidentManagementSettings _incident;
    private readonly AppInsightsSettings _appInsights;

    public ConnectedIntegrationsPlugin(
        IOptions<DashboardSettings> dashboardOptions,
        IOptions<IncidentManagementSettings> incidentOptions,
        IOptions<AppInsightsSettings> appInsightsOptions)
    {
        _dashboard = dashboardOptions.Value;
        _incident = incidentOptions.Value;
        _appInsights = appInsightsOptions.Value;
    }

    public List<IntegrationInfo> GetAllActiveIntegrations()
    {
        var integrations = new List<IntegrationInfo>();

        // Dashboard (e.g. Grafana + Prometheus)
        var dashConfigured =
            !string.IsNullOrWhiteSpace(_dashboard.GrafanaUrl) &&
            !string.IsNullOrWhiteSpace(_dashboard.GrafanaApiKey);
        integrations.Add(new IntegrationInfo
        {
            Name = "Dashboard",
            IsActive = dashConfigured,
            Details = dashConfigured
                ? $"GrafanaUrl={_dashboard.GrafanaUrl}"
                : "Missing URL or API key"
        });

        // Incident Management (e.g. PagerDuty)
        var incidentConfigured = !string.IsNullOrWhiteSpace(_incident.Kind);
        integrations.Add(new IntegrationInfo
        {
            Name = "IncidentManagement",
            IsActive = incidentConfigured,
            Details = incidentConfigured
                ? $"Kind={_incident.Kind}"
                : "No incident management provider configured"
        });

        // Application Insights
        var appInsightsConfigured =
            !string.IsNullOrWhiteSpace(_appInsights.ConnectionString);
        integrations.Add(new IntegrationInfo
        {
            Name = "AppInsights",
            IsActive = appInsightsConfigured,
            Details = appInsightsConfigured
                ? $"ConnectionString set"
                : "ConnectionString missing"
        });

        return integrations;
    }
}

