// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.Integration.Extensions;
using FirstPartyAgent.Tests.Integration.Mocks;
using FirstPartyAgent.ACA.Web;
using FirstPartyAgent.ACA.Web.Services;
using FirstPartyAgent.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Tests.Integration.Logging;

namespace FirstPartyAgent.Tests.Integration
{
    public class TestAgentApplication
    {
        TestWebApplication _firstParityAgentWeb;

        public TestAgentApplication(TestAgentApplicationBuilder testBuilder)
        {
            _firstParityAgentWeb =
                new TestWebApplication(
                builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        if (testBuilder.TestOutputHelper != null)
                        {
                            services.AddSingleton<ILoggerProvider>(new XunitTestHostLoggerProvider(testBuilder.TestOutputHelper));
                        }

                        ApplyDefaultMock(services);

                        if (!testBuilder.BackgroundTaskEnabled)
                        {
                            services.Remove<IHostedService, GpuQuotaIcmBackgroundService>();
                        }
                    });

                });
        }

        public IQuotaAgentService CreateQuotaAgentService()
        {
            return _firstParityAgentWeb.Services.CreateScope().ServiceProvider.GetRequiredService<IQuotaAgentService>();
        }

        public void ApplyDefaultMock(IServiceCollection services)
        {
            services.Replace<IIcmPlugin, IcmPlugin>(ServiceLifetime.Singleton, provider => new MockIcmPlugin());

            services.Replace<IContainerAppsPlugin, ContainerAppsPlugin>(ServiceLifetime.Scoped,
                provider =>
                {
                    var icmSettingsOptionsMock = new Mock<IOptions<IcmSettings>>();
                    icmSettingsOptionsMock.Setup(o => o.Value).Returns(() => null);
                    var containerAppsPlugin = new Mock<ContainerAppsPlugin>(icmSettingsOptionsMock.Object, null);
                    return new MockContainerAppsPlugin(containerAppsPlugin.Object);
                });

            var _taskStorageServiceMock = new Mock<ITaskStorageService>();
            services.Replace<ITaskStorageService, FileBasedStorageService>(ServiceLifetime.Singleton, provider => _taskStorageServiceMock.Object);

        }
    }

    internal class TestWebApplication : WebApplicationFactory<Program>
    {
        private readonly Action<IHostBuilder> _configure;

        public TestWebApplication(Action<IHostBuilder> configure)
        {
            _configure = configure;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            _configure(builder);
            IHost host = builder.Build();
            host.Start();
            return host;
        }
    }
}
