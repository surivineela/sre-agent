// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.External
{
    [Collection(nameof(CombinedTestCollection))]
    public class AzureAlertingPluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _environment;
        private readonly ServiceProvider _serviceProvider;

        public AzureAlertingPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            IServiceCollection services = new ServiceCollection();


            _environment = new Mock<IHostEnvironment>().Object;
            Mock.Get(_environment).Setup(e => e.EnvironmentName).Returns(Environments.Development);

            services.AddLogging();
            services.AddSingleton(_config);
            services.AddOptionsWithValidateOnStart<AzureSettings>()
               .BindConfiguration("AppSettings:Core:Azure")
               .ValidateDataAnnotations();

            services.AddOptionsWithValidateOnStart<ExternalSettings>()
               .BindConfiguration("AppSettings:Core:External")
               .ValidateDataAnnotations();

            services.AddOptionsWithValidateOnStart<KustoSettings>()
              .BindConfiguration("AppSettings:Core:External:Kusto")
              .ValidateDataAnnotations();

            services.AddSingleton<IICMWorkflowClient>(sp =>
            {
                var icmWorkflowSettings = sp.GetRequiredService<ICMWorkflowSettings>();
                if (icmWorkflowSettings.Enabled)
                {
                    var logger = sp.GetRequiredService<ILogger<ICMWorkflowClient>>();
                    return new ICMWorkflowClient(_environment, logger, icmWorkflowSettings);
                }
                return new NullableICMWorkflowClient();
            });
            services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.CosmosDB);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.Crawler);
            // add IOption<CrawlerSettings> by using Crawler
            services.AddOptions<CrawlerSettings>()
              .BindConfiguration("AppSettings:Core:External:Crawler")
              .ValidateDataAnnotations();

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.Indexing);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.Action);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.Federation);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.OpenAI);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.Dashboard);

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.GenevaActions);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.ICMWorkflows);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.AgentHelper);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.OneBranchApprovalService);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.IncidentManagement);

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<KustoSettings>>().Value);
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IICMPlugin, ICMPlugin>();
            services.AddSingleton<IICMAPIClient, ICMAPIClient>();


            services.AddSingleton<IHostEnvironment>(_environment);

            services.AddTransient<IAzureAlertingPlugin, AzureAlertingPlugin>();
            services.AddSingleton<IKustoPluginClient, KustoPluginClient>();
            services.AddSingleton<KustoClient>();
            services.AddCosmosClient();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact(Skip = "Only for local testing purpose")]
        public async Task RunAlertKustoQueryTest()
        {
            var plugin = _serviceProvider.GetRequiredService<IAzureAlertingPlugin>();
            var result = await plugin.RunAlertKustoQuery(
                impactStartDate: DateTime.UtcNow.ToString("o"),
                monitoringIterationNumber: 0,
                monitoringGapInSeconds: 60,
                correlationId: Guid.NewGuid().ToString(),
                incidentTitle: "[Public] TestSREAgent [Please ignore]: Hotsite availability issue",
                clusterName: "wawscus",
                databaseName: "wawsprod",
                false);
            
            Assert.NotNull(result);
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}
