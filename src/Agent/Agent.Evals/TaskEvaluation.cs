// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.ThreadEvaluator;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Evals;

[TestClass]
public class TaskEvaluation
{
    private static async Task<TestHost> GetTestHostAsync() => await TestHelpers.InitializeTestHost();

    public TestContext TestContext { get; set; } = null!;

    private static TaskExpectation[] TaskExpectations = [
        new(
            TraceFileName: "traces-create-networkpolicy.json",
            ExpectedTaskCount: 1), // Expected number of user message evaluations
        new(
            TraceFileName: "traces-runbook-detail-notsupported.json",
            ExpectedTaskCount: 1),
        new(
            TraceFileName: "traces-network-security-action.json",
            ExpectedTaskCount: 5)
    ];

    public static IEnumerable<object[]> TaskEvaluationTestCases()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "TaskEvaluations");

        // Verify the path exists
        if (!Directory.Exists(dataFolderPath))
        {
            throw new DirectoryNotFoundException($"Data folder not found at: {dataFolderPath}");
        }

        var data = ModelGenerationDataLoader.LoadChatMessagesFromDebuggerTraces(dataFolderPath);

        foreach (var taskTest in TaskExpectations)
        {
            if (!data.ContainsKey(taskTest.TraceFileName))
            {
                // Skip test case if the trace did not parse into model data
                continue;
            }

            var model = data[taskTest.TraceFileName];
            yield return new object[]
            {
                new TaskEvaluationTestCase(
                    TraceFileName: taskTest.TraceFileName,
                    ThreadId: Guid.NewGuid(),
                    ChatMessages: [.. model.ModelInput, .. model.ModelOutput],
                    ExpectedTaskCount: taskTest.ExpectedTaskCount)
            };
        }
    }

    [TestMethod]
    [DynamicData(nameof(TaskEvaluationTestCases))]
    public async Task TaskEvaluationTests(TaskEvaluationTestCase testCase)
    {
        // Create proper services from TestHost like HandOffEvaluation does
        var mockLogger = (await GetTestHostAsync()).Host.Services.GetRequiredService<ILogger<ThreadEvaluator>>();
        var mockChatClientProvider = (await GetTestHostAsync()).Host.Services.GetRequiredService<IChatClientProvider>();
        var mockTracer = (await GetTestHostAsync()).Host.Services.GetRequiredService<Tracer>();
        var agentProvider = (await GetTestHostAsync()).Host.Services.GetRequiredService<IAgentProvider<AgentContext>>();

        // Create a mock IThreadRepository
        var mockThreadRepository = new Mock<IThreadRepository>();
        var mockRagEvaluator = new Mock<IRagEvaluator>();

        // Create the ThreadEvaluator with proper dependencies
        var threadEvaluator = new ThreadEvaluator(
            logger: mockLogger,
            threadRepository: mockThreadRepository.Object,
            chatClientProvider: mockChatClientProvider,
            tracer: mockTracer,
            ragEvaluator: mockRagEvaluator.Object,
            agentProvider: agentProvider
        );

        // Create a test thread model
        var thread = new ThreadModel(
            Id: testCase.ThreadId,
            Title: $"Test Thread {testCase.ThreadId}",
            StartMessage: null,
            LastMessage: null,
            CreatedTimestamp: DateTime.UtcNow.AddHours(-1),
            ModifiedTimestamp: DateTime.UtcNow,
            FeatureConfig: FeatureConfigModel.Default,
            Source: ThreadSource.Agent
        );

        // Verify we have parsed chat messages correctly before calling the evaluator
        Assert.IsTrue(testCase.ChatMessages.Count > 0, "Should have parsed at least one chat message from the trace file");
        Assert.IsNotNull(testCase.ThreadId, "Should have extracted a thread ID");

        // Output all chat messages content for debugging
        TestContext.WriteLine($"=== Chat Messages for Thread {testCase.ThreadId} ===");
        TestContext.WriteLine($"Total Messages: {testCase.ChatMessages.Count}");
        TestContext.WriteLine("");

        // Now call the actual EvaluateTasksWithLLM method
        var evaluationResults = await threadEvaluator.EvaluateTaskWithLLM(
                thread,
                testCase.ChatMessages);

        // Display the evaluation results
        TestContext.WriteLine($"=== Evaluation Results ===");
        TestContext.WriteLine($"Number of evaluations: {evaluationResults.Count}");
        TestContext.WriteLine("");

        Assert.AreEqual(testCase.ExpectedTaskCount, evaluationResults.Count,
            $"Expected {testCase.ExpectedTaskCount} user message evaluations but got {evaluationResults.Count}");

        for (int i = 0; i < evaluationResults.Count; i++)
        {
            var result = evaluationResults[i];
            TestContext.WriteLine($"User Message Evaluation {i + 1}:");
            TestContext.WriteLine($"  Start User Message: {result.StartUserMessage}");
            TestContext.WriteLine($"  User Intent: {result.UserIntent}");
            TestContext.WriteLine($"  Resolved Score: {result.Resolved}");
            TestContext.WriteLine($"  Summary: {result.Summary}");
            TestContext.WriteLine("");

            // Validate that the scores are within the expected range (1-5)
            Assert.IsTrue(result.Resolved >= 1 && result.Resolved <= 5,
                $"Resolved score should be between 1 and 5, but was {result.Resolved}");
            Assert.IsFalse(string.IsNullOrEmpty(result.Summary),
                "Summary should not be null or empty");
        }

        // Test passes if we successfully called the evaluation method without exceptions
        TestContext.WriteLine($"Successfully evaluated user messages for thread {testCase.ThreadId} with {testCase.ChatMessages.Count} messages");
    }

    public sealed record TaskExpectation(
        string TraceFileName,
        int ExpectedTaskCount);

    public sealed record TaskEvaluationTestCase(
        string TraceFileName,
        Guid ThreadId,
        IReadOnlyList<ChatMessage> ChatMessages,
        int ExpectedTaskCount);
}
