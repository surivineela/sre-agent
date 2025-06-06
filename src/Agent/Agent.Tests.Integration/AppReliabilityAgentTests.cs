using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Agent.Runtime.SubAgents.Core;
using Agent.Tests.Common;
using Agent.Tests.Integration.Fixtures;
using Azure.AI.OpenAI;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;
using Helper = Agent.Tests.Integration.Helpers.Helper;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class AppReliabilityAgentTests : IAsyncLifetime
    {
        private TimeProvider _timeProvider;
        private ILogger _logger;
        private IHost _host;
        private DurableTaskClient _durableTaskClient;
        private AppReliabilityAgentFactory _agentFactory;
        private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";

        private MockArmPlugin _mockArmPlugin;
        private MockMetricsPlugin _mockMetricsPlugin;
        private MockGithubWorkflowTriggerPlugin _mockGithubPlugin;
        private MockTimePlugin _mockTimePlugin;
        private MockMIConfigurationCheckPlugin _mockMIConfigurationCheckPlugin;
        private MockAppIdentityUpdatePlugin _mockAppIdentityUpdatePlugin;
        private IThreadRepository _mockThreadRepository;

        private List<AppReliability> _testApps = new List<AppReliability>
        {
            new AppReliability(ResourceId : $"{BaseResourceId}/app1", AlwaysOnEnabled: false, HealthCheckEnabled: false, AutoHealEnabled: false, NumberOfWorkers: 1),
            new AppReliability(ResourceId : $"{BaseResourceId}/app2", AlwaysOnEnabled: false, HealthCheckEnabled: true, AutoHealEnabled: false, NumberOfWorkers: 2),
            new AppReliability(ResourceId : $"{BaseResourceId}/app3", AlwaysOnEnabled: true, HealthCheckEnabled: true, AutoHealEnabled: false, NumberOfWorkers: 1),
            new AppReliability(ResourceId : $"{BaseResourceId}/app4", AlwaysOnEnabled: false, HealthCheckEnabled: false, AutoHealEnabled: true, NumberOfWorkers: 2),
            new AppReliability(ResourceId : $"{BaseResourceId}/app5", AlwaysOnEnabled: true, HealthCheckEnabled: true, AutoHealEnabled: true, NumberOfWorkers: 3)
        };

        public static class ReliableApp
        {
            public static int NumberOfWorkers = 3;
            public static bool AlwaysOnEnabled = true;
            public static bool HealthCheckEnabled = true;
            public static bool AutoHealEnabled = true;
            public static Tuple<bool, bool, bool, int> Configuration = new Tuple<bool, bool, bool, int>(AlwaysOnEnabled, HealthCheckEnabled, AutoHealEnabled, NumberOfWorkers);
        }

        public AppReliabilityAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            var config = fixture.ConfigFixture;
            _logger = testOutputHelper.ToLogger<ILogger>();
            _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));

            var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(AppReliabilityAgentTests)));

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddXUnit(testOutputHelper)
                            .SetMinimumLevel(LogLevel.Trace);
                    });

                    var openAISettings = config.AzureSettings.OpenAI;
                    var openAIClient = new AzureOpenAIClient(new Uri(openAISettings.Endpoint), new System.ClientModel.ApiKeyCredential(config.AzureSettings.OpenAI.ApiKey));

                    var diskCache = new TestCachingChatClientBuilderExtensions.DiskCache(cacheDir);

                    var durableConnectionString = config.AzureSettings.DTS.ConnectionString;
                    if (string.IsNullOrEmpty(durableConnectionString))
                    {
                        durableConnectionString = "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
                    }

                    services.AddSingleton(openAIClient);

                    services.AddChatClient(serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(openAISettings.LLMDeploymentName), ServiceLifetime.Singleton)
                        .UseAgenticLogging()
                        .UseDistributedCache(diskCache);

                    // if we want, we can have different chat clients, some with function invocation enabled
                    services.AddKeyedChatClient("function-invocation-enabled", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(openAISettings.LLMDeploymentName), ServiceLifetime.Singleton)
                        .UseAgenticLogging()
                        .UseDistributedCache(diskCache)
                        .UseFunctionInvocation();

                    // -- test specific
                    _mockArmPlugin = new MockArmPlugin(_timeProvider);
                    _mockArmPlugin.ConfigureReliability(_testApps.ToDictionary(x => x.ResourceId));
                    _mockMetricsPlugin = new MockMetricsPlugin(_timeProvider);
                    _mockGithubPlugin = new MockGithubWorkflowTriggerPlugin(_timeProvider);
                    _mockTimePlugin = new MockTimePlugin(_timeProvider);
                    _mockMIConfigurationCheckPlugin = new MockMIConfigurationCheckPlugin();
                    _mockAppIdentityUpdatePlugin = new MockAppIdentityUpdatePlugin(_mockMIConfigurationCheckPlugin);

                    services.AddSingleton<IArmPlugin>(_mockArmPlugin);
                    services.AddSingleton<IMetricsPlugin>(_mockMetricsPlugin);
                    services.AddSingleton<IGithubWorkflowTriggerPlugin>(_mockGithubPlugin);
                    services.AddSingleton<ITimePlugin>(_mockTimePlugin);
                    services.AddSingleton<IMIConfigurationCheckPlugin>(_mockMIConfigurationCheckPlugin);
                    services.AddSingleton<IAppIdentityUpdatePlugin>(_mockAppIdentityUpdatePlugin);
                    services.AddSingleton<IToolsRepository, ToolsRepository>();
                    services.AddSingleton<AppReliabilityAgentFactory>();
                    services.AddSingleton<IThreadRepository, InmemoryThreadRepository>();

                    services.AddDurableTaskWorker(builder =>
                    {
                        builder.AddTasks(r =>
                        {
                            DurableHelper.AddAllGeneratedTasks(r);
                        });
                        builder.UseDurableTaskScheduler(durableConnectionString);
                    });

                    services.AddDurableTaskClient(builder =>
                    {
                        builder.UseDurableTaskScheduler(durableConnectionString);
                    });

                })
                .Build();

            _durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
            _agentFactory = _host.Services.GetRequiredService<AppReliabilityAgentFactory>();
            _mockThreadRepository = _host.Services.GetRequiredService<IThreadRepository>();
        }

        public async Task DisposeAsync()
        {
            // TODO - cleanup doesnt work against emulator, following up with DTS team    
            await Helper.CleanupAllOrchestration<AppReliabilityAgentFactory>(_durableTaskClient);

            await _host.StopAsync();
            _host.Dispose();
        }

        public async Task InitializeAsync()
        {
            await _host.StartAsync();
            await Helper.CleanupAllOrchestration<AppReliabilityAgentFactory>(_durableTaskClient);
        }

        [Fact]
        public async Task Cleanup()
        {
            // Need to chat with DTS folks about the best way to do this. 
        }

        [Fact]
        public async Task UpdateHealthyApps()
        {
            // TODO AgenticLoggingChatClient is kind of broken, some logs are still duplicated

            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var input = new AppReliabilityInput { AppsInViolation = _testApps };
            string? instanceID = "";
            var guid = Guid.NewGuid();

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, guid);

                await Task.Delay(TimeSpan.FromSeconds(3));
                await Helper.DoApproval(
                    _durableTaskClient,
                    _mockThreadRepository,
                    guid,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                foreach (var app in _testApps)
                {
                    Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(app.ResourceId));
                }
            }
            catch (Grpc.Core.RpcException ex)
            {
                Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
            }
            catch (TaskCanceledException)
            {
                if (!string.IsNullOrEmpty(instanceID))
                {
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, new TerminateInstanceOptions { Output = "test cleanup", Recursive = true });
                }
                Assert.Fail("Orchestration timed out");
            }

        }

        [Fact]
        public async Task RollbackUnhealthyApp()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.ResourceId.EndsWith("app2")).ResourceId);

            var input = new AppReliabilityInput { AppsInViolation = _testApps };
            string? instanceID = "";
            var guid = Guid.NewGuid();

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, guid);
                await Helper.DoApproval(
                    _durableTaskClient,
                    _mockThreadRepository,
                    guid,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(_testApps[0].ResourceId));
                Assert.Equal(new Tuple<bool, bool, bool, int>(false, true, false, 2), _mockArmPlugin.GetAppReliability(_testApps[1].ResourceId));
                Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(_testApps[2].ResourceId));
                Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(_testApps[3].ResourceId));
                Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(_testApps[4].ResourceId));
            }
            catch (Grpc.Core.RpcException ex)
            {
                Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
            }
            catch (TaskCanceledException)
            {
                if (!string.IsNullOrEmpty(instanceID))
                {
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, "Test timeout");
                }

                Assert.Fail("Orchestration timed out");
            }

        }

        [Fact]
        public async Task AbortOnUnhealthy()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.ResourceId.EndsWith("app2")).ResourceId);


            var input = new AppReliabilityInput { AppsInViolation = _testApps };
            string? instanceID = "";
            var guid = Guid.NewGuid();

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, guid);

                await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new Microsoft.Extensions.AI.ChatMessage
                (
                    ChatRole.User,
                    "If any apps become unhealthy then complete the rollback for the unhealthy app, but then do not proceed with any more updates."
                ));

                await Helper.DoApproval(
                    _durableTaskClient,
                    _mockThreadRepository,
                    guid,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                var reliableConfig = new Tuple<bool, bool, bool, int>(true, true, true, 3);
                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal(ReliableApp.Configuration, _mockArmPlugin.GetAppReliability(_testApps[0].ResourceId));
                Assert.Equal(new Tuple<bool, bool, bool, int>(false, true, false, 2), _mockArmPlugin.GetAppReliability(_testApps[1].ResourceId));
                Assert.Equal(new Tuple<bool, bool, bool, int>(true, true, false, 1), _mockArmPlugin.GetAppReliability(_testApps[2].ResourceId));
                Assert.Equal(new Tuple<bool, bool, bool, int>(false, false, true, 2), _mockArmPlugin.GetAppReliability(_testApps[3].ResourceId));
                Assert.Equal(new Tuple<bool, bool, bool, int>(true, true, true, 3), _mockArmPlugin.GetAppReliability(_testApps[4].ResourceId));
            }
            catch (Grpc.Core.RpcException ex)
            {
                Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
            }
            catch (TaskCanceledException)
            {
                if (!string.IsNullOrEmpty(instanceID))
                {
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, "Test timeout");
                }

                Assert.Fail("Orchestration timed out");
            }

        }
    }
}

