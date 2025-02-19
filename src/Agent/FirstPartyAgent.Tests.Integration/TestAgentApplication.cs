// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.Integration.Extensions;
using FirstPartyAgent.Tests.Integration.Mocks;
using FirstPartyAgent.Web;
using FirstPartyAgent.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

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
            services.Replace<IContainerAppsPlugin, ContainerAppsPlugin>(ServiceLifetime.Scoped, provider => new MockContainerAppsPlugin());

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
