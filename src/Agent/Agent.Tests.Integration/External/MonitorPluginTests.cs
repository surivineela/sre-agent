using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.PeriodicMonitor;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.External
{
    [Collection(nameof(CombinedTestCollection))]
    public class MonitorPluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly Session Session;
        private readonly TestChatClient ToolCallingChatClient;

        public MonitorPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddSingleton<IPeriodicMonitor, PeriodicMonitor>();
            services.AddSingleton<IMonitorPlugin, MonitorPlugin>();
            services.AddSingleton<MonitorPluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            var plugin = s.GetRequiredService<MonitorPluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(plugin.StartMonitor),
                    AIFunctionFactory.Create(plugin.GetMonitorInfo),
                    AIFunctionFactory.Create(plugin.SummarizeMonitorActivity),
                    AIFunctionFactory.Create(plugin.UpdateMonitorInterval),
                    AIFunctionFactory.Create(plugin.StopMonitor),
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
        public async Task GetMonitorInfoAsync_NoMonitorIsSetup()
        {
            await ToolCallingChatClient.CompleteAsync($"get monitor details for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("No monitor is currently set up for the specified app service resource."));
        }

        [Fact]
        public async Task GetMonitorInfoAsync_MonitorIsSetup()
        {
            await ToolCallingChatClient.CompleteAsync($"start monitoring app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            await ToolCallingChatClient.CompleteAsync($"get monitor details for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("The details for the monitor on"));
        }

        [Fact]
        public async Task StartMonitorInfoAsync()
        {
            await ToolCallingChatClient.CompleteAsync($"start monitoring app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("Monitoring has been started for the app"));
        }

        [Fact]
        public async Task StopMonitorInfoAsync()
        {
            await ToolCallingChatClient.CompleteAsync($"start monitoring app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            await ToolCallingChatClient.CompleteAsync($"stop monitoring app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("Monitoring has been successfully stopped for the app service."));
        }

        [Fact]
        public async Task UpdateMonitorIntervalAsync()
        {
            await ToolCallingChatClient.CompleteAsync($"start monitoring app /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1");
            await ToolCallingChatClient.CompleteAsync($"update the periodic execution interval of an existing monitor for /subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1 to 5 min");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("has been successfully updated to 5 min"));
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
