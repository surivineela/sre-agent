using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Runtime.Communication;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using static Agent.Tests.Common.Mocks.MetaAgentMock;

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

        _chatConfiguration = new ChatConfiguration(client);

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
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 0, -1)).
            ReturnsAsync([]);
        // return all function apps in graph
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), "", "", 0, -1)).
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

        var factory = GetMockedThirdPartAgentsFactory(graphDBPlugin: mockGraphDbPlugin.Object);
        var agent = GetMockedMetaAgent(_chatClient!, factory, threadService: threadService, threadRepository: mockThreadRepository.Object);

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

        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 0, -1), Times.AtLeastOnce);
        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), "", "", 0, -1), Times.Once);
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

        var factory = GetMockedThirdPartAgentsFactory(graphDBPlugin: mockGraphDbPlugin.Object);
        var agent = GetMockedMetaAgent(_chatClient!, factory, threadService: threadService, threadRepository: mockThreadRepository.Object);

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

        var userMsg = "What's wrong with my function app capps-azfunc-bgtasks-968c7";
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
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 0, -1)).
            ReturnsAsync([]);
        // return all function apps in graph
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), "", "", 0, -1)).
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


        var factory = GetMockedThirdPartAgentsFactory(graphDBPlugin: mockGraphDbPlugin.Object);
        var agent = GetMockedMetaAgent(_chatClient!, factory, threadService: threadService, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);

        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 0, -1), Times.AtLeastOnce);
        mockGraphDbPlugin.Verify(s => s.ListResourcesByTypeAsync(It.IsAny<string>(), "", "", 0, -1), Times.Once);
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
        var userMsg = "Create a simple deployment nginx with image nginx:latest in namespace default in my AKS cluster, the AKS cluster resource id is `/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1`";
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
        mockGraphDbPlugin.Setup(x => x.ListResourcesByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 0, -1)).
            ReturnsAsync([]);
        mockGraphDbPlugin.Setup(x => x.SearchResourceByNameAsync(It.IsAny<string>())).
            ReturnsAsync(
                new List<object>
                {
                    new Data.DatabaseClients.GraphDbClient.ArmResourceNode
                    {
                        SubscriptionId = "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10",
                        ResourceGroupName = "rg",
                        ResourceName = "prod-shopping-c1",
                        Location = "westus2",
                        ResourceType = "microsoft.containerservice/managedClusters",
                        ResourceId = "/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/microsoft.containerservice/managedClusters/prod-shopping-c1",
                    },
                    new Data.DatabaseClients.GraphDbClient.KubernetesNamespacedResourceNode(
                        null,                                         // k8sObject
                        "/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1", // clusterResourceId
                        "default",                                    // namespace
                        "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10",       // subscriptionId
                        "rg",                                         // resourceGroupName
                        "westus2",                                   // location
                        "checkout",                                   // name
                        "apps",                                       //group
                        "v1",                                         // apiVersion
                        "Deployment",                                 // kind
                        new Dictionary<string, string>(),             // labels
                        new Dictionary<string, string>()              // annotations
                    )
                }
            );

        mockGraphDbPlugin.Setup(x => x.SearchResourceAsync(It.IsAny<string>(), It.IsAny<string>())).
            ReturnsAsync(
                new List<Data.DatabaseClients.GraphDbClient.ArmResourceNode>
                {
                    new Data.DatabaseClients.GraphDbClient.ArmResourceNode
                    {
                        SubscriptionId = "ea2aa16c-c257-4359-aaea-ff2b0f3b3d10",
                        ResourceGroupName = "rg",
                        ResourceName = "prod-shopping-c1",
                        Location = "westus2",
                        ResourceType = "microsoft.containerservice/managedClusters",
                        ResourceId = "/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1",
                    }
                }
            );

        mockGraphDbPlugin.Setup(x => x.GetResourceIdForResourceName(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1");

        var modeKubePlugin = new Mock<IKubePlugin>();
        modeKubePlugin.Setup(x => x.GetAKSClusterResourceIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1");

        var mockKubernetesAgentPlugin = new Mock<IMetaAgentKubernetesAgentPlugin>();
        mockKubernetesAgentPlugin.Setup(x => x.StartKubernetesAgent
            (It.IsAny<string>())).ReturnsAsync("A workflow has been started to answer Kubernetes related questions or remediate Kubernetes workloads, the workflow instance id is: AKS-Orchestration-0236eab7-7166-43b5-9424-48ee43ef04f6-2025-04-30-12-13-45, thread id is: 0236eab7-7166-43b5-9424-48ee43ef04f6. Will provide followup updates once the workflow is completed.");
        mockKubernetesAgentPlugin.Setup(x => x.ListKubernetesAgentWorkflow())
            .ReturnsAsync(new List<WorkflowMetadata<string>>
            {
                new WorkflowMetadata<string>("mock-id", "mock-input")
            });

        var factory = GetMockedThirdPartAgentsFactory(kubernetesAgentPlugin: mockKubernetesAgentPlugin.Object, graphDBPlugin: mockGraphDbPlugin.Object, kubePlugin: modeKubePlugin.Object);
        var agent = GetMockedMetaAgent(_chatClient!, factory, threadService: threadService, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);


        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        mockKubernetesAgentPlugin.Verify(x => x.StartKubernetesAgent(It.IsAny<string>()), Times.Once);
        Console.WriteLine("mockKubernetesAgentPlugin: StartKubernetesAgent called");

    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task MetaAgent_GeneralQuestions_PagerDutyIncident(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Reply a message with pager duty incidents.
            2. The response should contain each pager duty incident's title, description and a link to each pager duty incident in Markdown format.

            ## Expected Response Characteristics
            - The response should avoid unnecessary information or ambiguity.
            """;

        var exampleResponse = $"""
            Here is a recent incident for your container app dotnet-dump-test2:
            Incident: Test incident titled edited
            Description: /subscriptions/0451dad7-a6c0-4344-bf56-5c52042aa5e2/resourcegroups/tombstone-test-cuseuap-rg/providers/microsoft.app/containerapps/dotnet-dump-test2 is down, not responding to any requests.
            Status: triggered
            Created at: 2025-04-21T08:07:05Z
            You can view more details in PagerDuty: [Test incident titled edited](https://yefutest.pagerduty.com/incidents/Q1GD948W0C9OQN)
            Let me know if you want to investigate or remediate this issue further.
            """;

        var userMsg = "Are there any incidents on my container app by resource id /subscriptions/0451dad7-a6c0-4344-bf56-5c52042aa5e2/resourcegroups/tombstone-test-cuseuap-rg/providers/microsoft.app/containerapps/dotnet-dump-test2?";
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

        var resourceId = "/subscriptions/0451dad7-a6c0-4344-bf56-5c52042aa5e2/resourcegroups/tombstone-test-cuseuap-rg/providers/microsoft.app/containerapps/dotnet-dump-test2";
        var htmlUrl = "https://yefutest.pagerduty.com/incidents/Q1GD948W0C9OQN";
        var incidentPlugin = new Mock<IIncidentPlugin>();
        incidentPlugin.Setup(x => x.GetPagerDutyIncidentsAsync(It.IsAny<string>(), It.IsAny<uint>()))
            .ReturnsAsync(new List<PagerDutyIncidentDocument>
            {
                new(
                    "Q1GD948W0C9OQN",
                    htmlUrl,
                    "triggered",
                    "P1",
                    "high",
                    "major_default",
                    "PHULJQ6",
                    "Default Service",
                    DateTime.UtcNow.AddDays(-1))
                    {
                        Title = "Test incident titled edited",
                        Description = $"{resourceId} is down, not responding to any requests.",
                        UpdatedAt = DateTime.UtcNow.AddDays(-1)
                    }
            });
        // return all function apps in graph
        var factory = GetMockedThirdPartAgentsFactory(incidentPlugin: incidentPlugin.Object);
        var agent = GetMockedMetaAgent(_chatClient!, factory, threadService: threadService, threadRepository: mockThreadRepository.Object);

        var userChatMsg = new List<ChatMessage>
        {
            userChatMessage
        };
        var result = await agent.ProcessUserMessageAsync(agentContext: agentContext, agentChatHistory: agentChatHistory);
        Assert.IsNotNull(result);
        Console.WriteLine($"Agent responds: {result}");

        var agentMsg = new ChatMessage(ChatRole.Assistant, result);
        var response = new ChatResponse(agentMsg);

        await response.EvaluateAsync(TestContext, _chatConfiguration, userChatMsg, groundedContext, exampleResponse, _llmDeploymentName);

        // Assert.IsTrue(result.Contains(htmlUrl), "The response should contain the incident link.");
    }
}
