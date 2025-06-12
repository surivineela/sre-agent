// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Implementation;
public class ConnectedIntegrationsPlugin : IConnectedIntegrationsPlugin
{

    private readonly DashboardSettings _dashboard;
    private readonly IncidentManagementSettings _incident;
    private readonly AppInsightsSettings _appInsights;

    public ConnectedIntegrationsPlugin(
        DashboardSettings dashboardOptions,
        IncidentManagementSettings incidentOptions,
        AppInsightsSettings appInsightsOptions)
    {
        _dashboard = dashboardOptions;
        _incident = incidentOptions;
        _appInsights = appInsightsOptions;
    }

    public List<IntegrationInfo> GetAllActiveIntegrations()
    {
        var integrations = new List<IntegrationInfo>();

        // Dashboard (e.g. Grafana + Prometheus)
        var dashConfigured =
            !string.IsNullOrWhiteSpace(_dashboard.GrafanaUrl);
        // todo: change the integration detail to include UMI instructions
        integrations.Add(new IntegrationInfo
        {
            Name = "Dashboard",
            IsActive = dashConfigured,
            Details = dashConfigured
                ? $"GrafanaUrl={_dashboard.GrafanaUrl}"
                : "Missing URL or API key. Configure Grafana URL and API key in this Microsoft.App/agents resource's DashboardSettings through an ARM call.",
        });

        // Incident Management (e.g. PagerDuty)
        var incidentConfigured = _incident.Type is not null && (_incident.Type == IncidentManagementType.AzMonitor ||
            (_incident.Type == IncidentManagementType.PagerDuty && !string.IsNullOrWhiteSpace(_incident.ConnectionKey)));
        integrations.Add(new IntegrationInfo
        {
            Name = "IncidentManagement",
            IsActive = incidentConfigured,
            Details = incidentConfigured
                ? $"Kind={_incident.Type}"
                : "No incident management provider configured. Configure IncidentManagementSettings on this Microsoft.App/agents resource through an ARM call."
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
                : "ConnectionString missing. Configure AppInsights settings on this Microsoft.App/Agents resource through an ARM call"
        });

        return integrations;
    }
}

