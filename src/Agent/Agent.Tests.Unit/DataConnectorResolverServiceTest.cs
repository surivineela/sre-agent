// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Plugins.Connector;
using Agent.Runtime.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Agent.Tests.Unit
{

    public class DataConnectorResolverServiceTest : IDisposable
    {
        private readonly Mock<ILogger<DataConnectorResolverService>> _mockLogger;
        private readonly Mock<IOptionsMonitor<List<DataConnectorInstanceSettings>>> _connectorSettings;
        private readonly ILogger<DataConnectorResolverService> _logger;
        private readonly Mock<IHostEnvironment> _env;

        public DataConnectorResolverServiceTest()
        {
            _mockLogger = new Mock<ILogger<DataConnectorResolverService>>();
            _connectorSettings = new Mock<IOptionsMonitor<List<DataConnectorInstanceSettings>>>();
            _connectorSettings.Setup(m => m.CurrentValue).Returns(new List<DataConnectorInstanceSettings>
            {
                new DataConnectorInstanceSettings
                {
                    Name = "KustoDev",
                    DataConnectorType = "Kusto",
                    DataSource = "https://kustodev.kusto.windows.net/TestDB",
                    Identity = "User"
                },
                new DataConnectorInstanceSettings
                {
                    Name = "KustoDev2",
                    DataConnectorType = "Kusto",
                    DataSource = "https://cappseus.eastus.kusto.windows.net/capps",

                    Identity = "/subscriptions/xxxx/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi"
                },
                new DataConnectorInstanceSettings
                {
                    Name = "KustoAgentSpace",
                    DataConnectorType = "Kusto",
                    DataSource = "https://kustoagentspace.kusto.windows.net/TestDB",
                    Identity = "User",
                    Source = DataConnectorSource.AgentSpace
                },

            });
            _logger = _mockLogger.Object;
            _env = new Mock<IHostEnvironment>();
            _env.Setup(e => e.EnvironmentName).Returns("Development");
            _env.Setup(e => e.ApplicationName).Returns("TestApp");
            _env.Setup(e => e.ContentRootPath).Returns("/test/root");
        }

        [Fact]
        public void GetConnectorSetting_Sucess_With_ClusterUrl()
        {
            var dataservice = new DataConnectorResolverService(_connectorSettings.Object, _logger, _env.Object);

            var settings = dataservice.GetConnectorFromSettings<KustoConnector>("Kusto", "Kusto", "cappseus.eastus.kusto.windows.net");
            Assert.NotNull(settings);
            Assert.Equal("KustoDev2", settings.Name);
            Assert.Equal("Kusto", settings.Type);
            Assert.Equal("https://cappseus.eastus.kusto.windows.net", settings.ClusterUrl);
        }

        [Fact]
        public void GetConnectorSetting_Sucess_With_NullCluster()
        {
            var dataservice = new DataConnectorResolverService(_connectorSettings.Object, _logger, _env.Object);

            var settings = dataservice.GetConnectorFromSettings<KustoConnector>("Kusto", "Kusto", string.Empty);
            Assert.NotNull(settings);
            Assert.Equal("KustoDev", settings.Name);
            Assert.Equal("Kusto", settings.Type);
            Assert.Equal("https://kustodev.kusto.windows.net", settings.ClusterUrl);
        }

        [Fact]
        public void GetConnectorSetting_Sucess_With_UnknownDatasource()
        {

            _env.Setup(e => e.EnvironmentName).Returns("Production");
            var dataservice = new DataConnectorResolverService(_connectorSettings.Object, _logger, _env.Object);
            try
            {
                var settings = dataservice.GetConnectorFromSettings<KustoConnector>("Kusto", "Kusto", "unknown.eastus.kusto.windows.net");
                Assert.Null(settings);

            }
            catch (Exception ex)
            {
                Assert.IsType<InvalidOperationException>(ex);
            }
        }
        [Fact]
        public void GetConnectorSetting_Sucess_With_ConnectorName()
        {
            var dataservice = new DataConnectorResolverService(_connectorSettings.Object, _logger, _env.Object);

            var settings = dataservice.GetConnectorFromSettings<KustoConnector>("KustoDev", "Kusto", string.Empty);
            Assert.NotNull(settings);
            Assert.Equal("KustoDev", settings.Name);
            Assert.Equal("Kusto", settings.Type);
            Assert.Equal("https://kustodev.kusto.windows.net", settings.ClusterUrl);
        }

        [Fact]
        public void GetConnectorSetting_Success_With_AgentSpaceAuthSource_SetsAgentSpaceAuthType()
        {
            var dataservice = new DataConnectorResolverService(_connectorSettings.Object, _logger, _env.Object);

            var settings = dataservice.GetConnectorFromSettings<KustoConnector>("KustoAgentSpace", "Kusto", string.Empty);
            Assert.NotNull(settings);
            Assert.Equal("KustoAgentSpace", settings.Name);
            Assert.Equal("Kusto", settings.Type);
            Assert.Equal("https://kustoagentspace.kusto.windows.net", settings.ClusterUrl);
            Assert.Equal(Agent.Framework.ConnectorAuthType.AgentSpace, settings.Auth.AuthenticationType);
        }

        public void Dispose()
        {
        }
    }
}
