using Agent.Framework;
using Agent.Runtime.ThreadEvaluator;
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
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync(dataFolderPath);
        return data.Values.ToArray();
    }

    #endregion

    [TestMethod]
    [DynamicData(nameof(PromptQualityTestCases))]
    public async Task PromptQuality_EvaluateResponses(ModelGenerationContent content)
    {
        var chatTrajectory = new Trajectory();

        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput
            .Concat(content.ModelOutput)
            .Where(m => m.Role != ChatRole.System)
            .ToList();

        // 2. Build the text block that will be fed to the summariser in a <chat>…</chat> wrapper
        foreach (var msg in conversationMessages)
        {
            chatTrajectory.Append(msg);
        }

        string chatTranscript = chatTrajectory.GetFullTrajectory();

        // 3. Compute the trajectory
        (var extractedTrajectory, var _) = await TrajectoryExtractor.GenerateTrajectoryAsync(
            TestHost.RunConfig.ChatClient,
            chatTranscript);

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
