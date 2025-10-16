// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Services;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.External
{
    [Collection(nameof(CombinedTestCollection))]
    public class WebappPluginTests : IDisposable
    {
        private CombinedFixture _fixture;
        private ITestOutputHelper _output;
        private IConfiguration _config;
        private IHostEnvironment _environment;
        private ServiceProvider _serviceProvider;

        public WebappPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
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


            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ExternalSettings>>().Value.ObserverClient);
            services.AddSingleton<IWebAppPlugin, WebAppPlugin>();
            services.AddSingleton<ObserverClientService>();


            services.AddSingleton<IHostEnvironment>(_environment);


            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact(Skip = "For local test only")]
        public async Task GetWebAppHostnamesTest()
        {
            var plugin = _serviceProvider.GetRequiredService<IWebAppPlugin>();
            var result = await plugin.GetWebAppHostnames("hotsite1", "waws-prod-dm1-211");
            Assert.NotNull(result);
        }


        public void Dispose()
        {
        }
    }
}
