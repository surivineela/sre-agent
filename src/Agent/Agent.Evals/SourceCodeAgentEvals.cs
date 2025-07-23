using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins.Interface;
using Agent.Plugins.Mocks;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agent.Evals;

[TestClass]
public sealed class SourceCodeAgentEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host = null!;
    private ChatConfiguration _chatConfiguration = null!;
    private string? _llmDeploymentName;

    private static int _iterationCount = 10; // Default value

    // Static constructor to initialize _iterationCount
    static SourceCodeAgentEvals()
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
            Console.WriteLine("SourceCodeAgentEvals Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        _host = builder.Build() ?? throw new InvalidOperationException("Failed to build the host.");
        IChatClient client = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(client);

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
    public async Task SingleAppWithoutSourceCodeNode_GeneratingPlan_CorrectResponse(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Identify container apps that lack source code nodes.
            2. Request GitHub repository URLs for these apps.
            3. Link the provided URLs to the respective container apps in the Azure graph.
            4. Verify that all container apps have source code nodes linked.

            ## Expected Response Characteristics
            - The response should clearly explain the steps to link a container app to its source code.
            - It should reference the specific container app and request the GitHub repository URL.
            - The response should avoid unnecessary information or ambiguity.
            """;

        string containerAppResourceId = $"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}";
        var exampleResponse = $"""
            ## 🔍 Summary of Steps

            I need to associate source code repository URLs with specific container apps. Here's how I will proceed:

            1. **Gather Source Code URLs from You**:
               - I need the GitHub repo URL (e.g., `https://github.com/...`) for the container app mentioned:
                 ```plaintext
                 /subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/caef7f3cf8-3fb0-458d-aef0-713828241604
                 ```
               - Please share the repo URL so I can create the mapping.

            2. **Update Graph**:
               - Once I have the repo URL, I will link it with the container app in the system.

            3. **Confirm Completion**:
               - After the update, I'll recheck to ensure no further containers are missing source code nodes.

            ---

            Let me know the corresponding GitHub repo URL for the app, and I will proceed! ✅
            """;

        var sourceCodeStatus = new SourceCodeStatus(containerAppResourceId);

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var mockGraphDBPlugin = new MockGraphDBPlugin(new List<string> { containerAppResourceId });
        services.AddSingleton<IGraphDBPlugin>(mockGraphDBPlugin);

        var threadRepository = new InMemoryThreadRepository(new NullLogger<InMemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<SourceCodeAgent>();

        var sourceCodeStatusList = new List<SourceCodeStatus>
        {
            sourceCodeStatus
        };
        services.AddSingleton(sourceCodeStatusList);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var sourceCodeAgent = serviceProvider.GetRequiredService<SourceCodeAgent>();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, sourceCodeAgent.SystemPrompt)
        };
        messages.AddRange(SourceCodeAgent.GetMessagesToInformAgentAboutAppsWithoutSourceCode(new List<Core.Models.SourceCodeStatus>
        {
            sourceCodeStatus
        }));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgent.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsWith_CorrectResponse(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Receive the GitHub repository URLs for these apps.
            2. Link the provided URLs to the respective container apps in the Azure graph.
            3. Verify that all container apps have source code nodes linked.
            4. Acknowledge that the loop is complete.

            ## Expected Response Characteristics
            - The response should clearly explain the steps to link a container app to its source code.
            - It should reference the specific container app and request the GitHub repository URL.
            - The response should avoid unnecessary information or ambiguity.
            """;

        string containerAppResourceId = $"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}";
        string gitHubRepo = $"https://github.com/user-{testRunGuid}/repo-{testRunGuid}";

        var exampleResponse = $"""
            ### ✅ Repository URL Received

            We are linking the following:

            - **Azure Container App**:  
              `{containerAppResourceId}`
            - **GitHub Repository**:  
              [{gitHubRepo}]({gitHubRepo})

            Let me link this repository to the container app!
            ### ✅ Repository Successfully Linked

            The following connection has been established:

            - **Azure Container App**:  
              `{containerAppResourceId}`
            - **GitHub Repository**:  
              [{gitHubRepo}]({gitHubRepo})

            Now, let me recheck if any container apps are still pending a source code node.
            ### 🎉 All Tasks Completed

            There are no container apps remaining without a linked source code node. The process has been successfully completed! Let me know if you need assistance with anything else.
            """;

        var sourceCodeStatus = new SourceCodeStatus(containerAppResourceId);

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var mockGraphDBPlugin = new MockGraphDBPlugin(new List<string>());
        services.AddSingleton<IGraphDBPlugin>(mockGraphDBPlugin);
        var threadRepository = new InMemoryThreadRepository(new NullLogger<InMemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<SourceCodeAgent>();

        var sourceCodeStatusList = new List<SourceCodeStatus>
        {
            sourceCodeStatus
        };
        services.AddSingleton(sourceCodeStatusList);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var sourceCodeAgent = serviceProvider.GetRequiredService<SourceCodeAgent>();

        var messages = new List<ChatMessage>();
        messages.AddRange(await sourceCodeAgent.GetStartingMessagesAsync());
        messages.Add(new ChatMessage(ChatRole.User, gitHubRepo));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgent.Tools()
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions);

        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);

        var mapping = mockGraphDBPlugin.GetContainerAppsToSourceCodeNodeMapping();
        Assert.IsTrue(mapping.ContainsKey(containerAppResourceId));
        Assert.AreEqual(gitHubRepo, mapping[containerAppResourceId]);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsIncorrectly_ContinuesToPromptForRepo(string testRunGuid)
    {
        string groundedContext = """
            ## Ground Truth:
            1. Receive the GitHub repository URLs for these apps.
            2. Acknowledge that this is not a valid GitHub repository url
            3. Acknowledge that this agent is available whenever the urls are ready.

            ## Expected Response Characteristics
            - The response should clearly explain the steps to link a container app to its source code.
            - It should reference the specific container app and request the GitHub repository URL.
            - The response should avoid unnecessary information or ambiguity.
            """;

        var containerAppResourceId = $"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}";

        var exampleResponse = "🔄 No worries! In order for me to link a repository to your container app, I need you to provide a GitHub repository URL (e.g., `https://github.com/...`). Without this information, I can't proceed to update the graph and associate the container app with its source code.\r\n\r\nWould you like me to wait for you to identify or create an appropriate GitHub repository for this container app? Let me know how you'd like to proceed!";

        var sourceCodeStatus = new SourceCodeStatus(containerAppResourceId);

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var mockGraphDBPlugin = new MockGraphDBPlugin(new List<string> { containerAppResourceId });
        services.AddSingleton<IGraphDBPlugin>(mockGraphDBPlugin);
        var threadRepository = new InMemoryThreadRepository(new NullLogger<InMemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<SourceCodeAgent>();

        var sourceCodeStatusList = new List<SourceCodeStatus>
        {
            sourceCodeStatus
        };
        services.AddSingleton(sourceCodeStatusList);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var sourceCodeAgent = serviceProvider.GetRequiredService<SourceCodeAgent>();

        var messages = new List<ChatMessage>();
        messages.AddRange(await sourceCodeAgent.GetStartingMessagesAsync());
        messages.Add(new ChatMessage(ChatRole.User, "I don't have a repo url"));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgent.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);
    }
}

