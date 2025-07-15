using Agent.Runtime.ThreadEvaluator;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class TrajectoryEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    #region Test-Case Discovery

    public static IEnumerable<object[]> PromptTrajectoryQualityTestCases => LoadQualityTestCasesFromFiles()
        .Select(tc => new object[] { tc });

    public static IEnumerable<object[]> PromptTrajectoryRelevanceTestCases => LoadRelevanceTestCasesFromFiles()
        .Select(tc => new object[] { tc });

    private static ModelGenerationContent[] LoadQualityTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory", "Quality");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync(dataFolderPath);
        return data.Values.ToArray();
    }

    private static ModelGenerationContent[] LoadRelevanceTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory", "Relevance");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync(dataFolderPath);
        return data.Values.ToArray();
    }

    #endregion

    [TestMethod]
    [DynamicData(nameof(PromptTrajectoryQualityTestCases))]
    public async Task PromptQuality_EvaluateResponses(ModelGenerationContent content)
    {
        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput
            .Concat(content.ModelOutput)
            .Where(m => m.Role != ChatRole.System);

        // 2. Extract the trajectory
        (var extractedTrajectory, var _) = await TrajectoryExtractor.GenerateTrajectoryAsync_v2(
            TestHost.RunConfig.ChatClient,
            conversationMessages);

        // 5. Provide evaluation context (quality)
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

    [TestMethod]
    [DynamicData(nameof(PromptTrajectoryRelevanceTestCases))]
    public async Task PromptRelevance_EvaluateResponses(ModelGenerationContent content)
    {
        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput
            .Concat(content.ModelOutput)
            .Where(m => m.Role != ChatRole.System);

        // 2. Extract the trajectory
        (var extractedTrajectory, var _) = await TrajectoryExtractor.GenerateTrajectoryAsync_v2(
            TestHost.RunConfig.ChatClient,
            conversationMessages);
    }
}
