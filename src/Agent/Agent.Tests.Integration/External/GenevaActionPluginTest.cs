// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data;
using Agent.Plugins;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Models;
using Agent.Plugins.TeamsPlugin;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using FirstPartyAgent.Core.Configuration;
using Kusto.Cloud.Platform.Modularization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.External
{
    [Collection(nameof(CombinedTestCollection))]
    public class GenevaActionPluginTests : IDisposable
    {
        private CombinedFixture _fixture;
        private ITestOutputHelper _output;
        private IConfiguration _config;
        private IHostEnvironment _environment;
        private ServiceProvider _serviceProvider;

        public GenevaActionPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
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
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.Dashboard);

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.GenevaActions);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.ICMWorkflows);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.AgentHelper);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.OneBranchApprovalService);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<KustoSettings>>().Value);
            services.AddSingleton<IAuthenticationService, AuthenticationService>();


            services.AddSingleton<IHostEnvironment>(_environment);
            services.AddSingleton<OneBranchApprovalService>();
            services.AddTransient<IGenevaActionsPlugin, GenevaActionsPlugin>();

            services.AddSingleton<KustoClient>();
            services.AddCosmosClient();

            
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact(Skip = "Only for local testing purpose")]
        public async Task ExecuteGenevaActionTest()
        {
            var plugin = _serviceProvider.GetRequiredService<IGenevaActionsPlugin>();
            var result = await plugin.ExecuteGenevaAction("RestartWebApp", new Dictionary<string, string>
            {
                ["subscriptionId"] = "14300d68-d0c8-4060-82af-bf2d9b70f130",
                ["webappName"] = "hotsite1",
                ["webspaceName"] = "hotsite-rg-CentralUSwebspace"
            });
            Assert.True(result?.Contains("RequestID"));
        }

       
        public void Dispose()
        {
        }
    }
}
