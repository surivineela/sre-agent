// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Runtime.ThreadEvaluator;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agent.Evals.Rag;

[TestClass]
[DoNotParallelize]
public class RagEvaluatorTests
{
    public TestContext TestContext { get; set; }

    private static IHost? _host;
    private static readonly IReadOnlyList<RagTestCase> _testCases = InitializeTestCases();

    private const bool RunIndexSetup = false;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        // Build test app
        var builder = TestHelpers.BuildTestApp(out _);
        if (!builder.IsAgentMemoryEnabled())
        {
            testContext.WriteLine("AgentMemory is not enabled, skipping test initialization.");
            return;
        }

        builder
            .RegisterDefaultServices()
            .ConfigureAgentMemory()
            .AddAgentMemoryPlugin();

        _host = builder.Build();
        await _host.StartAsync();

        // Setup index with test trajectories using shared helper
        // Skippable if running test multiple times
        // ToDo: Move to creating a marker file and use that as canary for reruns??
        if (RunIndexSetup)
        {
#pragma warning disable CS0162 // Unreachable code detected
            await RagTestHelpers.SetupTestIndexAsync(
                testContext,
                _host,
                dataFolderPath: Path.Combine("Data", "RagEval"));
#pragma warning restore CS0162 // Unreachable code detected
        }

        testContext.WriteLine($"Test initialization complete. Loaded {_testCases.Count} test cases.");
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
    public async Task PrecisionRecallSanityTest(RagTestCase testCase)
    {
        if (_host is null)
        {
            TestContext.WriteLine($"Skip {nameof(PrecisionRecallSanityTest)} because feature flag is off");
            return;
        }

        // Arrange - Use the AgentMemoryPlugin through DI
        var plugin = _host.Services.GetRequiredService<AgentMemoryPluginDefinition>();
        var ragEvaluator = _host.Services.GetRequiredService<RagEvaluator>();

        TestContext.WriteLine($"\n=== Testing Case: {testCase.Description} ===");
        TestContext.WriteLine($"Query: {testCase.Query}");
        TestContext.WriteLine($"Expected relevant trajectory titles: {string.Join(", ", testCase.RelevantTrajectoryTitles)}");

        // Act - Execute search using the plugin abstraction layer (the way agent would use it)
        var searchResultText = await plugin.SearchMemoryAsync(
            resourceId: testCase.ResourceId,
            symptoms: testCase.Query);

        // Assert - Validate search returns meaningful results
        Assert.IsNotNull(searchResultText, "SearchMemoryAsync should return results");
        Assert.IsFalse(string.IsNullOrWhiteSpace(searchResultText), "Search results should not be empty");
        Assert.IsFalse(searchResultText.Contains(AgentMemoryPluginDefinition.NoRelevantResultsMessage),
            "Should find relevant memories for this query");

        TestContext.WriteLine($"Search result preview (first 500 chars):\n{searchResultText[..Math.Min(500, searchResultText.Length)]}");

        // Validate results contain expected trajectories by checking if their titles appear in the markdown response
        foreach (var relevantTitle in testCase.RelevantTrajectoryTitles)
        {
            var found = searchResultText.Contains(relevantTitle, StringComparison.OrdinalIgnoreCase);
            TestContext.WriteLine($"Expected trajectory '{relevantTitle}': {(found ? "FOUND" : "NOT FOUND")}");
        }

        // At least one relevant trajectory section should be in results
        var hasTrajectories = searchResultText.Contains("## Similar Past Incidents") ||
                             searchResultText.Contains("## Past Incidents with Similar Symptoms");

        Assert.IsTrue(hasTrajectories,
            "Search results should contain trajectory sections with past incidents");

        TestContext.WriteLine("✓ Test passed: Plugin abstraction layer returns relevant results in expected format");

        // Use RagEvaluator to evaluate retrieval quality
        var searchCall = new SearchMemoryCall(
            CallId: string.Empty,
            ResourceId: testCase.ResourceId,
            Symptoms: testCase.Query);

        var evalResult = await ragEvaluator.EvaluateRetrievalScore(searchCall, searchResultText);

        TestContext.WriteLine($"\n=== Per-Document Evaluation Results ===");
        TestContext.WriteLine($"Ranking Quality Score: {evalResult.RankingQualityScore}/5");
        TestContext.WriteLine($"Ranking Reasoning: {evalResult.RankingReasoning}");
        TestContext.WriteLine($"\nDocument Scores ({evalResult.DocumentScores.Count} items):");

        foreach (var docScore in evalResult.DocumentScores)
        {
            TestContext.WriteLine($"  - {docScore.Title}: {docScore.RelevanceScore}/5");
            TestContext.WriteLine($"    Reasoning: {docScore.Reasoning}");
        }

        TestContext.WriteLine($"\nThought Chain: {evalResult.ThoughtChain}");

        // Assert that expected trajectory titles appear in document scores with high relevance
        foreach (var expectedTitle in testCase.RelevantTrajectoryTitles)
        {
            var matchingDoc = evalResult.DocumentScores.FirstOrDefault(d =>
                d.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase));

            Assert.IsNotNull(matchingDoc,
                $"Expected trajectory '{expectedTitle}' should appear in DocumentScores");
            Assert.IsTrue(matchingDoc.RelevanceScore >= 4,
                $"Expected trajectory '{expectedTitle}' should have RelevanceScore >= 4, got {matchingDoc.RelevanceScore}");
        }

        // Assert that ranking quality is acceptable
        Assert.IsTrue(evalResult.RankingQualityScore >= 3,
            $"Ranking quality should be at least 3 (acceptable), got {evalResult.RankingQualityScore}");

        TestContext.WriteLine($"\n✓ Test case passed with ranking quality {evalResult.RankingQualityScore}/5");
    }

    private static IEnumerable<object[]> GetTestCases()
    {
        return _testCases.Select(tc => new object[] { tc });
    }

    private static IReadOnlyList<RagTestCase> InitializeTestCases()
    {
        return
        [
            new RagTestCase(
                Query: "web app experiencing high CPU usage",
                ResourceId: "test-resource-id",
                RelevantTrajectoryTitles: new HashSet<string>
                {
                    "Web App High CPU - Memory Leak Investigation",
                    "App Service Performance Degradation - High CPU"
                },
                Description: "Query should retrieve CPU-related trajectories. Ground truth: 2 clearly relevant. The borderline CPU spike trajectory may or may not be retrieved/judged relevant by LLM.")
        ];
    }
}

public sealed record RagTestCase(
    string Query,
    string ResourceId,
    IReadOnlySet<string> RelevantTrajectoryTitles,
    string Description);

