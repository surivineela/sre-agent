using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class TrajectoryEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    #region Test-Case Discovery

    public static IEnumerable<object[]> PromptQualityTestCases => LoadTestCasesFromFiles()
        .Select(tc => new object[] { tc });

    private static ModelGenerationContent[] LoadTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data\\Trajectory");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync().GetAwaiter().GetResult();
        return data.Values.ToArray();
    }

    #endregion

    [TestMethod]
    [DynamicData(nameof(PromptQualityTestCases))]
    public async Task PromptQuality_EvaluateResponses(ModelGenerationContent content)
    {
        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput.ToList();

        // 2. Build the text block that will be fed to the summariser in a <chat>…</chat> wrapper
        string chatTranscript = string.Join("\n", conversationMessages.Select(m => $"[{m.Role}] {m.Text ?? string.Join("", m.Contents)}"));

        // 3. Grabbing the prompt
        var promptPath = Path.Combine(
           AppDomain.CurrentDomain.BaseDirectory,
           "EvaluatorPrompts",
           "TrajectorySummarizer.txt");

        var prompt = await File.ReadAllTextAsync(promptPath);

        // 4. Send to the model using the custom system-prompt
        var chatClient = TestHost.RunConfig.ChatClient;
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, prompt),
            new ChatMessage(ChatRole.User, chatTranscript)
        };

        var chatOptions = new ChatOptions {
            ToolMode = ChatToolMode.None,
            Temperature = 0,
            ResponseFormat = ChatResponseFormat.Text
        };
        var summaryResponse = await chatClient.GetResponseAsync(messages, chatOptions);

        // 5. Provide evaluation context (place-holders for now)
        //string groundedContext = "TODO: supply ground-truth context for this trajectory";
        //string exampleResponse = "TODO: supply an ideal reference answer";
        //var messagesForEval = conversationMessages.Append(new ChatMessage(ChatRole.Assistant, summaryResponse.Text));

        //// 5. Run evaluators against the generated summary
        //var evalResults = await summaryResponse.EvaluateAsync(
        //    TestContext,
        //    chatConfiguration: null,
        //    messages: messagesForEval,
        //    groundedContext: groundedContext,
        //    exampleResponse: exampleResponse,
        //    llmDeploymentName: null);

        //// 6. Assert quality thresholds
        //Assert.IsTrue(evalResults.Equivalence.Value >= 4, $"Low equivalence score: {evalResults.Equivalence.Reason}");
        //Assert.IsTrue(evalResults.Coherence.Value >= 4, $"Low coherence score: {evalResults.Coherence.Reason}");
        //Assert.IsTrue(evalResults.Groundedness.Value >= 4, $"Low groundedness score: {evalResults.Groundedness.Reason}");
    }
}
