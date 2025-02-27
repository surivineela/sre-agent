// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class DiagnosePluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestChatClient ToolCallingChatClient;

        public DiagnosePluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<IDiagnosePlugin, DiagnosePlugin>();
            services.AddScoped<DiagnosePluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            DiagnosePluginDefinition diagnosePlugin =
                s.GetRequiredService<DiagnosePluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools =
                [
                    AIFunctionFactory.Create(diagnosePlugin.Diagnose),
                    AIFunctionFactory.Create(diagnosePlugin.GetDiagnoseStatus),
                ],
            };

            ToolCallingChatClient = new TestChatClient(
                chatClient.AsBuilder().UseFunctionInvocation().Build(),
                chatOptions,
                _output
            );
        }

        [Fact]
        public async Task Diagnose_ResourceId_ReturnsData()
        {
            await ToolCallingChatClient.CompleteAsync("diagnose for resource_id_1 and resource_id_2");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("diagnosis for 2 apps started"));
        }

        [Fact]
        public async Task GetDiagnoseStatus_ResourceId_PromptsStarting()
        {
            var result =  await ToolCallingChatClient.CompleteAsync("get diagnose status for resource_id_1 and resource_id_2");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("would you like me to start the diagnostic process"));
        }

        public void Dispose()
        {
        }
    }
}
