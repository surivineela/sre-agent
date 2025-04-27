// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents;
using Agent.Runtime.ContextManagement;
using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;
using Agent.Core.Models.Api.v1;
using Thread = Agent.Core.Models.Api.v1.Thread;
using System.Text.Json;
using Agent.Plugins.Definitions;
using Agent.Plugins;

namespace Agent.Evals;

[TestClass]
[DoNotParallelize]
public sealed class ContainerAppsRemediationAgentV2Evals
{
    public TestContext TestContext { get; set; }

    private string? _llmDeploymentName;
    private BasicMockSetup? _mocks;
    private IHost? _host;
    private ChatConfiguration? _chatConfiguration;

    [TestInitialize]
    public Task TestInitializeAsync()
    {
        _mocks = new BasicMockSetup(DateTimeOffset.Parse("2025-02-24T01:00:00Z"), null);

        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();

        var services = builder.Services;
        services.AddMockServices(_mocks);
        services.AddSingleton<IToolsRepository, ToolsRepository>();
        services.AddSingleton<ContainerAppPluginDefinition>();
        services.AddSingleton<NSGRulePluginDefinition>();
        services.AddSingleton<GraphDBPluginDefinition>();
        services.AddSingleton<ChartPluginDefinition>();

        var sp = services.BuildServiceProvider();
        _host = builder.Build();

        IChatClient client = _host.Services.GetRequiredService<IChatClient>()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        _chatConfiguration = new(client, null);

        return _host.StartAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [TestMethod]
    public async Task AnalyzeHealthyApp()
    {
        Assert.IsNotNull(_chatConfiguration);
        Assert.IsNotNull(_llmDeploymentName);
        Assert.IsNotNull(_mocks);
        Assert.IsNotNull(_host);

        string appName = "healthy-app";
        string appResourceId = $"/subscriptions/test/resourceGroups/test/providers/Microsoft.App/containerApps/{appName}";
        string nsgResourceId = $"/subscriptions/test/resourceGroups/test/providers/Microsoft.Network/networkSecurityGroups/test-nsg";

        _mocks.NSGRulePlugin.ConfigureNSGDefaults(nsgResourceId);
        _mocks.ContainerAppPlugin.ConfigureDefaultApplication(appName, appResourceId);
        _mocks.ContainerAppPlugin.ConfigureSecurityRules(nsgResourceId, appResourceId);

        EvalInput evalInput = new(_chatConfiguration, TestContext, _llmDeploymentName)
        {
            GroundedContext = """
                ## Ground Truth
                1. Receive a request to investigate a container app.
                2. Check the health of the container app.
                3. If the container app is healthy, return a message indicating that the app is healthy.
                4. Check the NSG rules associated with the container app.
                5. If there are no problems with the NSG rules, return a message indicating that no problems were found.
                6. Check for any connected network dependent resources and verify if NSG rules are blocking connectivity to these resources if they exist.

                ## Expected Response Characteristics
                - The agent should keep the user informed as it performs each step of the analysis.
                - The agent should be brief, concise, and informative in its responses.
                """,

            ExampleResponse = $"""
                📝 I am starting to analyze network related issues for the container app **{appName}**.

                ✅ I've retrieved detailed information about **{appName}** including container configurations and ingress settings. The app is healthy.

                📝 Next, I will check the network security group (NSG) rules affecting this container app to identify any network restrictions or blocks.

                ✅ I have retrieved the NSG rules affecting the container app **{appName}**, and have not found any overly restrictive rules that could be causing connectivity issues.

                📝 Next, I will identify any connected network resources like databases or caches that may be relevant for network connectivity issues.

                ✅ I have not found any connected network resources that could be impacting the availability of the container app **{appName}**.
            """
        };

        var processor = await SetupConversationAsync($"{appResourceId} investigate this containerapp for network issues");

        await processor.RunLoopAsync();

        var threadRepository = _host.Services.GetRequiredService<IThreadRepository>();

        var chatHistory = await threadRepository.GetAgentChatHistoryAsync(Guid.Parse(processor.AgentContextId));
        var reasoningMessages = (await chatHistory.GetReasoningMessagesAsync(threadRepository))
            .Where(m => m.Role == ReasoningMessageRoleEnum.Assistant).ToList();
        var chatMessages = reasoningMessages.GetChatMessages();

        await evalInput.EvaluateAgentResponsesAsync(chatMessages);
    }

    private async Task<ReasoningLoopProcessor> SetupConversationAsync<T>(T inputData)
    {
        Assert.IsNotNull(_host);
        Assert.IsNotNull(_chatConfiguration);

        var threadRepository = _host.Services.GetRequiredService<IThreadRepository>();
        var instanceManagementRepository = _host.Services.GetRequiredService<IInstanceManagementRepository>();
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var outboundCommunicationService = _host.Services.GetRequiredService<IAgentOutboundCommunicationService>();
        var serviceProvider = _host.Services;

        var message = "Hello!";

        var now = DateTime.UtcNow;
        var startMessage = new Message(
            Guid.NewGuid(),
            now,
            new Author(Role.User, "user-default", "User"),
            message,
            false,
            new Posted(false)
        );

        var thread = new Thread(
            Id: Guid.NewGuid(),
            Title: "Test Thread",
            StartMessage: startMessage,
            LastMessage: startMessage,
            CreatedTimestamp: now,
            ModifiedTimestamp: now,
            Source: ThreadSource.Conversation
        );

        var agentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: thread.Id,
            AgentType: AgentTypeEnum.ContainerAppsRemediation,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null,
            InputDataSerialized: JsonSerializer.Serialize(inputData)
        );

        var startReasoningMessage = new ReasoningMessage(
            Id: Guid.NewGuid(),
            AgentContextId: agentContext.Id,
            Role: ReasoningMessageRoleEnum.Assistant,
            SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.Assistant, message)));

        var agentChatHistory = new AgentChatHistory(AgentContextId: agentContext.Id, ReasoningMessageIds: [startReasoningMessage.Id]);

        var assignment = new AgentContextInstanceAssignment(
            agentContext.Id.ToString(),
            thread.Id.ToString(),
            "instance-id",
            DateTimeOffset.MaxValue
        );

        await threadRepository.CreateThreadAsync(thread);
        await threadRepository.CreateAgentContextAsync(agentContext);
        await threadRepository.CreateAgentChatHistoryAsync(agentChatHistory);
        await threadRepository.AddMessageAsync(thread.Id, thread.StartMessage);
        await threadRepository.CreateReasoningMessageAsync(startReasoningMessage);

        await instanceManagementRepository.CreateAgentContextInstanceAssignmentAsync(assignment);
        await threadRepository.UpdateAgentContextAssignmentInfoAsync(agentContext.Id, thread.Id, assignment.InstanceId, assignment.Expires);

        return new ReasoningLoopProcessor(
            assignment,
            threadRepository,
            _chatConfiguration.ChatClient,
            loggerFactory,
            serviceProvider,
            outboundCommunicationService,
            5
        );
    }
}
