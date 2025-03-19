// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.Integration.Extensions;
using FirstPartyAgent.Tests.Integration.Mocks;
using FirstPartyAgent.ACA.Web;
using FirstPartyAgent.ACA.Web.Services;
using FirstPartyAgent.Core.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Tests.Integration.Logging;
using Agent.Core.Configuration;

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

                        if (testBuilder.DefaultMockEnabled)
                        {
                            ApplyDefaultMock(services);
                        }

                        if (!testBuilder.BackgroundTaskEnabled)
                        {
                            services.Remove<IHostedService, GpuQuotaIcmBackgroundService>();
                        }
                    });

                });
        }

        public IServiceProvider ServiceProvider => _firstParityAgentWeb.Services;

        public IQuotaAgentService CreateQuotaAgentService()
        {
            return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<IQuotaAgentService>();
        }

        public void ApplyDefaultMock(IServiceCollection services)
        {
            var code = services.BuildServiceProvider().GetRequiredService<OpenAISettings>();


            services.Replace<IIcmPlugin, IcmPlugin>(ServiceLifetime.Singleton, provider => new MockIcmPlugin());
            services.Replace<IContainerAppsPlugin, ContainerAppsPlugin>(ServiceLifetime.Singleton, provider => new MockContainerAppsPlugin());

            var _taskStorageServiceMock = new Mock<ITaskStorageService>();
            services.Replace<ITaskStorageService, FileBasedStorageService>(ServiceLifetime.Singleton, provider => _taskStorageServiceMock.Object);

        }
    }

    internal class TestWebApplication : WebApplicationFactory<FirstPartyAgent.ACA.Web.Program>
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
