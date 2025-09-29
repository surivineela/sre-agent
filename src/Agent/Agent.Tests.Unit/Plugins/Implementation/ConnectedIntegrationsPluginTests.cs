// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Implementation;
using Agent.Core.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class ConnectedIntegrationsPluginTests
    {
        private DashboardSettings GetDashboardSettings(string? url = null, string? apiKey = null)
        {
            return new DashboardSettings
            {
                GrafanaUrl = url ?? string.Empty,
                GrafanaApiKey = apiKey ?? string.Empty
            };
        }

        private IOptionsMonitor<IncidentManagementSettings> GetIncidentSettings(IncidentManagementType? type = null, string? key = null)
        {
            var mock = new Mock<IOptionsMonitor<IncidentManagementSettings>>();
            mock.Setup(o => o.CurrentValue).Returns(new IncidentManagementSettings
            {
                Type = type,
                ConnectionKey = key
            });
            return mock.Object;
        }

        private AppInsightsSettings GetAppInsightsSettings(string? connStr = null)
        {
            return new AppInsightsSettings
            {
                ConnectionString = connStr ?? string.Empty
            };
        }

        [Fact]
        public void GetAllActiveIntegrations_AllConfigured_ReturnsAllActive()
        {
            var plugin = new ConnectedIntegrationsPlugin(
                GetDashboardSettings("http://grafana", "apikey"),
                GetIncidentSettings(IncidentManagementType.PagerDuty, "pdkey"),
                GetAppInsightsSettings("connstr"));

            var result = plugin.GetAllActiveIntegrations();

            Assert.Equal(3, result.Count);
            Assert.All(result, i => Assert.True(i.IsActive));
        }

        [Fact]
        public void GetAllActiveIntegrations_NoneConfigured_ReturnsAllInactive()
        {
            var plugin = new ConnectedIntegrationsPlugin(
                GetDashboardSettings(),
                GetIncidentSettings(),
                GetAppInsightsSettings());

            var result = plugin.GetAllActiveIntegrations();

            Assert.Equal(3, result.Count);
            Assert.All(result, i => Assert.False(i.IsActive));
        }

        [Fact]
        public void GetAllActiveIntegrations_PartialConfigured_ReturnsCorrectStatus()
        {
            var plugin = new ConnectedIntegrationsPlugin(
                GetDashboardSettings("http://grafana", "apikey"),
                GetIncidentSettings(IncidentManagementType.AzMonitor),
                GetAppInsightsSettings());

            var result = plugin.GetAllActiveIntegrations();

            Assert.Equal(3, result.Count);
            Assert.True(result[0].IsActive); // Dashboard
            Assert.True(result[1].IsActive); // IncidentManagement
            Assert.False(result[2].IsActive); // AppInsights
        }

        [Fact]
        public void GetAllActiveIntegrations_IncidentPagerDutyWithoutKey_IsInactive()
        {
            var plugin = new ConnectedIntegrationsPlugin(
                GetDashboardSettings("http://grafana", "apikey"),
                GetIncidentSettings(IncidentManagementType.PagerDuty, null),
                GetAppInsightsSettings("connstr"));

            var result = plugin.GetAllActiveIntegrations();

            Assert.False(result[1].IsActive); // IncidentManagement
        }

        [Fact]
        public void GetAllActiveIntegrations_DetailsContainExpectedMessages()
        {
            var plugin = new ConnectedIntegrationsPlugin(
                GetDashboardSettings(),
                GetIncidentSettings(),
                GetAppInsightsSettings());

            var result = plugin.GetAllActiveIntegrations();

            Assert.Contains("Missing URL or API key", result[0].Details);
            Assert.Contains("No incident management provider configured", result[1].Details);
            Assert.Contains("ConnectionString missing", result[2].Details);
        }
    }
}
