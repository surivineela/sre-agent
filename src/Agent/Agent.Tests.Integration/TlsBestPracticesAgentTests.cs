// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Mocks;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Integration.Fixtures;
using Agent.Tests.Integration.Helpers;
using Agent.Tests.Integration.Mocks;
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
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class TlsBestPracticesAgentTests : IAsyncLifetime
    {
        private TimeProvider _timeProvider;
        private ILogger _logger;
        private IHost _host;
        private DurableTaskClient _durableTaskClient;
        private TlsBestPracticeAgentFactory _agentFactory;
        private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";

        private MockApprovalPlugin _mockApprovalPlugin;
        private MockRecordActionsPlugin _mockRecordActionsPlugin;
        private MockArmPlugin _mockArmPlugin;
        private MockMetricsPlugin _mockMetricsPlugin;
        private MockGithubWorkflowTriggerPlugin _mockGithubPlugin;
        private MockTimePlugin _mockTimePlugin;
        private MockMIConfigurationCheckPlugin _mockMIConfigurationCheckPlugin;
        private MockAppIdentityUpdatePlugin _mockAppIdentityUpdatePlugin;
        private MockCommunicationService _mockCommunicationService;

        private List<TlsStatus> _testApps = new List<TlsStatus>
        {
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app1", ResourceId : $"{BaseResourceId}/app1", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app2", ResourceId : $"{BaseResourceId}/app2", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app3", ResourceId : $"{BaseResourceId}/app3", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app4", ResourceId : $"{BaseResourceId}/app4", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app5", ResourceId : $"{BaseResourceId}/app5", Location:"eastus" ),
        };

        public TlsBestPracticesAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            var config = fixture.ConfigFixture;
            _logger = testOutputHelper.ToLogger<ILogger>();
            _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));

            var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(TlsBestPracticesAgentTests)));

            var services = fixture.ConfigFixture.Builder.Services;

            services.AddLogging(builder =>
            {
                builder.AddXUnit(testOutputHelper)
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter("Microsoft.DurableTask", LogLevel.Information)
                    .AddFilter("ModelContextProtocol", LogLevel.Error);
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
            _mockApprovalPlugin = new MockApprovalPlugin();
            _mockArmPlugin = new MockArmPlugin(_timeProvider, _mockApprovalPlugin);
            _mockArmPlugin.ConfigureTlsStatus(_testApps.ToDictionary(x => x.ResourceId));
            _mockMetricsPlugin = new MockMetricsPlugin(_timeProvider);
            _mockGithubPlugin = new MockGithubWorkflowTriggerPlugin(_timeProvider);
            _mockTimePlugin = new MockTimePlugin(_timeProvider);
            _mockMIConfigurationCheckPlugin = new MockMIConfigurationCheckPlugin();
            _mockAppIdentityUpdatePlugin = new MockAppIdentityUpdatePlugin(_mockMIConfigurationCheckPlugin);
            _mockCommunicationService = new MockCommunicationService(testOutputHelper.ToLogger<MockCommunicationService>());
            _mockRecordActionsPlugin = new MockRecordActionsPlugin(_timeProvider, testOutputHelper.ToLogger<MockRecordActionsPlugin>());

            services.AddSingleton<IContainerImagePullFailurePlugin>(new MockContainerImagePullFailurePlugin());
            services.AddSingleton<IAzureSupportCenterPlugin>(new MockAzureSupportCenterPlugin());
            services.AddSingleton<IReliabilityPlugin>(new MockReliabilityPlugin());
            services.AddSingleton<IGithubIssuePlugin>(new MockGithubIssuePlugin());
            services.AddSingleton<IKubePlugin>(new MockKubePlugin());
            services.AddSingleton<IGrafanaPlugin>(new MockGrafanaPlugin());
            services.AddSingleton<IGraphDBPlugin>(new MockGraphDBPlugin());
            services.AddSingleton<IContainerAppPlugin>(new MockContainerAppPlugin());
            services.AddSingleton<IThreadOrchestrationManager, InMemoryThreadOrchestrationManager>();
            services.AddSingleton<TimeProvider>(_timeProvider);
            services.AddSingleton<IRemediationPlugin, MockRemediationPlugin>();
            services.AddSingleton<IApprovalPlugin>(_mockApprovalPlugin);
            services.AddSingleton<IRecordActionsPlugin>(_mockRecordActionsPlugin);
            services.AddSingleton<IArmPlugin>(_mockArmPlugin);
            services.AddSingleton<IMetricsPlugin>(_mockMetricsPlugin);
            services.AddSingleton<IGithubWorkflowTriggerPlugin>(_mockGithubPlugin);
            services.AddSingleton<ITimePlugin>(_mockTimePlugin);
            services.AddSingleton<IChartPlugin, ChartPlugin>();
            services.AddSingleton<IMIConfigurationCheckPlugin>(_mockMIConfigurationCheckPlugin);
            services.AddSingleton<IAppIdentityUpdatePlugin>(_mockAppIdentityUpdatePlugin);
            services.AddSingleton<ToolsRepository>();
            services.AddSingleton<McpToolsRepository>();
            services.AddSingleton<ManagedIdentityMigrationAgentFactory>();
            services.AddSingleton<MCPMetaAgent>();
            services.AddSingleton<TlsBestPracticeAgentFactory>();
            services.AddSingleton<IAgentOutboundCommunicationService>(_mockCommunicationService);

            services.AddHostedService<MCPMetaAgentManagementService>();

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

            var sp = services.BuildServiceProvider();

            _host = fixture.ConfigFixture.Builder.Build();

            _durableTaskClient = sp.GetRequiredService<DurableTaskClient>();
            _agentFactory = sp.GetRequiredService<TlsBestPracticeAgentFactory>();
        }

        public async Task DisposeAsync()
        {
            // TODO - cleanup doesnt work against emulator, following up with DTS team    
            await Helper.CleanupAllOrchestration<TlsBestPracticesAgent>(_durableTaskClient);

            await _host.StopAsync();
            _host.Dispose();
        }

        public async Task InitializeAsync()
        {
            await _host.StartAsync();
            await Helper.CleanupAllOrchestration<TlsBestPracticesAgent>(_durableTaskClient);
        }

        [Fact]
        public async Task UpdateHealthyApps()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, new ThreadContext(Guid.NewGuid(), Core.Helpers.AgentTypeEnum.DurableAgent));
                await Helper.DoApproval(
                    _durableTaskClient,
                    _timeProvider,
                    instanceID,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                foreach (var app in _testApps)
                {
                    Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(app.ResourceId));
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

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps[1].ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, new ThreadContext(Guid.NewGuid(), Core.Helpers.AgentTypeEnum.DurableAgent));
                await Helper.DoApproval(
                    _durableTaskClient,
                    _timeProvider,
                    instanceID,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.0", _mockArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.Name == "app2").ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, new ThreadContext(Guid.NewGuid(), Core.Helpers.AgentTypeEnum.DurableAgent));

                await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                (
                    ChatRole.User,
                    "If any apps become unhealthy then complete the rollback for the unhealthy app, but then do not proceed with any more updates."
                ));

                await Helper.DoApproval(
                    _durableTaskClient,
                    _timeProvider,
                    instanceID,
                    tokenSource.Token);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.0", _mockArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.0", _mockArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.0", _mockArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.0", _mockArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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
        public async Task AskForConfirmationOnRollback()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.Name == "app2").ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, new ThreadContext(Guid.NewGuid(), Core.Helpers.AgentTypeEnum.DurableAgent));

                await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                (
                    ChatRole.User,
                    "If any apps become unhealthy, I want you to ask me for confirmation on whether I want to proceed with the rollback, or leave the app as is. Specifically use the word confirmation when you request it."
                ));

                await Helper.DoApproval(
                    _durableTaskClient,
                    _timeProvider,
                    instanceID,
                    tokenSource.Token);

                OrchestrationMetadata? orchestrationMetadata = null;

                while (true)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), tokenSource.Token);

                    var last = _mockCommunicationService.Messages.Last();

                    // Wait for the model to ask us whether it should perform a rollback.
                    if (last != null
                        && last.Contains("back", StringComparison.InvariantCultureIgnoreCase)
                        && last.Contains("confirm", StringComparison.InvariantCultureIgnoreCase)
                        && last.Contains("?"))
                    {
                        // simulate the user taking a while to respond.
                        await Task.Delay(TimeSpan.FromSeconds(5));

                        await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                        (
                            ChatRole.User,
                            "I checked the app myself, a rollback is not necessary. You can leave the app as is and proceed."
                        ));
                        break;
                    }

                    orchestrationMetadata = await _durableTaskClient.GetInstanceAsync(instanceID, tokenSource.Token);
                    if (orchestrationMetadata.IsCompleted)
                    {
                        Assert.Fail("Orchestration completed before we could respond to the rollback confirmation.");
                    }
                }

                orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.2", _mockArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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

