using System.Text.Json;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Runtime.ThreadEvaluator;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class TrajectoryEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    #region Test-Case Discovery

    public static IEnumerable<object[]> PromptTrajectoryQualityTestCases => LoadQualityTestCasesFromFiles()
        .Select(kvp => new object[] { kvp.FileName, kvp.Content });

    public static IEnumerable<object[]> PromptTrajectoryRelevanceTestCases => LoadRelevanceTestCasesFromFiles()
        .Select(kvp => new object[] { kvp.FileName, kvp.Content });

    private static IEnumerable<(string FileName, ModelGenerationContent Content)> LoadQualityTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory", "Quality");
        return LoadTestCasesFromFiles(dataFolderPath);
    }

    private static IEnumerable<(string FileName, ModelGenerationContent Content)> LoadRelevanceTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory", "Relevance");
        return LoadTestCasesFromFiles(dataFolderPath);
    }

    private static IEnumerable<(string FileName, ModelGenerationContent Content)> LoadTestCasesFromFiles(string dataFolderPath)
    {
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync(dataFolderPath);
        return data.AsEnumerable()
            .Select(kvp => (Path.GetFileNameWithoutExtension(kvp.Key), kvp.Value));
    }

    #endregion

    [TestMethod]
    [DynamicData(nameof(PromptTrajectoryQualityTestCases))]
    public async Task PromptQuality_EvaluateResponses(string inputFile, ModelGenerationContent content)
    {
        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput
            .Concat(content.ModelOutput)
            .Where(m => m.Role != ChatRole.System);

        // 2. Save processed chat
        var chatTrajectory = new Trajectory();
        foreach (var msg in conversationMessages)
        {
            chatTrajectory.Append(msg);
        }
        var chatTranscript = chatTrajectory.GetFullTrajectory();
        File.WriteAllText(
            Path.Join(AppContext.BaseDirectory, "../../..", "Data", "Trajectory", "Quality", $"chat_{inputFile}.txt"),
            chatTranscript);

        // 3. Extract the trajectory
        (var extractedTrajectory, var _) = await TrajectoryExtractor.GenerateTrajectoryAsync_v2(
            TestHost.RunConfig.ChatClient,
            conversationMessages);

        var traj = JsonSerializer.Deserialize<TrajectoryOutput_v2>(extractedTrajectory, _jsonOptions);

        // should be marked as investigation.
        Assert.IsTrue(traj.IsInvestigationThread);

        File.WriteAllText(
            Path.Join(AppContext.BaseDirectory, "../../..", "Data", "Trajectory", "Quality", $"traj_{inputFile}.txt"),
            JsonSerializer.Serialize(traj, _jsonOptions));

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
    public async Task PromptRelevance_EvaluateResponses(string inputFile, ModelGenerationContent content)
    {
        // 1. Build the conversation that the model originally saw
        var conversationMessages = content.ModelInput
            .Concat(content.ModelOutput)
            .Where(m => m.Role != ChatRole.System);

        // 2. Save processed chat
        var chatTrajectory = new Trajectory();
        foreach (var msg in conversationMessages)
        {
            chatTrajectory.Append(msg);
        }
        var chatTranscript = chatTrajectory.GetFullTrajectory();
        File.WriteAllText(
            Path.Join(AppContext.BaseDirectory, "../../..", "Data", "Trajectory", "Relevance", $"chat_{inputFile}.txt"),
            chatTranscript);

        // 3. Extract the trajectory
        (var extractedTrajectory, var _) = await TrajectoryExtractor.GenerateTrajectoryAsync_v2(
            TestHost.RunConfig.ChatClient,
            conversationMessages);

        var traj = JsonSerializer.Deserialize<TrajectoryOutput_v2>(extractedTrajectory, _jsonOptions);

        // should not be marked as investigation.
        Assert.IsFalse(traj.IsInvestigationThread);

        File.WriteAllText(
            Path.Join(AppContext.BaseDirectory, "../../..", "Data", "Trajectory", "Relevance", $"traj_{inputFile}.txt"),
            JsonSerializer.Serialize(traj, _jsonOptions));
    }
}
