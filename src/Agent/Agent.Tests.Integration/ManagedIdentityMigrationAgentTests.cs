// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Interface;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
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

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class ManagedIdentityMigrationAgentTests : IAsyncLifetime
    {
        private TimeProvider _timeProvider;
        private ILogger _logger;
        private IHost _host;
        private DurableTaskClient _durableTaskClient;
        private ManagedIdentityMigrationAgentFactory _agentFactory;
        private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";

        private MockArmPlugin _mockArmPlugin;
        private MockMetricsPlugin _mockMetricsPlugin;
        private MockGithubWorkflowTriggerPlugin _mockGithubPlugin;
        private MockTimePlugin _mockTimePlugin;
        private MockMIConfigurationCheckPlugin _mockMIConfigurationCheckPlugin;
        private MockAppIdentityUpdatePlugin _mockAppIdentityUpdatePlugin;

        private List<AppMigrationStatus> _testApps = new List<AppMigrationStatus>
        {
            new AppMigrationStatus
            {
                Name = "app1",
                ResourceId = $"{BaseResourceId}/app1",
                UsesAzureSqlConnectionString = true,
                CurrentConnectionMethod = "Connection String"
            },
            new AppMigrationStatus
            {
                Name = "app2",
                ResourceId = $"{BaseResourceId}/app2",
                UsesAzureSqlConnectionString = true,
                CurrentConnectionMethod = "Connection String"
            }
        };

        public ManagedIdentityMigrationAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            var config = fixture.ConfigFixture;
            _logger = testOutputHelper.ToLogger<ILogger>();
            _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));

            var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(ManagedIdentityMigrationAgentTests)));

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

                    var durableConnectionString = "";
                    if (string.IsNullOrEmpty(durableConnectionString))
                    {
                        durableConnectionString = "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
                    }

                    services.AddSingleton(openAIClient);

                    services.AddChatClient(serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient(), ServiceLifetime.Singleton)
                        .UseAgenticLogging();

                    services.AddKeyedChatClient("function-invocation-enabled", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient(), ServiceLifetime.Singleton)
                        .UseAgenticLogging()
                        .UseDistributedCache(diskCache)
                        .UseFunctionInvocation();

                    // -- test specific
                    _mockArmPlugin = new MockArmPlugin(_timeProvider);
                    _mockMetricsPlugin = new MockMetricsPlugin(_timeProvider);
                    _mockGithubPlugin = new MockGithubWorkflowTriggerPlugin(_timeProvider);
                    _mockTimePlugin = new MockTimePlugin(_timeProvider);
                    _mockMIConfigurationCheckPlugin = new MockMIConfigurationCheckPlugin();
                    _mockAppIdentityUpdatePlugin = new MockAppIdentityUpdatePlugin(_mockMIConfigurationCheckPlugin);
                    _mockAppIdentityUpdatePlugin.ConfigureTestApps(_testApps);

                    services.AddSingleton<IArmPlugin>(_mockArmPlugin);
                    services.AddSingleton<IMetricsPlugin>(_mockMetricsPlugin);
                    services.AddSingleton<IGithubWorkflowTriggerPlugin>(_mockGithubPlugin);
                    services.AddSingleton<ITimePlugin>(_mockTimePlugin);
                    services.AddSingleton<IMIConfigurationCheckPlugin>(_mockMIConfigurationCheckPlugin);
                    services.AddSingleton<IAppIdentityUpdatePlugin>(_mockAppIdentityUpdatePlugin);
                    services.AddSingleton<IToolsRepository, ToolsRepository>();
                    services.AddSingleton<ManagedIdentityMigrationAgentFactory>();

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
            _agentFactory = _host.Services.GetRequiredService<ManagedIdentityMigrationAgentFactory>();
        }

        public async Task DisposeAsync()
        {
            await CleanupAsync();
            await _host.StopAsync();
            _host.Dispose();
        }

        public async Task InitializeAsync()
        {
            await _host.StartAsync();
            await CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            var query = new OrchestrationQuery
            {
                Statuses = [OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Pending]
            };

            var instances = _durableTaskClient.GetAllInstancesAsync(query);

            await foreach (var instance in instances.Where(x => x.Name == nameof(ManagedIdentityMigrationAgent)))
            {
                await _durableTaskClient.TerminateInstanceAsync(instance.InstanceId, new TerminateInstanceOptions { Output = "Test cleanup", Recursive = true });
                await _durableTaskClient.WaitForInstanceCompletionAsync(instance.InstanceId);
            }
        }

        private async Task DoApproval(string instanceID, CancellationTokenSource approvalSource)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), approvalSource.Token);
                var orchestrationMetadata = await _durableTaskClient.GetInstanceAsync(instanceID, getInputsAndOutputs: true);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                if (orchestrationMetadata.SerializedCustomStatus == null)
                {
                    continue;
                }

                var orchestrationStatus = orchestrationMetadata.ReadCustomStatusAs<string>();

                if (orchestrationStatus.StartsWith("Pending approval:"))
                {
                    var approvalId = orchestrationStatus.Split(":")[1];
                    var approvalStatus = new ApprovalStatus(approvalId, _timeProvider.GetUtcNow().DateTime, ApprovalDecision.Approved, _timeProvider.GetUtcNow().DateTime, "unit test", ProcessedTime: null);
                    await _durableTaskClient.RaiseEventAsync(approvalId, "ApprovalEvent", approvalStatus);
                    break;
                }
            }
        }

        [Fact]
        public async Task SuccessfullyMigrateAppsToManagedIdentity()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var input = new ManagedIdentityMigrationInput
            {
                AppsToMigrate = _testApps,
                message = string.Empty
            };
            string? instanceID = "";

            try
            {
                instanceID = await _agentFactory.StartOrchestration(input, Guid.NewGuid());
                await DoApproval(instanceID, tokenSource);

                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                // Verify that all apps have been migrated to Managed Identity
                foreach (var app in _testApps)
                {
                    Assert.Equal("Managed Identity", app.CurrentConnectionMethod);
                    Assert.False(app.UsesAzureSqlConnectionString);
                }

                // Verify that GitHub workflows were triggered and completed successfully
                Assert.True(_mockGithubPlugin.WorkflowRuns.All(r => r.Status == "completed" && r.Conclusion == "success"));

                // Verify that MI configuration check returns MI as the value for the app
                foreach (var app in _testApps)
                {
                    var miInfo = await _mockMIConfigurationCheckPlugin.GetManagedIdentityInfo(app.ResourceId);
                    Assert.True(miInfo.IsConnected);
                    Assert.Equal("Managed Identity", miInfo.Details);
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
    }
}

