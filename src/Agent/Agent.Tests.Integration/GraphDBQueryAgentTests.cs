// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Runtime;
using Agent.Runtime.SubAgents;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class GraphDBQueryAgentTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly GraphDBQueryAgent Agent;

        public GraphDBQueryAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<ITestOutputHelper>(_ => _output);
            services.AddScoped<IGraphDatabaseClient, GremlinGraphDatabaseClient>();
            services.AddScoped<GraphDBPluginDefinition>();
            services.AddScoped<IGraphDBPlugin, GraphDBPlugin>();
            services.AddScoped<GraphDBQueryAgent>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient(_config);

            ServiceProvider s = services.BuildServiceProvider();

            Agent = s.GetRequiredService<GraphDBQueryAgent>();
        }

        [Fact]
        public async Task BadPracticeExample()
        {
            await Agent.Ask($"what is a bad practice you could find from the resource graph");
            Assert.True(await _fixture.TestChatClientFixture.MatchesNaturalLanguagePrompt("lack of managed identity", Agent.ChatHistory));
        }

        // Add more tests once graph can be mocked again

        private async Task _Dispose()
        {
            Assert.True(await _fixture.TestChatClientFixture.MatchesNaturalLanguagePrompt("no exceptions or errors occurred", Agent.ChatHistory));
        }

        public void Dispose()
        {
            _Dispose().GetAwaiter().GetResult();
        }
    }
}
