using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Evals;

[DoNotParallelize]
[TestClass]
public class TlsHandoffEvals
{
    public TestContext TestContext { get; set; }
    private ChatConfiguration? _chatConfiguration;
    private IHost? _host;

    private static int _iterationCount = 1;
    private string? _llmDeploymentName;

    private E2EMockSetup _mocks;
    private DurableTaskClient _durableTaskClient;
    private ThreadManagementService _threadManager;
    private IThreadRepository _threadRepo;

    static TlsHandoffEvals()
    {
        _iterationCount = TestHelpers.GetIterationCount(defaultValue: _iterationCount);
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();
        builder.ConfigureDurable();

        _mocks = new E2EMockSetup(DateTimeOffset.Parse("2025-02-24T01:00:00Z"), graphName: "gsimpleWebapps", logger: null);
        builder.Services.AddServices(_mocks);

        builder.Services.AddPluginDefinitionsForGenericSubAgent();
        builder.Services.AddSingleton<TlsBestPracticesPlugin>();
        builder.Services.AddSingleton<TlsBestPracticeAgentFactory>();
        builder.Services.AddSingleton<IAgentsFactory>(sp =>
        {
            return MetaAgentMock.GetMockedThirdPartAgentsFactory(
                tlsBestPracticesPlugin: sp.GetRequiredService<TlsBestPracticesPlugin>(),
                graphDBPlugin: sp.GetRequiredService<GraphDBPlugin>()
                );
        });

        var agentModeString = builder.Configuration.GetSection("AppSettings:Core:Azure:Action:Mode").Get<string>();

        // This block is correct for conditionally registering the configurator
        if (string.Equals(agentModeString, "ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, ReadOnlyAgentModeConfigurator<AgentContext>>();
        }
        else // Assuming this is your default/full access mode
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, DefaultAgentModeConfigurator<AgentContext>>();
        }

        builder.Services.AddSingleton<IReasoningLoopManager, ReasoningLoopManager>();
        builder.Services.AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>();
        builder.Services.AddSingleton(sp =>
        {
            return new ToolFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<ToolFactory<AgentContext>>>(),
                serviceProvider: sp,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true));
        });

        builder.Services.AddSingleton<IToolFactory<AgentContext>, ToolFactory<AgentContext>>(sp =>
        {
            return sp.GetRequiredService<ToolFactory<AgentContext>>();
        });

        builder.Services.AddSingleton<IAgentFactory<AgentContext>, AgentFactory<AgentContext>>(sp =>
        {
            var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<AgentContext>>();
            return new AgentFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                toolFactory: sp.GetRequiredService<IToolFactory<AgentContext>>(),
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                commonToolsYamlDirectory: null
            );
        });

        builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();

        _host = builder.Build();

        _threadManager = _host.Services.GetRequiredService<ThreadManagementService>();
        _threadRepo = _host.Services.GetRequiredService<IThreadRepository>();
        _durableTaskClient = _host.Services.GetRequiredService<DurableTaskClient>();
        await SetupArmMockForTls();

        var evalClient = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(evalClient);

        await _host.StartAsync();
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
    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    private async Task SetupArmMockForTls()
    {
        // read the TLS configuration from the graph and configure the arm plugin mock with those details
        var graphDBPlugin = _host.Services.GetRequiredService<GraphDBPlugin>();
        var webApps = await graphDBPlugin.SearchResourceAsync("pbatum-sre-web", "microsoft.web/sites");
        var tlsStatus = new List<TlsStatus>();
        foreach (var webApp in webApps)
        {
            var properties = await graphDBPlugin.GetResourceDetailedProperties(webApp.ResourceId);
            tlsStatus.Add(new TlsStatus(webApp.ResourceId, webApp.ResourceName, webApp.Location, (string)properties["minTlsVersion"]));
        }
        _mocks.BasicMocks.ArmPlugin.ConfigureTlsStatus(tlsStatus.ToDictionary(x => x.ResourceId));
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task TlsHandoff_MetaAgentCanFindTheAppsToHandoff(string testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(2));

        var userMsg = "help me update my web apps that are accepting older TLS versions to accept only TLS 1.3 and above";
        OrchestrationMetadata? orchestrationMetadata = null;

        try
        {
            var startMessageRequest = new CreateMessageRequest(userMsg, "testUser", "Test User");
            var thread = await _threadManager.CreateUserInitiatedThread(new CreateThreadRequest(startMessageRequest));

            OrchestrationState orchestrationState = await _threadRepo.WaitForSubAgentAssignment(thread.Id, tokenSource.Token);
            orchestrationMetadata = await _durableTaskClient.GetInstanceAsync(orchestrationState.OrchestrationInstanceId, true, tokenSource.Token);

            var agentInput = orchestrationMetadata.ReadInputAs<TlsBestPracticesAgentInput>();

            Assert.IsTrue(agentInput.Input.DesiredVersion == "1.3", $"The agent input should be 1.3, but it is {agentInput.Input.DesiredVersion}");
            Assert.IsTrue(agentInput.Input.AppsInViolation.Count == 1, $"The agent input should have only one app in violation, but it has {agentInput.Input.AppsInViolation.Count}");
            Assert.IsTrue(agentInput.Input.AppsInViolation[0].ResourceId == "/subscriptions/29e3378b-0aaf-45da-b3c6-6fd0eea164e4/resourcegroups/pbatum-sre-web-eas-lin/providers/microsoft.web/sites/pbatum-sre-web-eas3", $"The agent input should have the app pbatum-sre-web-eas3 in violation, but it has {agentInput.Input.AppsInViolation[0].ResourceId}");
        }
        finally
        {
            // No need to complete the update.
            if (orchestrationMetadata != null)
            {
                await _durableTaskClient.TerminateInstanceAsync(orchestrationMetadata.InstanceId);
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task TlsHandoff_MetaAgentIsAwareOfTaskCompletion(string testRunGuid)
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMinutes(5));

        var userMsg = "can you help me update pbatum-sre-web-eas3 to accept only TLS 1.2 and above?";
        var followupMsg = "was the update successful?";

        string groundedContext = """
            ## Ground Truth:
            1. The user asks the agent to help them update their application pbatum-sre-web-eas3 to accept only TLS 1.2 and above.
            2. The agent handles the update process, subject to an approval by the user.
            3. The agent sends a summary when it completes the task.
            4. When the user asks if the update was successful, the agent is expected to know this and should answer accordingly. Its response should not be ambigious.
            """;

        var exampleResponse = $"""
            The web app pbatum-sre-web-eas3 was updated to accept a minimum TLS version of 1.2.
            """;

        var startMessageRequest = new CreateMessageRequest(userMsg, "testUser", "Test User");
        var thread = await _threadManager.CreateUserInitiatedThread(new CreateThreadRequest(startMessageRequest));

        OrchestrationState orchestrationState = await _threadRepo.WaitForSubAgentAssignment(thread.Id, tokenSource.Token);
        OrchestrationMetadata orchestrationMetadata = await ApprovalTestHelper.WaitForCompletionWithAutomaticApprovals(
            durableTaskClient: _durableTaskClient,
            orchestrationState.OrchestrationInstanceId,
            threadRepository: _threadRepo,
            thread.Id,
            logger: null,
            tokenSource.Token);

        var inboundResponse = await _threadManager.CreateMessage(thread.Id, new CreateMessageRequest(followupMsg, "testUser", "Test User"));
        var (agentResponse, fullConversation) = await _threadRepo.WaitForAgentResponse(thread, tokenSource.Token);

        // For the eval, remove the last message (agent response) from the conversation history as the agentResponse is used for this.
        var evalResult = await agentResponse.EvaluateAsync(TestContext, _chatConfiguration, fullConversation.SkipLast(1), groundedContext, exampleResponse, _llmDeploymentName);

        TestContext.WriteMessages(fullConversation);
        Assert.IsTrue(evalResult.Equivalence.Value >= 4, $"Low equivalence score: {evalResult.Equivalence.Value}, {evalResult.Equivalence.Reason}");
        Assert.IsTrue(evalResult.Groundedness.Value >= 4, $"Low groundedness score: {evalResult.Groundedness.Value}, {evalResult.Groundedness.Reason}");
    }
}
