using System.Text;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Mocks;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Agent.Runtime.SubAgents.FeedbackRCAAgent;
using Agent.Core.Models.Api.v1;

namespace Agent.Evals;

[TestClass]
public sealed class FeedbackRCAEvals
{
    public TestContext TestContext { get; set; }

    private IHost _host;
    private ChatConfiguration _chatConfiguration;
    private string? _llmDeploymentName;

    private static int _iterationCount = 10; // Default value

    // Static constructor to initialize _iterationCount
    static FeedbackRCAEvals()
    {
        // Retrieve the IterationCount from environment variables or a default value
        string iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
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
        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);
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
    public async Task ThumbsUp(string testRunGuid)
    {
        var exampleResponse = "📝 **Feedback RCA Analysis**: \\n\\n**Feedback Summary**: The feedback provided is a \\\"thumbs up,\\\" which is a positive gesture. \\n\\n**Message Context**:\\n1. The user initiated the conversation with two instances of \\\"Hi.\\\"\\n2. The Azure SRE Agent greeted the user in a friendly, professional manner and offered assistance for Microsoft Azure-related needs.\\n3. The user responded with a thumbs-up (positive feedback).\\n\\n**Root Cause Analysis**:\\n✅ The positive feedback suggests the user appreciated the prompt and professional response from the Azure SRE Agent. The agent’s tone and inquiry to assist were likely perceived as helpful and friendly. \\n\\n**Key Contributing Factors**:\\n1. **Timely Response**: The agent replied promptly to the user's messages.\\n2. **Professional and Friendly Tone**: The use of a warm greeting and an open offer to assist created a positive user experience.\\n3. **Clarity in Communication**: The agent clearly communicated their role and purpose in the message.\\n\\nLet me know if you’d like to dive deeper into other aspects of the interaction! 😊\",\r\n";

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var threadRepository = new InmemoryThreadRepository(new NullLogger<InmemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<FeedbackRCAAgent>();

        var messageFeedback = new MessageFeedback(
            Id: Guid.NewGuid(),
            ThreadId: Guid.NewGuid(),
            TimeStamp: DateTime.UtcNow,
            Messages: new List<Message>
            {
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, testRunGuid, "User"), "Hi"),
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, testRunGuid, "User"), "Hi"),
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, testRunGuid, "Assistant"), "Hello! How can I assist you with your Microsoft Azure-related needs today?"),
            },
            IsPositiveFeedback: true,
            FeedbackText: "",
            RootCause: null);

        services.AddSingleton(messageFeedback);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var feedbackRCAAgent = serviceProvider.GetRequiredService<FeedbackRCAAgent>();

        var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, feedbackRCAAgent.SystemPrompt)
            };

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        var introMessage = FeedbackRCAAgent.GetIntroMessage(messageFeedback);
        messages.Add(new ChatMessage(ChatRole.Assistant, introMessage.ToString()));

        response = await chatClient.GetResponseAsync(messages);

        var groundedContext = feedbackRCAAgent.SystemPrompt;
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task ThumbsDown(string testRunGuid)
    {
        var exampleResponse = "📝 Hello, this is **Feedback RCA**, and I am here to provide the root cause analysis for the feedback given in the context of the messages shared.\n\n### Summary of Interaction:\n- **User's Activity**: The user initiated the conversation with repetitive \"Hi\" messages.\n- **Agent's Response**: The Azure SRE Agent responded with a generic greeting, asking how it could assist with Azure-related needs.\n- **Feedback**: The user expressed dissatisfaction with a thumbs-down emoji.\n\n---\n\n### Identified Root Cause(s):\n✅ **Generic and Misaligned Response by the Agent**: The agent's reply (\"How can I assist you with Microsoft Azure-related needs today?\") assumes the user's intent without fully understanding or acknowledging their initial messages (\"Hi,\" repeated twice). A more personalized or acknowledgment-based response could have made the interaction feel more engaging or relevant. For example:\n  - \"Hello! Thanks for reaching out. How can I assist you today?\"\n  - \"Hi there! Let me know how I may help.\"\n  \n✅ **Possible User Expectations for a Specific Acknowledgment**: The user might have expected a more directly engaging acknowledgment of their greeting, such as recognition of their attempt to initiate the conversation (\"Hi there! How can I assist?\"). The lack of such acknowledgment could have led to dissatisfaction.\n\n---\n\n### Recommendations for Improvement:\n1. **Improve Greeting Responsiveness**:\n   - Enhance the agent's initial response to dynamically acknowledge the user's greeting in a contextual manner before transitioning to assistance. For example:\n     - \"Hi, [User's DisplayName]! How can I be of help today?\"\n   \n2. **Handle Repeated Greetings**:\n   - If the system detects repeated \"Hi\" or similar phrases without added context, implement logic to handle that in a friendly and proactive way. For instance:\n     - \"Hi! I noticed you've said hello a couple of times—how can I assist you right away?\"\n\n3. **Engagement Training for the Agent**:\n   - Train or configure the agent to interpret different starting signals from users (like short greetings) and respond more conversationally for higher engagement.\n\n---\n\nIf you have a specific GitHub repo for the Azure SRE Agent's configuration (e.g., chatbot intent management files or response templates), I can analyze those further for any gaps in how these responses handle user greetings. Let me know if you'd like me to dive deeper! 🚀";

        var services = new ServiceCollection();

        // Step 2: Register the mock implementation
        var threadRepository = new InmemoryThreadRepository(new NullLogger<InmemoryThreadRepository>());
        var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
        services.AddSingleton<IThreadRepository>(threadRepository);
        services.AddSingleton<SinkService>(sinkService);

        // Step 3: Register other required dependencies
        var chatClient = _chatConfiguration.ChatClient
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
        services.AddScoped<IChatClient>(_ => chatClient);
        services.AddScoped<FeedbackRCAAgent>();

        var messageFeedback = new MessageFeedback(
            Id: Guid.NewGuid(),
            ThreadId: Guid.NewGuid(),
            TimeStamp: DateTime.UtcNow,
            Messages: new List<Message>
            {
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, testRunGuid, "User"), "Hi"),
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.User, testRunGuid, "User"), "Hi"),
                new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, testRunGuid, "Assistant"), "Hello! How can I assist you with your Microsoft Azure-related needs today?"),
            },
            IsPositiveFeedback: false,
            FeedbackText: "",
            RootCause: null);

        services.AddSingleton(messageFeedback);

        // Step 4: Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        // Step 5: Resolve the class under test
        var feedbackRCAAgent = serviceProvider.GetRequiredService<FeedbackRCAAgent>();

        var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, feedbackRCAAgent.SystemPrompt)
            };

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        var introMessage = FeedbackRCAAgent.GetIntroMessage(messageFeedback);
        messages.Add(new ChatMessage(ChatRole.Assistant, introMessage.ToString()));

        response = await chatClient.GetResponseAsync(messages);

        var groundedContext = feedbackRCAAgent.SystemPrompt;
        await response.EvaluateAsync(TestContext, _chatConfiguration, messages, groundedContext, exampleResponse, _llmDeploymentName);
    }
}

