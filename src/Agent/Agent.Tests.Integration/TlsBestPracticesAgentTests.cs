using System.Net;
using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Integration.Fixtures;
using Azure.AI.OpenAI;
using Azure.Identity;
using DurableTask.Core;
using Microsoft.DurableTask;
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
using static System.Net.WebRequestMethods;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class TlsBestPracticesAgentTests : IAsyncLifetime
    {
        private TimeProvider _timeProvider;
        private ILogger _logger;
        private IHost _host;
        private DurableTaskClient _durableTaskClient;
        private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";

        private MockArmPlugin _mockArmPlugin;
        private MockMetricsPlugin _mockMetricsPlugin;

        private List<TlsStatus> _testApps = new List<TlsStatus>
        {
            new TlsStatus { MinimumTlsVersion = "1.0", Name = "app1", ResourceId = $"{BaseResourceId}/app1" },
            new TlsStatus { MinimumTlsVersion = "1.0", Name = "app2", ResourceId = $"{BaseResourceId}/app2" },
            new TlsStatus { MinimumTlsVersion = "1.0", Name = "app3", ResourceId = $"{BaseResourceId}/app3" },
            new TlsStatus { MinimumTlsVersion = "1.0", Name = "app4", ResourceId = $"{BaseResourceId}/app4" },
            new TlsStatus { MinimumTlsVersion = "1.0", Name = "app5", ResourceId = $"{BaseResourceId}/app5" },
        };

        public TlsBestPracticesAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            var config = fixture.ConfigFixture;
            _logger = testOutputHelper.ToLogger<ILogger>();
            _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));

            var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(TlsBestPracticesAgentTests)));

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddXUnit(testOutputHelper)
                            .SetMinimumLevel(LogLevel.Trace);
                    });

                    var openAISettings = config.AzureSettings.OpenAI;
                    var openAIClient = new AzureOpenAIClient(new Uri(openAISettings.Endpoint), new DefaultAzureCredential());

                    var diskCache = new TestCachingChatClientBuilderExtensions.DiskCache(cacheDir);

                    var durableConnectionString = config.AzureSettings.DurableTaskScheduler.ConnectionString;
                    if (string.IsNullOrEmpty(durableConnectionString))
                    {
                        durableConnectionString = "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
                    }
                    

                    services.AddSingleton(openAIClient);

                    services.AddChatClient(serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(openAISettings.DeploymentName))                        
                        .UseAgenticLogging()
                        .UseDistributedCache(diskCache)
                        .UseFunctionInvocation();

                    services.AddKeyedChatClient("no-function-invocation", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(openAISettings.DeploymentName))
                        .UseAgenticLogging()
                        .UseDistributedCache(diskCache);

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

                    // -- test specific

                    _mockArmPlugin = new MockArmPlugin(_timeProvider, _testApps);                    
                    _mockMetricsPlugin = new MockMetricsPlugin(_timeProvider);

                    services.AddSingleton<IArmPlugin>(_mockArmPlugin);
                    services.AddSingleton<IMetricsPlugin>(_mockMetricsPlugin);
                    services.AddSingleton<TlsBestPracticesAgentTools>();

                    
                })
                .Build();

            _durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        }

        public async Task DisposeAsync()
        {
            // TODO - cleanup doesnt work against emulator, following up with DTS team
            // in meantime might need to terminate orphan orchestrations in dashboard
            //await CleanupAsync();

            await _host.StopAsync();
            _host.Dispose();
        }

        public async Task InitializeAsync()
        {
            await _host.StartAsync();
            //await CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            // todo - this might cause problems once we have tests running in parallel

            var query = new OrchestrationQuery
            {
                Statuses = [OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Pending]
            };

            var instances = _durableTaskClient.GetAllInstancesAsync(query);

            await foreach (var instance in instances.Where(x => x.Name == nameof(TlsBestPracticesAgent)))
            {
                await _durableTaskClient.TerminateInstanceAsync(instance.InstanceId, "Test cleanup");
                await _durableTaskClient.WaitForInstanceCompletionAsync(instance.InstanceId);
            }
        }


        [Fact]
        public async Task UpdateHealthyApps()
        {
            // TODO AgenticLoggingChatClient is kind of broken, some logs are still duplicated

            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(input);
                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: true, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);

                foreach(var app in _testApps)
                {
                    Assert.Equal("1.2", app.MinimumTlsVersion);
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
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, "Test timeout");
                }
                Assert.Fail("Orchestration timed out");
            }

        }


        [Fact]
        public async Task RollbackUnhealthyApp()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

            _mockMetricsPlugin.UnhealthyResourceIds.Add(_testApps.Single(x => x.Name == "app2").ResourceId);

            var input = new TlsBestPracticesInput { AppsInViolation = _testApps, DesiredVersion = "1.2", };
            string? instanceID = "";

            try
            {
                instanceID = await _durableTaskClient.ScheduleNewTlsBestPracticesAgentInstanceAsync(input);
                var orchestrationMetadata = await _durableTaskClient.WaitForInstanceCompletionAsync(instanceID, getInputsAndOutputs: false, tokenSource.Token);
                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                Assert.True(orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed);
                Assert.Equal("1.2", _testApps[0].MinimumTlsVersion);
                Assert.Equal("1.0", _testApps[1].MinimumTlsVersion);
                Assert.Equal("1.2", _testApps[2].MinimumTlsVersion);
                Assert.Equal("1.2", _testApps[3].MinimumTlsVersion);
                Assert.Equal("1.2", _testApps[4].MinimumTlsVersion);
            }
            catch (Grpc.Core.RpcException ex)
            {
                Assert.Fail($"Make sure you have the DTS emulator running (run-durable-emulator.ps1) or your appsettings.development.json has a valid Durable Task Scheduler connection string.{Environment.NewLine} {ex}");
            }
            catch (TaskCanceledException)
            {
                if(!string.IsNullOrEmpty(instanceID))
                {
                    await _durableTaskClient.TerminateInstanceAsync(instanceID, "Test timeout");
                }
                
                Assert.Fail("Orchestration timed out");
            }

        }
    }
}
