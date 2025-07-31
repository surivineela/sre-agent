// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.ThreadEvaluator;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Evals;

[TestClass]
public class HandOffEvaluation
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    public TestContext TestContext { get; set; } = null!;

    private static HandOffExpectation[] HandOffExpectations = [
        new(
            TraceFileName: "traces-local-aks-pod-status.json",
            AutoHandoffEnabled: false,
            ExpectedHandoffSuccess: [true]),
        new(
            TraceFileName: "traces-local-containerapp-port-success.json",
            AutoHandoffEnabled: true,
            ExpectedHandoffSuccess: [true, true, true]),
        new(
            TraceFileName : "traces-local-containerapp-port-failure.json",
            AutoHandoffEnabled : true,
            ExpectedHandoffSuccess :[true, false]),
        new(
            TraceFileName : "traces-prod-paas-containerapp-cpu-diagnostics.json",
            AutoHandoffEnabled : false,
            // the 2nd and 4th handoff is questionable as success/failure. could be marked either way.
            ExpectedHandoffSuccess :[true, null, false, null, true, false]),
    ];

    private static IEnumerable<object[]> HandOffEvaluationTestCases()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "HandOffEvaluations");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromDebuggerTraces(dataFolderPath);
        foreach (var handoffTest in HandOffExpectations)
        {
            var model = data[handoffTest.TraceFileName];
            yield return new object[]
            {
                new HandOffEvaluationTestCase(
                    TraceFileName: handoffTest.TraceFileName,
                    ThreadId: Guid.NewGuid(),
                    StartAgent: "meta_agent",
                    AutoHandoffEnabled: handoffTest.AutoHandoffEnabled,
                    ChatMessages: [.. model.ModelInput, .. model.ModelOutput],
                    ExpectedHandoffSuccess: handoffTest.ExpectedHandoffSuccess)
            };
        }
    }

    [TestMethod]
    [DynamicData(nameof(HandOffEvaluationTestCases))]
    public async Task HandOffEvaluationTests(HandOffEvaluationTestCase testCase)
    {
        // Create proper mocks for the ThreadEvaluator dependencies
        var mockLogger = TestHost.Host.Services.GetRequiredService<ILogger<ThreadEvaluator>>();
        var mockChatClient = TestHost.Host.Services.GetRequiredService<IChatClient>();
        var mockTracer = TestHost.Host.Services.GetRequiredService<Tracer>();

        // Create a mock IThreadRepository
        var mockThreadRepository = new Mock<IThreadRepository>();

        // Create the ThreadEvaluator with proper dependencies
        var threadEvaluator = new ThreadEvaluator(
            logger: mockLogger,
            threadRepository: mockThreadRepository.Object,
            chatClient: mockChatClient,
            tracer: mockTracer
        );

        // Create a test thread model
        var thread = new ThreadModel(
            Id: testCase.ThreadId,
            Title: $"Test Thread {testCase.ThreadId}",
            StartMessage: null,
            LastMessage: null,
            CreatedTimestamp: DateTime.UtcNow.AddHours(-1),
            ModifiedTimestamp: DateTime.UtcNow,
            FeatureConfig: FeatureConfigModel.Default with
            {
                AutoHandoffEnabled = testCase.AutoHandoffEnabled
            },
            Source: ThreadSource.Agent
        );

        // Verify we have parsed chat messages correctly before calling the evaluator
        Assert.IsTrue(testCase.ChatMessages.Count > 0, "Should have parsed at least one chat message from the trace file");
        Assert.IsNotNull(testCase.ThreadId, "Should have extracted a thread ID");
        Assert.IsNotNull(testCase.StartAgent, "Should have a start agent specified");

        // Output all chat messages content for debugging
        TestContext.WriteLine($"=== Chat Messages for Thread {testCase.ThreadId} ===");
        TestContext.WriteLine($"Total Messages: {testCase.ChatMessages.Count}");
        TestContext.WriteLine($"Start Agent: {testCase.StartAgent}");
        TestContext.WriteLine("");

        // Now call the actual EvaluateHandoffsWithLLM method
        var evaluationResults = await threadEvaluator.EvaluateHandoffsWithLLM(
            thread,
            testCase.ChatMessages,
            testCase.StartAgent,
            testCase.AutoHandoffEnabled);

        // Display the evaluation results
        TestContext.WriteLine($"=== Evaluation Results ===");
        TestContext.WriteLine($"Number of evaluations: {evaluationResults.Count}");
        TestContext.WriteLine("");

        Assert.AreEqual(testCase.ExpectedHandoffSuccess.Length, evaluationResults.Count);

        for (int i = 0; i < evaluationResults.Count; i++)
        {
            var result = evaluationResults[i];
            TestContext.WriteLine($"Handoff Chat {i + 1}:\n{result.HandoffChat}");
            TestContext.WriteLine("");
            TestContext.WriteLine($"Evaluation {i + 1}:");
            TestContext.WriteLine($"  Handoffs Are Correct: {result.HandoffsAreCorrect}");
            TestContext.WriteLine($"  Error Explanation: {result.ErrorExplanation}");
            TestContext.WriteLine("");

            var expectedResult = testCase.ExpectedHandoffSuccess[i];
            if (expectedResult is not null)
            {
                Assert.AreEqual(expectedResult, result.HandoffsAreCorrect,
                    $"Failed evaluation #{i + 1} for {testCase.TraceFileName}. " +
                    $"Expected Evaluation: {expectedResult}. Prompt Evaluation: {result.HandoffsAreCorrect}");
            }
        }

        // Test passes if we successfully called the evaluation method without exceptions
        TestContext.WriteLine($"Successfully evaluated handoffs for thread {testCase.ThreadId} with {testCase.ChatMessages.Count} messages, starting with agent {testCase.StartAgent}");
    }

    public sealed record HandOffExpectation(
        string TraceFileName,
        bool AutoHandoffEnabled,
        bool?[] ExpectedHandoffSuccess);

    public sealed record HandOffEvaluationTestCase(
        string TraceFileName,
        Guid ThreadId,
        string StartAgent,
        bool AutoHandoffEnabled,
        IReadOnlyList<ChatMessage> ChatMessages,
        bool?[] ExpectedHandoffSuccess);
}
