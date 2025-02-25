using Agent.Core.Helpers;
using Agent.Core.Plugins;
using Agent.Plugins;
using Agent.Plugins.Implementation;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.External
{
    [Collection(nameof(CombinedTestCollection))]
    public class CurrentStatePluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly Session Session;
        private readonly TestChatClient ToolCallingChatClient;

        public CurrentStatePluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddSingleton<ICurrentStatePlugin, CurrentStatePlugin>();
            services.AddSingleton<CurrentStatePluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            var plugin = s.GetRequiredService<CurrentStatePluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(plugin.GetCurrentAppState),
                    AIFunctionFactory.Create(plugin.GetCurrentBotState)
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
        public async Task GetCurrentAppState()
        {
            await ToolCallingChatClient.CompleteAsync($"Get current app state for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("There is no tracked state found for the app service"));
        }

        [Fact]
        public async Task GetCurrentBotState()
        {
            await ToolCallingChatClient.CompleteAsync($"What is the current state of this AI Agent?");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("The current state of this AI Agent is as follows"));
        }

        private async Task _Dispose()
        {
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("no exceptions or errors occurred"));

            _output.WriteLine("\nAll chat messages:");
            foreach (var message in ToolCallingChatClient.ChatHistory)
            {
                if (message.Text != null)
                {
                    _output.WriteLine(message.Text);
                }
            }
        }

        public void Dispose()
        {
            _Dispose().GetAwaiter().GetResult();
        }
    }
}
