// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Integration.Fixtures;
using Azure.AI.OpenAI;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.DurableTask.Client.AzureManaged;
using Xunit.Abstractions;
using Agent.Runtime.MetaAgent;
using Agent.Core.Models;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Tests.Integration.Helpers;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Newtonsoft.Json;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Agent.Runtime.Communication;
using Agent.Data.Repositories;
using Agent.Core.Interfaces;

namespace Agent.Tests.Integration;

[Collection(nameof(CombinedTestCollection))]
public class MetaAgentTests : IAsyncLifetime
{
    private readonly IHost _host;

    public MetaAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var config = fixture.ConfigFixture;
                services.AddLogging(builder =>
                {
                    builder.AddXUnit(testOutputHelper)
                        .SetMinimumLevel(LogLevel.Trace);
                });

                var openAISettings = config.AzureSettings.OpenAI;
                var openAIClient = new AzureOpenAIClient(new Uri(openAISettings.Endpoint), new System.ClientModel.ApiKeyCredential(config.AzureSettings.OpenAI.ApiKey));

                var cacheDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..\\..\\..", "ChatCompletionCache", nameof(MetaAgentTests)));
                var diskCache = new TestCachingChatClientBuilderExtensions.DiskCache(cacheDir);

                var durableConnectionString = "";// "Endpoint=https://scheduler1-cqczc8d2dhhj.northcentralus.durabletask.io;TaskHub=taskhub1;Authentication=DefaultAzure";
                                                 //var durableConnectionString = "Endpoint=https://scheduler1-enekctgzbtbj.northcentralus.durabletask.io;TaskHub=taskhub1;Authentication=DefaultAzure";//"Endpoint=https://sanmeht-dts-a0bgg6eafjd2.westus2.durabletask.io;Authentication=DefaultAzure;TaskHub=sanmeht-dts-hub";
                if (string.IsNullOrEmpty(durableConnectionString))
                {
                    durableConnectionString = "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
                }

                services.AddSingleton(openAIClient);

                services.AddChatClient(serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>()
                    .AsChatClient(openAISettings.LLMDeploymentName), ServiceLifetime.Singleton)
                    .UseAgenticLogging()
                    .UseDistributedCache(diskCache);

                // if we want, we can have different chat clients, some with function invocation enabled
                services.AddKeyedChatClient("function-invocation-enabled", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(openAISettings.LLMDeploymentName), ServiceLifetime.Singleton)
                    .UseAgenticLogging()
                    .UseDistributedCache(diskCache)
                    .UseFunctionInvocation();

                // -- test specific
                var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2025-02-24T01:00:00Z"));
                var mockArmPlugin = new MockArmPlugin(timeProvider);
                // mockArmPlugin.ConfigureTlsStatus(testApps.ToDictionary(x => x.ResourceId));
                var mockMetricsPlugin = new MockMetricsPlugin(timeProvider);
                var mockGithubPlugin = new MockGithubWorkflowTriggerPlugin(timeProvider);
                var mockTimePlugin = new MockTimePlugin(timeProvider);
                var mockMIConfigurationCheckPlugin = new MockMIConfigurationCheckPlugin();
                var mockAppIdentityUpdatePlugin = new MockAppIdentityUpdatePlugin(mockMIConfigurationCheckPlugin);

