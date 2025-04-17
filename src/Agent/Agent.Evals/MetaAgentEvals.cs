using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins.Mocks;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agent.Runtime;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.MetaAgent;
using Microsoft.Extensions.Hosting;
using Moq;
using Microsoft.Extensions.Logging;
using Agent.Core.Configuration;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Core.Models.Api.v1;
using Agent.Core.Helpers;
using Microsoft.AspNetCore.OData.Query;

namespace Agent.Evals;

[TestClass]
public class MetaAgentEvals
{
    private IHost? _host;
    public TestContext TestContext { get; set; }

    private IChatClient? _chatClient;
    private ChatConfiguration? _chatConfiguration;

    private static int _iterationCount = 10; // Default value

    // Static constructor to initialize _iterationCount
    static MetaAgentEvals()
    {
        // Retrieve the IterationCount from environment variables or a default value
        string? iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
        if (int.TryParse(iterationCountEnv, out int parsedIterations))
        {
            Console.WriteLine($"Static Constructor: IterationCount is {parsedIterations}");
            _iterationCount = parsedIterations;
        }
        else
        {
            Console.WriteLine("Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _host = TestHelpers.BuildTestHost();
        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

        _chatClient = _chatConfiguration.ChatClient.AsBuilder().Build();
    }

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    private MetaAgent GetMockedMetaAgent(
        IChatClient chatClient,
        ILogger<MetaAgent>? logger = null,
        ThreadService? threadService = null,
        McpToolsRepository? mcpToolsRepository = null,
        IChartPlugin? chartplugin = null,
        DashboardSettings? dashboardSettings = null,
        IMetaAgentManagedIdentityMigrationPlugin? managedIdentityMigrationPlugin = null,
        IMetaAgentTlsBestPracticesPlugin? tlsBestPracticesPlugin = null,
        IMetaAgentAppServiceRemediationPlugin? appServiceRemediationPlugin = null,
        IMetaAgentContainerAppsRemediationPlugin? containerAppsRemediationPlugin = null,
        IMetaAgentStorageAccountPlugin? storageAccountPlugin = null,
        IMetaAgentKubernetesAgentPlugin? kubernetesAgentPlugin = null,
        IAppServicePlugin? appServicePlugin = null,
        IContainerAppPlugin? containerAppPlugin = null,
        IGithubIssuePlugin? githubIssuePlugin = null,
        IGraphDBPlugin? graphDBPlugin = null,
        IMetaAgentAppReliabilityPlugin? appReliabilityPlugin = null,
        IMetaAgentWebAppDownPlugin? webAppDownPlugin = null,
        IServiceProvider? serviceProvider = null,
        IMetaAgentVmRdpInvestigatorPlugin? vmRdpInvestigatorPlugin = null,
        IMetaAgentContainerImageTroubleshooterPlugin? containerImageTroubleshooterPlugin = null,
        IMetaAgentFunctionAppConnectivityPlugin? functionAppConnectivityPlugin = null)
    {
        return new MetaAgent(
            _chatClient!,
            logger ?? Mock.Of<ILogger<MetaAgent>>(),
            threadService ?? Mock.Of<ThreadService>(),
            mcpToolsRepository ?? Mock.Of<McpToolsRepository>(),
            chartplugin ?? Mock.Of<IChartPlugin>(),
            dashboardSettings ?? new DashboardSettings(),
            managedIdentityMigrationPlugin ?? Mock.Of<IMetaAgentManagedIdentityMigrationPlugin>(),
            tlsBestPracticesPlugin ?? Mock.Of<IMetaAgentTlsBestPracticesPlugin>(),
            appServiceRemediationPlugin ?? Mock.Of<IMetaAgentAppServiceRemediationPlugin>(),
            containerAppsRemediationPlugin ?? Mock.Of<IMetaAgentContainerAppsRemediationPlugin>(),
            storageAccountPlugin ?? Mock.Of<IMetaAgentStorageAccountPlugin>(),
            kubernetesAgentPlugin ?? Mock.Of<IMetaAgentKubernetesAgentPlugin>(),
            appServicePlugin ?? Mock.Of<IAppServicePlugin>(),
            containerAppPlugin ?? Mock.Of<IContainerAppPlugin>(),
            githubIssuePlugin ?? Mock.Of<IGithubIssuePlugin>(),
            graphDBPlugin ?? Mock.Of<IGraphDBPlugin>(),
            appReliabilityPlugin ?? Mock.Of<IMetaAgentAppReliabilityPlugin>(),
            webAppDownPlugin ?? Mock.Of<IMetaAgentWebAppDownPlugin>(),
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),
            vmRdpInvestigatorPlugin ?? Mock.Of<IMetaAgentVmRdpInvestigatorPlugin>(),
            containerImageTroubleshooterPlugin ?? Mock.Of<IMetaAgentContainerImageTroubleshooterPlugin>(),
            functionAppConnectivityPlugin ?? Mock.Of<IMetaAgentFunctionAppConnectivityPlugin>());
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_GeneralQuestions_ListBlobTriggerFunctions(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Identify the function app that uses the blob trigger.
            2. Provide the information about this function app. For example, the resource ID, or subscription name, resource group name and resource name.
            3. Do not ask for more information from user.

            ## Expected Response Characteristics
            - The response should clearly states the function app that uses the blob trigger.
            - The response should avoid unnecessary information or ambiguity.
            """;

        var exampleResponse = $"""
            Here's one of the function apps that uses the blob trigger:
            - /subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/capps-zhenqxu-eu2-private-rp-rg/providers/Microsoft.Web/sites/capps-azfunc-bgtasks-968c7

            Do you have further questions about this function app?
            """;

        var userMsg = "show me a function app that uses blob trigger";
        var threadMsgs = new List<Message>
        {
            new Message(Guid.Parse(testRunGuid), DateTime.UtcNow, new Author(Role.User, testRunGuid, "testUser"), userMsg),
        };

        var mockThreadRepository = new Mock<IThreadRepository>();
        mockThreadRepository.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<ODataQueryOptions>()))
            .ReturnsAsync(threadMsgs);

        var sinkService = new SinkService(
            Mock.Of<IThreadRepository>(),
            Mock.Of<ILogger<SinkService>>());

        var threadService = new ThreadService(
            Mock.Of<ILogger<ThreadService>>(),
            mockThreadRepository.Object,
            Mock.Of<IThreadOrchestrationManager>(),
            sinkService);

        var agent = GetMockedMetaAgent(_chatClient!, threadService: threadService);

        var userChatMsg = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, userMsg)
        };
        var context = new ThreadContext(Guid.Parse(testRunGuid), AgentTypeEnum.Meta);
        var result = agent.ProcessUserMessage(context);
        var agentMsg = new ChatMessage(ChatRole.Assistant, result.Result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse);
    }
}
