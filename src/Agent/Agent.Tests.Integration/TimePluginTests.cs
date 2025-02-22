// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{

    [Collection(nameof(CombinedTestCollection))]
    public class TimePluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestChatClient ToolCallingChatClient;

        public TimePluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<ITimePlugin, TimePlugin>();
            services.AddScoped<TimePluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            TimePluginDefinition timePlugin = s.GetRequiredService<TimePluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(timePlugin.GetCurrentUtcTime),
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
        public async Task GetCurrentUtcTime()
        {
            await ToolCallingChatClient.CompleteAsync($"What is the current time?");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt($"the current time is close to {DateTime.UtcNow}"));
        }

        public void Dispose()
        {
            // No cleanup needed since we don't have any resources to dispose
        }
    }
}