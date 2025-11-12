// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{

    [Collection(nameof(CombinedTestCollection))]
    public class MetricsPluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestChatClient ToolCallingChatClient;

        public MetricsPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<IMetricsPlugin, MetricsPlugin>();
            services.AddScoped<MetricsPluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient(_config);

            ServiceProvider s = services.BuildServiceProvider();

            MetricsPluginDefinition metricsPlugin = s.GetRequiredService<MetricsPluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(metricsPlugin.GetFunctionAppRequestAvailability),
                    AIFunctionFactory.Create(metricsPlugin.GetWebAppCpuMetrics),
                    AIFunctionFactory.Create(metricsPlugin.GetMemoryMetrics),
                    AIFunctionFactory.Create(metricsPlugin.GetSuccessfulRequestVolumeAsync),
                ]
            };

            ToolCallingChatClient = new TestChatClient(
                chatClient
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build(),
                chatOptions,
                _output
            );
        }

        [Fact]
        public async Task GetWebAppCpuMetrics_ValidResourceId_ReturnsData()
        {
            await ToolCallingChatClient.CompleteAsync($"get cpu usage for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("cpu usage was reported successfully without any errors"));
        }

        [Fact]
        public async Task GetWebAppCpuMetrics_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"get cpu usage for invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid or does not exist"));
        }

        [Fact]
        public async Task GetMemoryMetrics_ValidResourceId_ReturnsData()
        {
            await ToolCallingChatClient.CompleteAsync($"get memory usage for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("memory usage was reported successfully without any errors"));
        }

        [Fact]
        public async Task GetMemoryMetrics_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"get memory usage for invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid or does not exist"));
        }

        [Fact]
        public async Task GetSuccessfulRequestVolumeAsync_ValidResourceId_ReturnsData()
        {
            await ToolCallingChatClient.CompleteAsync($"get successful requests for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("successful requests were reported (even if zero)"));
        }

        [Fact]
        public async Task GetSuccessfulRequestVolumeAsync_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"get successful requests for invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid or does not exist"));
        }

        [Fact]
        public async Task GetFunctionAppRequestAvailability_ValidResourceId_ReturnsData()
        {
            await ToolCallingChatClient.CompleteAsync($"get availability for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("availability was reported successfully without any errors"));
        }

        [Fact]
        public async Task GetFunctionAppRequestAvailability_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync("get availability for invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid or does not exist"));
        }

        public void Dispose()
        {
            // No cleanup needed since we don't have any resources to dispose
        }
    }
}
