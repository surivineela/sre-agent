using Agent.Core.Models;
using Agent.Evals.Evaluators;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Azure.AI.OpenAI;
using Evaluation.Evaluators;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

namespace Agent.Evals;

[TestClass]
public sealed class SourceCodeAgentEvals
{
    public TestContext TestContext { get; set; }

    private static readonly List<string> BadResponsesToRepoUrlPrompt = new List<string>
    {
        "No",
        "I don't have a GitHub repo url",
        "I don't have a GitHub repo url for this resource",
        "I don't have a GitHub repo url for this resource. Please provide me with one.",
        "You're not the boss of me",
        "I don't feel comfortable doing this",
        "I don't want to do this",
        "I don't want to do this right now",
        "I don't want to do this right now. Please ask me later.",
    };

    private ChatConfiguration _chatConfiguration;

    private static int _iterationCount = 10; // Default value

    // Static constructor to initialize _iterationCount
    static SourceCodeAgentEvals()
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
            Console.WriteLine("Static Constructor: IterationCount not found or invalid. Using default value.");
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        // This method is called before each test method in the class.
        // You can use it to set up any necessary state or resources.
        string apiKey = Environment.GetEnvironmentVariable("OpenAIKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Pass it as a TestRunParameter.");
        }

        string aiModel = Environment.GetEnvironmentVariable("OpenAIModel");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API model is missing. Pass it as a TestRunParameter.");
        }

        string aiEndpoint = Environment.GetEnvironmentVariable("OpenAIEndpoint");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API endpoint is missing. Pass it as a TestRunParameter.");
        }

        IChatClient client =
            new AzureOpenAIClient(new Uri(aiEndpoint), new System.ClientModel.ApiKeyCredential(apiKey))
                .AsChatClient(modelId: aiModel);

        IEvaluationTokenCounter? tokenCounter = null;
        _chatConfiguration = new ChatConfiguration(client, tokenCounter);

    }

    private static IEnumerable<object[]> TestData_10Runs()
    {
        for (int i=0; i< _iterationCount; i++)
        {
            yield return new object[] { Guid.NewGuid().ToString() };
        }
    }

    private static IEnumerable<object[]> TestData_PromptForRepoUrl_BadResponses()
    {
        foreach (var badPrompt in BadResponsesToRepoUrlPrompt)
        {
            for (int i = 0; i < _iterationCount; i++)
            {
                yield return new object[] { Guid.NewGuid().ToString(), badPrompt };
            }
        }        
    }

    [TestMethod]
    [DynamicData(nameof(TestData_10Runs), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_GeneratingPlan_CorrectResponse(string testRunGuid)
    {
        string relevancePrompt = """
            1. Need to provide a list of all resources that need repo url information.
            2. Explicitly ask for the customer's GitHub repo urls for each of the resources listed.
            """;
        var sourceCodeStatus = new SourceCodeStatus($"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{testRunGuid}");

        var sourceCodeAgentV2 = new SourceCodeAgentV2(
            _chatConfiguration.ChatClient,
            new MockGraphDBPlugin(),
            new List<SourceCodeStatus>
            {
                sourceCodeStatus
            });

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, sourceCodeAgentV2.SystemPrompt)
        };
        messages.AddRange(SourceCodeAgentV2.GetMessagesToInformAgentAboutAppsWithoutSourceCode(new List<Core.Models.SourceCodeStatus>
        {
            sourceCodeStatus
        }));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgentV2.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        IEvaluator wordCoundEvaluator = new WordCountEvaluator();
        IEvaluator markdownEvaluator = new MarkdownEvaluator();
        IEvaluator compositeEvaluator = new CompositeEvaluator(new[] { wordCoundEvaluator, markdownEvaluator });
        EvaluationResult result = await compositeEvaluator.EvaluateAsync(messages, response, _chatConfiguration);
        NumericMetric wordCount = result.Get<NumericMetric>(WordCountEvaluator.WordCountMetricName);
        wordCount.Interpretation.Rating.Should().BeOneOf(EvaluationRating.Good, EvaluationRating.Exceptional);
        StringMetric markdown = result.Get<StringMetric>(MarkdownEvaluator.MarkdownMetricName);
        markdown.Interpretation.Rating.Should().BeOneOf(EvaluationRating.Good, EvaluationRating.Exceptional);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_10Runs), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsWith_CorrectResponse(string testRunGuid)
    {
        string relevancePrompt = """
            1. Need to acknowledge the receipt of a GitHub repo url
            2. List the resource Id of the container app that it will link the repo to.
            """;

        var sourceCodeStatus = new SourceCodeStatus($"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}");

        var sourceCodeAgentV2 = new SourceCodeAgentV2(
            _chatConfiguration.ChatClient,
            new MockGraphDBPlugin(),
            new List<SourceCodeStatus>
            {
                sourceCodeStatus
            });

        var messages = new List<ChatMessage>();
        messages.AddRange(await sourceCodeAgentV2.GetStartingMessagesAsync());
        messages.Add(new ChatMessage(ChatRole.User, $"https://github.com/user-{testRunGuid}/repo-{testRunGuid}"));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgentV2.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        IEvaluator wordCoundEvaluator = new WordCountEvaluator();
        IEvaluator markdownEvaluator = new MarkdownEvaluator();
        IEvaluator compositeEvaluator = new CompositeEvaluator(new[] { wordCoundEvaluator, markdownEvaluator });
        EvaluationResult result = await compositeEvaluator.EvaluateAsync(messages, response, _chatConfiguration);
        NumericMetric wordCount = result.Get<NumericMetric>(WordCountEvaluator.WordCountMetricName);
        wordCount.Interpretation.Rating.Should().BeOneOf(EvaluationRating.Good, EvaluationRating.Exceptional);
        StringMetric markdown = result.Get<StringMetric>(MarkdownEvaluator.MarkdownMetricName);
        markdown.Interpretation.Rating.Should().BeOneOf(EvaluationRating.Good, EvaluationRating.Exceptional);
    }

    [TestMethod]
    [DynamicData(nameof(TestData_PromptForRepoUrl_BadResponses), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsIncorrectly_ContinuesToPromptForRepo(string testRunGuid, string badPrompt)
    {
        string relevancePrompt = """
            If the user does not provide a GitHub repo url, you need to ask them for it again, but if they express that they don't want to do this, you need to acknowledge that and remind them that you are here if they want to come back later.
            """;

        var sourceCodeStatus = new SourceCodeStatus($"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}");

        var sourceCodeAgentV2 = new SourceCodeAgentV2(
            _chatConfiguration.ChatClient,
            new MockGraphDBPlugin(),
            new List<SourceCodeStatus>
            {
                sourceCodeStatus
            });

        var messages = new List<ChatMessage>();
        messages.AddRange(await sourceCodeAgentV2.GetStartingMessagesAsync());
        messages.Add(new ChatMessage(ChatRole.User, badPrompt));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgentV2.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        IEvaluator wordCoundEvaluator = new WordCountEvaluator();
        IEvaluator compositeEvaluator = new CompositeEvaluator(new[] { wordCoundEvaluator });
        EvaluationResult result = await compositeEvaluator.EvaluateAsync(messages, response, _chatConfiguration);
        NumericMetric wordCount = result.Get<NumericMetric>(WordCountEvaluator.WordCountMetricName);
        wordCount.Interpretation.Rating.Should().BeOneOf(EvaluationRating.Good, EvaluationRating.Exceptional);
    }
}