                services.AddSingleton<IArmPlugin>(mockArmPlugin);
                services.AddSingleton<IMetricsPlugin>(mockMetricsPlugin);
                services.AddSingleton<IGithubWorkflowTriggerPlugin>(mockGithubPlugin);
                services.AddSingleton<ITimePlugin>(mockTimePlugin);
                services.AddSingleton<IMIConfigurationCheckPlugin>(mockMIConfigurationCheckPlugin);
                services.AddSingleton<IAppIdentityUpdatePlugin>(mockAppIdentityUpdatePlugin);
                services.AddSingleton<IToolsRepository, ToolsRepository>();
                services.AddSingleton<ManagedIdentityMigrationAgentFactory>();
                services.AddSingleton<TlsBestPracticeAgentFactory>();
                services.AddSingleton<AppReliabilityAgentFactory>();
                services.AddSingleton<Runtime.MetaAgent.IAgent, MetaAgent>();
                services.AddSingleton<IMetaAgentManagedIdentityMigrationPlugin, ManagedIdentityMigrationPlugin>();
                services.AddSingleton<IMetaAgentTlsBestPracticesPlugin, TlsBestPracticesPlugin>();
                services.AddSingleton<IMetaAgentAppReliabilityPlugin, AppReliabilityPlugin>();
                services.AddSingleton<IMetaAgentVmRdpInvestigatorPlugin, VmRdpInvestigatorPlugin>();
                services.AddSingleton<IMetaAgentFunctionAppConnectivityPlugin, FunctionAppConnectivityPlugin>();
                services.AddSingleton<IMetaAgentSqlDbQueryPerfPlugin, SqlDbQueryPerfPlugin>();
                services.AddSingleton<TimeProvider>(timeProvider);
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
    }

    private const string BaseResourceId = "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourceGroups/my-resource-group/providers/Microsoft.Web/sites";
    private readonly List<TlsStatus> _testApps = new List<TlsStatus>
        {
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app1", ResourceId : $"{BaseResourceId}/app1", Location:"eastus" ),
            new TlsStatus ( MinimumTlsVersion : "1.0", Name : "app2", ResourceId : $"{BaseResourceId}/app2", Location:"eastus" ),
        };
    private readonly List<AppReliability> _testApps2 = new List<AppReliability>
    {
        new AppReliability ($"{BaseResourceId}/app1", false, false, false, 1),
        new AppReliability ($"{BaseResourceId}/app2", false, true, false, 2)
    };

    [Fact]
    public async Task StartTlsBestPracticeAgent()
    {

        var message = new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, "hello", "User"), $"Help me to apply tls best practice. Here are my apps: {JsonSerializer.Serialize(_testApps)}, I want to upgrade TLS version to 1.2");
        var threadGuid = Guid.NewGuid();

        var chatMessage = new ChatMessage(ChatRole.User, message.Text);
        var agentContext = new AgentContext(Guid.NewGuid(), threadGuid, AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(chatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        // generate threadId for this background task
        var threadId = threadGuid.ToString();

        var metaAgent = _host.Services.GetRequiredService<MetaAgent>();
        var durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        var threadRepository = _host.Services.GetRequiredService<IThreadRepository>();
        var resp = await metaAgent.ProcessUserMessageAsync(agentContext, agentChatHistory);

        var tlsOrche = (await durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
        {
            FetchInputsAndOutputs = true
        }).ToListAsync()).Single();
        var input = JsonSerializer.Deserialize<TlsBestPracticesAgentInput>(tlsOrche.SerializedInput.ThrowIfNull()).ThrowIfNull().Input;

        Assert.True(input.AppsInViolation.SequenceEqual(_testApps));
        Assert.Equal("1.2", input.DesiredVersion);

        await Helper.DoApproval(
            durableTaskClient,
            threadRepository,
            Guid.Parse(threadId),
            cancellationToken: default);

        var orchestrationMetadata = await durableTaskClient.WaitForInstanceCompletionAsync(
            tlsOrche.InstanceId,
            getInputsAndOutputs: true);
    }

    [Fact]
    public async Task StartAppReliabilityAgent()
    {

        var message = new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, "hello", "User"), $"Help me to apply best practices for app reliability. Here are my apps: {JsonSerializer.Serialize(_testApps2)}, I want to upgrade the AlwaysOn to true, HealthCheck to true, AutoHeal to true, and NumberOfWorkers to 3");
        // generate threadId for this background task
        var threadId = Guid.NewGuid(); ;
        var chatMessage = new ChatMessage(ChatRole.User, message.Text);
        var agentContext = new AgentContext(Guid.NewGuid(), threadId, AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(chatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        var metaAgent = _host.Services.GetRequiredService<MetaAgent>();
        var durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        var threadRepository = _host.Services.GetRequiredService<IThreadRepository>();

        var resp = await metaAgent.ProcessUserMessageAsync(agentContext, agentChatHistory);

        var relOrche = (await durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
        {
            FetchInputsAndOutputs = true
        }).ToListAsync()).Single();
        var input = JsonSerializer.Deserialize<AppReliabilityAgentInput>(relOrche.SerializedInput.ThrowIfNull()).ThrowIfNull().Input;

        Assert.True(input.AppsInViolation.SequenceEqual(_testApps2));

        await Helper.DoApproval(
            durableTaskClient,
            threadRepository,
            threadId,
            cancellationToken: default);

        var orchestrationMetadata = await durableTaskClient.WaitForInstanceCompletionAsync(
            relOrche.InstanceId,
            getInputsAndOutputs: true);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Cleanup();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Cleanup();
    }

    private async Task Cleanup()
    {
        var durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        await Helper.CleanupAllOrchestration<TlsBestPracticesAgent>(durableTaskClient);
        await Helper.CleanupAllOrchestration<AppReliabilityAgent>(durableTaskClient);
    }
}

