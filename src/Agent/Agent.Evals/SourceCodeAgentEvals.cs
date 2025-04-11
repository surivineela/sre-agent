using Agent.Core.Models;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Azure.AI.OpenAI;
using Evaluation.Evaluators;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Newtonsoft.Json;

namespace Agent.Evals;

[TestClass]
public sealed class SourceCodeAgentEvals
{
    public TestContext TestContext { get; set; }

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

    private static IEnumerable<object[]> TestData_Iterations()
    {
        for (int i=0; i< _iterationCount; i++)
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

        var exampleResponse = $"## 🛠️ Steps I Will Follow for Completion  \r\n\r\n1. **Start by Identifying Apps**  \r\n   - Begin by checking which container apps currently lack an associated source code node.  \r\n   - Completed: Based on your input, I found the app (`/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{testRunGuid}`) without a source code node.\r\n\r\n2. **Await Repo URL**  \r\n   - Please provide a specific GitHub repo URL for the container app that currently lacks a source code node.\r\n\r\n   Example:\r\n   ```plaintext\r\n   Container App:  \r\n   /subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{testRunGuid}  \r\n\r\n   Repo URL: https://github.com/{{ORG_NAME}}/{{REPO_NAME}}\r\n   ```\r\n\r\n3. **Link the Repo (Once Provided)**  \r\n   - I'll proceed to attach the provided repo URL to the specified container app.\r\n\r\n4. **Recheck App List**  \r\n   - Perform another scan to check for any remaining container apps requiring source code nodes, repeating the workflow until all are resolved.\r\n\r\nLet me know the GitHub repo URL for the app so I can move forward!";

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
        var result = await response.GenerateEvaluationAsync(_chatConfiguration, messages, groundedContext, exampleResponse);
        TestContext.WriteLine(JsonConvert.SerializeObject(result));
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsWith_CorrectResponse(string testRunGuid)
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

        var exampleResponse = $"## 🛠️ Steps I Will Follow for Completion  \r\n\r\n1. **Start by Identifying Apps**  \r\n   - Begin by checking which container apps currently lack an associated source code node.  \r\n   - Completed: Based on your input, I found the app (`{containerAppResourceId}`) without a source code node.\r\n\r\n2. **Await Repo URL**  \r\n   - Please provide a specific GitHub repo URL for the container app that currently lacks a source code node.\r\n\r\n   Example:\r\n   ```plaintext\r\n   Container App:  \r\n   {containerAppResourceId}  \r\n\r\n   Repo URL: https://github.com/{{ORG_NAME}}/{{REPO_NAME}}\r\n   ```\r\n\r\n3. **Link the Repo (Once Provided)**  \r\n   - I'll proceed to attach the provided repo URL to the specified container app.\r\n\r\n4. **Recheck App List**  \r\n   - Perform another scan to check for any remaining container apps requiring source code nodes, repeating the workflow until all are resolved.\r\n\r\nLet me know the GitHub repo URL for the app so I can move forward!";

        var sourceCodeStatus = new SourceCodeStatus(containerAppResourceId);

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

        var result = await response.GenerateEvaluationAsync(_chatConfiguration, messages, groundedContext, exampleResponse);
        TestContext.WriteLine(JsonConvert.SerializeObject(result));
    }

    [TestMethod]
    [DynamicData(nameof(TestData_Iterations), DynamicDataSourceType.Method)]
    public async Task SingleAppWithoutSourceCodeNode_UserRespondsIncorrectly_ContinuesToPromptForRepo(string testRunGuid)
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

        var sourceCodeStatus = new SourceCodeStatus($"/subscriptions/e7d12d69-614e-4bc8-98cb-c93ab4e91017/resourceGroups/hackathon-2024-rg/providers/Microsoft.App/containerApps/ca{Guid.NewGuid()}");

        var exampleResponse = "🔄 No worries! In order for me to link a repository to your container app, I need you to provide a GitHub repository URL (e.g., `https://github.com/...`). Without this information, I can't proceed to update the graph and associate the container app with its source code.\r\n\r\nWould you like me to wait for you to identify or create an appropriate GitHub repository for this container app? Let me know how you'd like to proceed!";

        var sourceCodeAgentV2 = new SourceCodeAgentV2(
            _chatConfiguration.ChatClient,
            new MockGraphDBPlugin(),
            new List<SourceCodeStatus>
            {
                sourceCodeStatus
            });

        var messages = new List<ChatMessage>();
        messages.AddRange(await sourceCodeAgentV2.GetStartingMessagesAsync());
        messages.Add(new ChatMessage(ChatRole.User, "I don't have a repo url"));

        var chatOptions = new ChatOptions
        {
            Tools = sourceCodeAgentV2.Tools(),
        };

        var response = await _chatConfiguration.ChatClient.GetResponseAsync(messages, chatOptions);
        var result = await response.GenerateEvaluationAsync(_chatConfiguration, messages, groundedContext, exampleResponse);
        TestContext.WriteLine(JsonConvert.SerializeObject(result));
    }
}

