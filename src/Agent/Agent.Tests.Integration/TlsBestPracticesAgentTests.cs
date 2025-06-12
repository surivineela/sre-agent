// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins.Interface;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Agent.Tests.Integration.Helpers;
using Azure.AI.OpenAI;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class TlsBestPracticesAgentTests : IAsyncLifetime
    {
        private ILogger _logger;
        private IHost _host;
        private DurableTaskClient _durableTaskClient;
        private TlsBestPracticeAgentFactory _agentFactory;
        private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";
        private BasicMockSetup _mocks;
        private IThreadRepository _mockThreadRepository;

        private List<TlsStatus> _testApps = new List<TlsStatus>
        {
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app1", ResourceId : $"{BaseResourceId}/app1", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app2", ResourceId : $"{BaseResourceId}/app2", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app3", ResourceId : $"{BaseResourceId}/app3", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app4", ResourceId : $"{BaseResourceId}/app4", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app5", ResourceId : $"{BaseResourceId}/app5", Location:"eastus" ),
        };

        public TlsBestPracticesAgentTests(ITestOutputHelper testOutputHelper)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            var services = builder.Services;

            builder.LoadAppSettings();
            builder.ValidateAndRegisterAppSettings<AppSettings>();
            builder.ConfigureDurable();
            builder.Services.ConfigureAzureOpenAIClient();

            _logger = testOutputHelper.ToLogger<ILogger>();
            _mocks = new BasicMockSetup(DateTimeOffset.Parse("2025-02-24T01:00:00Z"), _logger);
            _mocks.ArmPlugin.ConfigureTlsStatus(_testApps.ToDictionary(x => x.ResourceId));
            services.AddMockServices(_mocks);

            var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(TlsBestPracticesAgentTests)));
            var diskCache = new TestCachingChatClientBuilderExtensions.DiskCache(cacheDir);

            services.AddLogging(builder =>
            {
                builder.AddXUnit(testOutputHelper)
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter("Microsoft.DurableTask", LogLevel.Information)
                    .AddFilter("ModelContextProtocol", LogLevel.Error);
            });

            string llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];

            services.AddChatClient(serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient(), ServiceLifetime.Singleton)
                .UseAgenticLogging()
                .UseDistributedCache(diskCache);

            // if we want, we can have different chat clients, some with function invocation enabled
            services.AddKeyedChatClient("function-invocation-enabled", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient(), ServiceLifetime.Singleton)
                .UseAgenticLogging()
                .UseDistributedCache(diskCache)
                .UseFunctionInvocation();

            services.AddSingleton<IThreadOrchestrationManager, InMemoryThreadOrchestrationManager>();
            services.AddSingleton<IThreadRepository, InMemoryThreadRepository>();
            services.AddSingleton<IInstanceManagementRepository, InMemoryInstanceManagementRepository>();
            services.AddSingleton<ThreadService>();
            services.AddSingleton<SinkService>();
            services.AddSingleton(sp => new Mock<IPostToTeamsPlugin>().Object);

            services.AddSingleton<IToolsRepository, ToolsRepository>();
            services.AddSingleton<TlsBestPracticeAgentFactory>();

            TlsTestHelpers.AddPluginDefinitionsForGenericSubAgent(services);

            _host = builder.Build();

            _durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
            _agentFactory = _host.Services.GetRequiredService<TlsBestPracticeAgentFactory>();
            _mockThreadRepository = _host.Services.GetRequiredService<IThreadRepository>();
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
                var threadId = Guid.NewGuid();
                instanceID = await _agentFactory.StartOrchestration(input, threadId);

                var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                    _durableTaskClient,
                    instanceID,
                    _mockThreadRepository,
                    threadId,
                    _logger,
                    tokenSource.Token,
                    _mocks.TimeProvider);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                foreach (var app in _testApps)
                {
                    Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(app.ResourceId));
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

            _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps[1].ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                var threadId = Guid.NewGuid();
                instanceID = await _agentFactory.StartOrchestration(input, threadId);
                var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                    _durableTaskClient,
                    instanceID,
                    _mockThreadRepository,
                    threadId,
                    _logger,
                    tokenSource.Token,
                    _mocks.TimeProvider);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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

            _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.Name == "app2").ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                var threadId = Guid.NewGuid();
                instanceID = await _agentFactory.StartOrchestration(input, threadId);

                await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                (
                    ChatRole.User,
                    "If any apps become unhealthy then complete the rollback for the unhealthy app, but then do not proceed with any more updates."
                ));

                var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                    _durableTaskClient,
                    instanceID,
                    _mockThreadRepository,
                    threadId,
                    _logger,
                    tokenSource.Token,
                    _mocks.TimeProvider);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.0", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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

            _mocks.MetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.Name == "app2").ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                var threadId = Guid.NewGuid();
                instanceID = await _agentFactory.StartOrchestration(input, threadId);

                await _durableTaskClient.RaiseEventAsync(instanceID, "NewChatMessage", new ChatMessage
                (
                    ChatRole.User,
                    "If any apps become unhealthy, I want you to ask me for confirmation on whether I want to proceed with the rollback, or leave the app as is. Specifically use the word confirmation when you request it."
                ));

                bool shouldCheckForRollbackMessage = true;
                var orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
                    _durableTaskClient,
                    instanceID,
                    _mockThreadRepository,
                    threadId,
                    _logger,
                    tokenSource.Token,
                    _mocks.TimeProvider,
                    customAction: async () =>
                    {
                        var last = _mocks.CommunicationService.Messages.LastOrDefault();

                        // Wait for the model to ask us whether it should perform a rollback.
                        if (shouldCheckForRollbackMessage
                            && last != null
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
                            shouldCheckForRollbackMessage = false;
                        }
                    });

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[0].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[1].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[2].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[3].ResourceId));
                Assert.Equal("1.2", _mocks.ArmPlugin.GetTlsStatus(_testApps[4].ResourceId));
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

