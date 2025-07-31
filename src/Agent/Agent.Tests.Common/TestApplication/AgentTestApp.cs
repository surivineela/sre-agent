// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Core.Clients.Search;
using Agent.Core.Clients.Storage;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace Agent.Tests.Common.TestApplication
{
    public class AgentTestApp : WebApplicationFactory<Agent.Web.Program>
    {
        private static readonly TimeSpan DefaultHttpRequestTimeout = TimeSpan.FromSeconds(30);

        private readonly ITestOutputHelper _output;
        private readonly Action<IWebHostBuilder> _configureWebHost;
        private readonly Action<IHostBuilder> _configureDeferredHost;

        public AgentTestApp(ITestOutputHelper output)
            : this(output, _ => { }, _ => { })
        {
        }
        public AgentTestApp(ITestOutputHelper output, Action<IWebHostBuilder> configureWebHost)
            : this(output, configureWebHost, _ => { })
        {
        }

        public AgentTestApp(ITestOutputHelper output, Action<IWebHostBuilder> configureWebHost, Action<IHostBuilder> configureDeferredHost)
        {
            _output = output;
            _configureWebHost = configureWebHost ?? throw new ArgumentNullException(nameof(configureWebHost));
            _configureDeferredHost = configureDeferredHost;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.OverrideOtelLoggingForTestOutput(_output);

            ReplaceServicesWithMocks(builder);

            _configureWebHost(builder);
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            _configureDeferredHost(builder);

            return base.CreateHost(builder);
        }

        protected override void ConfigureClient(HttpClient client)
        {
            if (!Debugger.IsAttached)
            {
                client.Timeout = DefaultHttpRequestTimeout;
            }
            else
            {
                // don't timeout while stepping through code in the debugger
                client.Timeout = TimeSpan.MaxValue;
            }
        }

        private static void ReplaceServicesWithMocks(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.ReplaceAll<ISearchIndexingClient, MockSearchIndexingClient>(ServiceLifetime.Singleton);
                services.ReplaceAll<IAzureBlobStorageClient, MockAzureBlobStorageClient>(ServiceLifetime.Singleton);
            });
        }
    }
}
