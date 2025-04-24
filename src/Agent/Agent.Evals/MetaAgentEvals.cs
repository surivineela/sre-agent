using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Evals;

[TestClass]
public class MetaAgentEvals
{
    private IHost? _host;
    public TestContext TestContext { get; set; }

    private IChatClient? _chatClient;
    private ChatConfiguration? _chatConfiguration;

    private static int _iterationCount = 1; // Default value

    private string? _llmDeploymentName;

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
            Console.WriteLine("FeedbackRCAEvals Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        _host = builder.Build();
        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        ILoggerFactory loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();

        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

        _chatClient = _chatConfiguration.ChatClient.AsBuilder().
            UseLogging(loggerFactory).
            UseFunctionInvocation(loggerFactory, x =>
            {
                x.IncludeDetailedErrors = true;
            }).Build();

        await _host.StartAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i = 0; i < _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    public static MetaAgent GetMockedMetaAgent(
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
        IMetaAgentFunctionAppConnectivityPlugin? functionAppConnectivityPlugin = null,
        IFirstPartySubAgentsFactory? firstPartySubAgentsFactory = null,
        IThreadRepository threadRepository = null,
        IMetaAgentSqlDbQueryPerfPlugin? sqlDbQueryPerfPlugin = null,
        IMetaAgentAppCodeAnalysisPlugin appCodeAnalysisPlugin = null,
        IMetaAgentCPUAnalysisPlugin cpuAnalysisPlugin = null,
        IAppCodeAnalysisPlugin appCodePlugin = null,
        ICpuAnalysisPlugin cpuPlugin = null)
    {

        return new MetaAgent(
            chatClient,
            logger ?? Mock.Of<ILogger<MetaAgent>>(),
            threadService ?? Mock.Of<ThreadService>(),
            mcpToolsRepository ?? Mock.Of<McpToolsRepository>(),
            chartplugin ?? Mock.Of<IChartPlugin>(),
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
            functionAppConnectivityPlugin ?? Mock.Of<IMetaAgentFunctionAppConnectivityPlugin>(),
            firstPartySubAgentsFactory ?? Mock.Of<IFirstPartySubAgentsFactory>(),
            threadRepository ?? Mock.Of<IThreadRepository>(),
            sqlDbQueryPerfPlugin ?? Mock.Of<IMetaAgentSqlDbQueryPerfPlugin>(),
            Mock.Of<IConnectedIntegrationsPlugin>(),
            appCodeAnalysisPlugin ?? Mock.Of<IMetaAgentAppCodeAnalysisPlugin>(),
            cpuAnalysisPlugin ?? Mock.Of<IMetaAgentCPUAnalysisPlugin>(),
            appCodePlugin ?? Mock.Of<IAppCodeAnalysisPlugin>(),
            cpuPlugin ?? Mock.Of<ICpuAnalysisPlugin>(),
            Mock.Of<InstanceManagementSettings>()
        );
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_GeneralQuestions_ListBlobTriggerFunctions(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Identify one function app that uses the blob trigger.
            2. Provide the information about this function app. For example, the resource ID, or subscription name, resource group name and resource name.
            3. EXACTLY ONE function app should be returned. The function name must be either funcapp1 or funcapp3.
            4. The response should perfectly solves user's request. Should not ask for more information for solving the request.

            ## Expected Response Characteristics
            - The response should clearly states the function app that uses the blob trigger.
            - The response should avoid unnecessary information or ambiguity.
            """;

        var exampleResponse = $"""
            Here is an Azure Function App that uses a queue trigger:

            Function App Name: funcapp1
            Resource Group: rg
            Subscription ID: ea2aa16c-c257-4359-aaea-ff2b0f3b3d10
            Location: eastus2
            Queue Trigger Function Example:

            a (trigger type: queue)

            Would you like to see more details or explore other Function Apps with queue triggers?
            """;

        var userMsg = "show me a function app that uses blob trigger";
        var threadMsgs = new List<Message>
        {
            new Message(Guid.Parse(testRunGuid), DateTime.UtcNow, new Author(Role.User, testRunGuid, "testUser"), userMsg),
        };

        var mockThreadRepository = new Mock<IThreadRepository>();
        mockThreadRepository.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<ODataQueryOptions>()))
            .ReturnsAsync(threadMsgs);

        var userChatMessage = new ChatMessage(ChatRole.User, userMsg);

        var agentContext = new AgentContext(Guid.NewGuid(), Guid.Parse(testRunGuid), AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(userChatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        mockThreadRepository.Setup(x => x.GetReasoningMessageAsync(reasoningMessage.Id, reasoningMessage.AgentContextId))
            .ReturnsAsync(reasoningMessage);

        var sinkService = new SinkService(
            Mock.Of<IThreadRepository>(),
            Mock.Of<ILogger<SinkService>>());

        var threadService = new ThreadService(
            Mock.Of<ILogger<ThreadService>>(),
            mockThreadRepository.Object,
            Mock.Of<IThreadOrchestrationManager>(),
            sinkService);

        var mockGraphDbPlugin = new Mock<IGraphDBPlugin>();
        // when filters are passed, return empty to force it go throught list+get mode
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).
            ReturnsAsync([]);
        // return all function apps in graph
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), "", "")).
            ReturnsAsync(
            [
                // web app
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "webapp" },
                    { "resourceType", "microsoft.web/sites" },
              },
              // func app with blob trigger
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "funcapp1" },
                    { "resourceType", "microsoft.web/sites" },
              },
              // func app with queue trigger
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "funcapp2" },
                    { "resourceType", "microsoft.web/sites" },
              },
              // func app with blob trigger
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "funcapp3" },
                    { "resourceType", "microsoft.web/sites" },
              },
            ]);

        mockGraphDbPlugin.Setup(x => x.GetResourceDetailedProperties("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourcegroups/rg/providers/microsoft.web/sites/webapp")).
            ReturnsAsync(new Dictionary<string, object>
            {
                { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                { "resourceGroupName", "rg" },
                { "resourceName", "webapp" },
                { "resourceType", "microsoft.web/sites" },
                { "kind", "app" },
            });

        mockGraphDbPlugin.Setup(x => x.GetResourceDetailedProperties("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourcegroups/rg/providers/microsoft.web/sites/funcapp1")).
            ReturnsAsync(new Dictionary<string, object>
            {
                { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                { "resourceGroupName", "rg" },
                { "resourceName", "funcapp1" },
                { "resourceType", "microsoft.web/sites" },
                { "kind", "app" },
                { "function_0_name", "a"},
                { "function_0_triggerType", "blob"},
                { "function_1_name", "b"},
                { "function_1_triggerType", "timer"},
            });
        mockGraphDbPlugin.Setup(x => x.GetResourceDetailedProperties("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourcegroups/rg/providers/microsoft.web/sites/funcapp2")).
            ReturnsAsync(new Dictionary<string, object>
            {
                { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                { "resourceGroupName", "rg" },
                { "resourceName", "funcapp2" },
                { "resourceType", "microsoft.web/sites" },
                { "kind", "app" },
                { "function_0_name", "c"},
                { "function_0_triggerType", "queue"},
                { "function_1_name", "d"},
                { "function_1_triggerType", "timer"},
            });
        mockGraphDbPlugin.Setup(x => x.GetResourceDetailedProperties("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourcegroups/rg/providers/microsoft.web/sites/funcapp3")).
            ReturnsAsync(new Dictionary<string, object>
            {
                { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                { "resourceGroupName", "rg" },
                { "resourceName", "funcapp3" },
                { "resourceType", "microsoft.web/sites" },
                { "kind", "app" },
                { "function_0_name", "e"},
                { "function_0_triggerType", "blob"},
                { "function_1_name", "f"},
                { "function_1_triggerType", "timer"},
            });

        // return empty to catch hallucinations of LLM
        mockGraphDbPlugin.Setup(x => x.GetResourceDetailedProperties(It.IsAny<string>())).
            ReturnsAsync(new Dictionary<string, object>());

        var agent = GetMockedMetaAgent(_chatClient!, threadService: threadService, graphDBPlugin: mockGraphDbPlugin.Object, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };

        var context = new ThreadContext(Guid.Parse(testRunGuid), AgentTypeEnum.Meta);
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);

        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), "", ""), Times.Once);
        mockGraphDbPlugin.Verify(s => s.GetResourceDetailedProperties(It.IsAny<string>()), Times.AtLeastOnce);

    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_GeneralQuestions_SummarizeResources(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Reply a message with a link to the grafana dashboard.
            2. A hyper link MUST be provided in the response.
            3. The response should perfectly solves user's request. Should not ask for more information for solving the request.

            ## Expected Response Characteristics
            - The response should clearly stats the details can be viewed in the link.
            - The response should avoid unnecessary information or ambiguity.
            """;

        var exampleResponse = $"""
            I manage your resources through a detailed dashboard. You can view the exact number and types of resources I am managing by visiting [this dashboard link](https://agent-report-ate4c2fvbcf5epds.eus2.grafana.azure.com/d/azure-sre-resources/sre-azure-resource-overview?orgId=1&refresh=1m).
            """;

        var userMsg = "how many resources are you managing for me";
        var threadMsgs = new List<Message>
        {
            new Message(Guid.Parse(testRunGuid), DateTime.UtcNow, new Author(Role.User, testRunGuid, "testUser"), userMsg),
        };

        var mockThreadRepository = new Mock<IThreadRepository>();
        mockThreadRepository.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<ODataQueryOptions>()))
            .ReturnsAsync(threadMsgs);

        var userChatMessage = new ChatMessage(ChatRole.User, userMsg);

        var agentContext = new AgentContext(Guid.NewGuid(), Guid.Parse(testRunGuid), AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(userChatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        mockThreadRepository.Setup(x => x.GetReasoningMessageAsync(reasoningMessage.Id, reasoningMessage.AgentContextId))
            .ReturnsAsync(reasoningMessage);

        var sinkService = new SinkService(
            Mock.Of<IThreadRepository>(),
            Mock.Of<ILogger<SinkService>>());

        var threadService = new ThreadService(
            Mock.Of<ILogger<ThreadService>>(),
            mockThreadRepository.Object,
            Mock.Of<IThreadOrchestrationManager>(),
            sinkService);

        var mockGraphDbPlugin = new Mock<IGraphDBPlugin>();
        mockGraphDbPlugin.Setup(x => x.GetKnowledgeGraphResourceUsageDashboard())
            .Returns("https://agent-report-ate4c2fvbcf5epds.eus2.grafana.azure.com/d/azure-sre-resources/sre-azure-resource-overview?orgId=1&refresh=1m");

        var agent = GetMockedMetaAgent(_chatClient!, threadService: threadService, graphDBPlugin: mockGraphDbPlugin.Object, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);

        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        mockGraphDbPlugin.Verify(s => s.GetKnowledgeGraphResourceUsageDashboard(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_ResourceQuestions_TypoName(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Reply a message that indicates a potential typo in the provided resource name.
            2. One or more potential matching resource names should be provided.
            3. No more than 3 potential matching resource names should be provided.
            4. The response should perfectly solves user's request. Should not ask for more information for solving the request.

            ## Expected Response Characteristics
            - The response should clearly stats the details can be viewed in the link.
            - The response should avoid unnecessary information or ambiguity.
            """;

        var exampleResponse = $"""
            No function app named "capps-axfunc-bgtasks-968c7" exists, but there is a very similar one: capps-azfunc-bgtasks-968c7 in resource group capps-zhenqxu-eu2-private-rp-rg.

            Would you like help diagnosing or troubleshooting "capps-azfunc-bgtasks-968c7"? If so, please confirm this is the correct function app, and let me know what issue you are facing.
            """;

        var userMsg = "What's wrong with my function app capps-axfunc-bgtasks-968c7";
        var threadMsgs = new List<Message>
        {
            new Message(Guid.Parse(testRunGuid), DateTime.UtcNow, new Author(Role.User, testRunGuid, "testUser"), userMsg),
        };

        var mockThreadRepository = new Mock<IThreadRepository>();
        mockThreadRepository.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<ODataQueryOptions>()))
            .ReturnsAsync(threadMsgs);

        var userChatMessage = new ChatMessage(ChatRole.User, userMsg);

        var agentContext = new AgentContext(Guid.NewGuid(), Guid.Parse(testRunGuid), AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(userChatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        mockThreadRepository.Setup(x => x.GetReasoningMessageAsync(reasoningMessage.Id, reasoningMessage.AgentContextId))
            .ReturnsAsync(reasoningMessage);

        var sinkService = new SinkService(
            Mock.Of<IThreadRepository>(),
            Mock.Of<ILogger<SinkService>>());

        var threadService = new ThreadService(
            Mock.Of<ILogger<ThreadService>>(),
            mockThreadRepository.Object,
            Mock.Of<IThreadOrchestrationManager>(),
            sinkService);

        var mockGraphDbPlugin = new Mock<IGraphDBPlugin>();
        // when filters are passed, return empty to force it go throught list+get mode
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).
            ReturnsAsync([]);
        // return all function apps in graph
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), "", "")).
            ReturnsAsync(
            [
                // the correct name
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "capps-azfunc-bgtasks-968c7" },
                    { "resourceType", "microsoft.web/sites" },
              },
              // non-relevant name
              new  Dictionary<string, object>
              {
                    { "subscriptionId", "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10" },
                    { "resourceGroupName", "rg" },
                    { "resourceName", "bgtasks" },
                    { "resourceType", "microsoft.web/sites" },
              },
            ]);


        var agent = GetMockedMetaAgent(_chatClient!, threadService: threadService, graphDBPlugin: mockGraphDbPlugin.Object, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);

        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), "", ""), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_Delegate_to_AKSAgent(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Delegate the question to the Kubernetes agent.
            2. The Kubernetes agent should be able to handle the question.

            ## Expected Response Characteristics
            - The response should clearly show Kubernetes Agent is handling the question or starting the diagnostic.
            """;

        var exampleResponse = $"""
             A workflow has been started to answer Kubernetes related questions or remediate Kubernetes workloads, the workflow instance id is: AKS-Orchestration-0236eab7-7166-43b5-9424-48ee43ef04f6-2025-04-30-12-13-45, thread id is: 0236eab7-7166-43b5-9424-48ee43ef04f6.
            """;

        var userMsg = "Can you show me the AKS APIServer status? cluster name is `prod-shopping-c1`, subscription id is `ea2aa16c-c257-4359-aaea-ff2b0f3b3d10`, resource group name is `rg`";
        var threadMsgs = new List<Message>
        {
            new Message(Guid.Parse(testRunGuid), DateTime.UtcNow, new Author(Role.User, testRunGuid, "testUser"), userMsg),
        };

        var mockThreadRepository = new Mock<IThreadRepository>();
        mockThreadRepository.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<ODataQueryOptions>()))
            .ReturnsAsync(threadMsgs);

        var userChatMessage = new ChatMessage(ChatRole.User, userMsg);

        var agentContext = new AgentContext(Guid.NewGuid(), Guid.Parse(testRunGuid), AgentTypeEnum.Meta, ContextStateEnum.Idle, null, null);
        var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.User, JsonSerializer.Serialize(userChatMessage));
        var agentChatHistory = new AgentChatHistory(agentContext.Id, new List<Guid> { reasoningMessage.Id });

        mockThreadRepository.Setup(x => x.GetReasoningMessageAsync(reasoningMessage.Id, reasoningMessage.AgentContextId))
            .ReturnsAsync(reasoningMessage);

        var sinkService = new SinkService(
            Mock.Of<IThreadRepository>(),
            Mock.Of<ILogger<SinkService>>());

        var threadService = new ThreadService(
            Mock.Of<ILogger<ThreadService>>(),
            mockThreadRepository.Object,
            Mock.Of<IThreadOrchestrationManager>(),
            sinkService);

        var mockGraphDbPlugin = new Mock<IGraphDBPlugin>();
        var mockKubernetesAgentPlugin = new Mock<IMetaAgentKubernetesAgentPlugin>();
        mockKubernetesAgentPlugin.Setup(x => x.StartKubernetesAgentWorkflow
            (It.IsAny<string>())).ReturnsAsync("A workflow has been started to answer Kubernetes related questions or remediate Kubernetes workloads, the workflow instance id is: AKS-Orchestration-0236eab7-7166-43b5-9424-48ee43ef04f6-2025-04-30-12-13-45, thread id is: 0236eab7-7166-43b5-9424-48ee43ef04f6. Will provide followup updates once the workflow is completed.");
        mockKubernetesAgentPlugin.Verify(x => x.StartKubernetesAgentWorkflow(It.IsAny<string>()), Times.Once);
        mockKubernetesAgentPlugin.Setup(x => x.ListKubernetesAgentWorkflow())
            .ReturnsAsync(new List<WorkflowMetadata<string>>
            {
                new WorkflowMetadata<string>("mock-id", "mock-input")
            });

        var agent = GetMockedMetaAgent(_chatClient!, threadService: threadService, threadRepository: mockThreadRepository.Object, kubernetesAgentPlugin: mockKubernetesAgentPlugin.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);

        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);
    }
}
