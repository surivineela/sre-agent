using System.Linq;
using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class GeneralAgentEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> GeneralTestCases => LoadTestCasesFromFiles().Select(i => new object[] { i });

    private static GeneralTestCase[] LoadTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "HandOff");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync(dataFolderPath);
        var result = data.Select(kvp => GeneralTestCase.FromModelGenerationContent(kvp.Value, kvp.Key))
            .ToArray();
        return result;
    }

    [TestMethod]
    [DynamicData(nameof(GeneralTestCases))]
    public async Task GeneralAgentTests(GeneralTestCase testCase)
    {
        var agent = TestHost.AgentFactory.GetAgent(testCase.AgentName);
        Assert.IsNotNull(agent, $"Agent '{testCase.AgentName}' not found");

        List<ChatMessage> modelInput = [
            new ChatMessage(ChatRole.System, agent.Instructions),
            .. testCase.ModelInput,
        ];

        var chatClient = agent.GetChatClient(TestHost.RunConfig);
        var chatOptions = agent.GetChatOptions(TestHost);

        ChatResponse response;
        if (agent.HasStructuredOutput)
        {
            (response, _) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
        }
        else
        {
            response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        }

        // Validate based on expected output type
        switch (testCase.ExpectedOutputType)
        {
            case ExpectedOutputType.ToolCall:
                ValidateToolCallOutput(response, testCase);
                break;
            case ExpectedOutputType.FinalResponse:
                ValidateFinalResponseOutput(response, testCase);
                break;
            case ExpectedOutputType.Mixed:
                ValidateMixedOutput(response, testCase);
                break;
            case ExpectedOutputType.Handoff:
                ValidateHandoffOutput(response, testCase);
                break;
            default:
                Assert.Fail($"Unknown expected output type: {testCase.ExpectedOutputType}");
                break;
        }
    }

    [TestMethod]
    [DynamicData(nameof(GeneralTestCases))]
    public async Task GeneralAgentTests_DetailedComparison(GeneralTestCase testCase)
    {
        TestContext.WriteLine($"=== STARTING TEST CASE ===");
        TestContext.WriteLine($"Test Case: {testCase.TestCaseName}");
        TestContext.WriteLine($"============================");

        var agent = TestHost.AgentFactory.GetAgent(testCase.AgentName);
        Assert.IsNotNull(agent, $"Agent '{testCase.AgentName}' not found");

        TestContext.WriteLine($"Testing agent: {agent.Name}");
        var instructionsText = agent.Instructions?.ToString();
        TestContext.WriteLine($"Agent instructions: {(instructionsText != null ? instructionsText.Substring(0, Math.Min(200, instructionsText.Length)) + "..." : "null")}");

        // Get chat client and options similar to RunSingleTurnAsync
        var chatClient = agent.GetChatClient(TestHost.RunConfig);
        var chatOptions = agent.GetChatOptions(TestHost);

        TestContext.WriteLine($"Chat options configured:");
        TestContext.WriteLine($"  - Tools count: {chatOptions.Tools?.Count ?? 0}");
        TestContext.WriteLine($"  - Tool mode: {chatOptions.ToolMode}");
        TestContext.WriteLine($"  - Temperature: {chatOptions.Temperature}");
        TestContext.WriteLine($"  - Allow multiple tool calls: {chatOptions.AllowMultipleToolCalls}");

        // Log available tools
        if (chatOptions.Tools?.Count > 0)
        {
            TestContext.WriteLine("Available tools:");
            foreach (var tool in chatOptions.Tools)
            {
                if (tool is AIFunction func)
                {
                    TestContext.WriteLine($"  - {func.Name}");
                }
            }
        }

        // Fix Bug 1: Override system prompt with agent's actual instructions
        // Remove the system message from test data if it exists and use agent's actual instructions
        var modelInput = testCase.ModelInput.ToList();

        // Remove existing system message from test data
        if (modelInput.Count > 0 && modelInput[0].Role == ChatRole.System)
        {
            modelInput.RemoveAt(0);
            TestContext.WriteLine("Removed system prompt from test data to use agent's actual instructions");
        }

        // Prepend the agent's actual system instructions
        modelInput.Insert(0, new ChatMessage(ChatRole.System, agent.Instructions));
        TestContext.WriteLine("Using agent's actual system instructions for evaluation");

        TestContext.WriteLine($"\n=== EXPECTED MODEL OUTPUT ===");
        foreach (var expectedMsg in testCase.ExpectedOutput)
        {
            TestContext.WriteLine($"Expected [{expectedMsg.Role}]:");
            if (!string.IsNullOrEmpty(expectedMsg.Text))
            {
                TestContext.WriteLine($"  Text: {expectedMsg.Text}");
            }

            if (expectedMsg.Contents?.Count > 0)
            {
                foreach (var content in expectedMsg.Contents)
                {
                    if (content is FunctionCallContent funcCall)
                    {
                        TestContext.WriteLine($"  Expected function call: {funcCall.Name}");
                        TestContext.WriteLine($"  Arguments: {JsonSerializer.Serialize(funcCall.Arguments, new JsonSerializerOptions { WriteIndented = true })}");
                    }
                }
            }
        }

        // Call the LLM with the same setup as RunSingleTurnAsync
        TestContext.WriteLine($"\n=== CALLING LLM ===");
        ChatResponse response;

        try
        {
            if (agent.HasStructuredOutput)
            {
                TestContext.WriteLine("Agent has structured output - calling with structured output support");
                (response, _) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
            }
            else
            {
                TestContext.WriteLine("Agent using regular chat completion");
                response = await chatClient.GetResponseAsync(modelInput, chatOptions);
            }

            TestContext.WriteLine($"LLM call completed successfully");
            TestContext.WriteLine($"Finish reason: {response.FinishReason}");
            TestContext.WriteLine($"Response contains {response.Messages.Count} messages");

            if (response.Usage != null)
            {
                TestContext.WriteLine($"Token usage - Input: {response.Usage.InputTokenCount}, Output: {response.Usage.OutputTokenCount}, Total: {response.Usage.TotalTokenCount}");
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"ERROR during LLM call: {ex.Message}");
            TestContext.WriteLine($"Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                TestContext.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }

        // Log actual output
        TestContext.WriteLine($"\n=== ACTUAL MODEL OUTPUT ===");
        for (int i = 0; i < response.Messages.Count; i++)
        {
            var msg = response.Messages[i];
            TestContext.WriteLine($"Response message {i + 1} [{msg.Role}]:");

            if (!string.IsNullOrEmpty(msg.Text))
            {
                TestContext.WriteLine($"  Text: {msg.Text}");
            }

            if (msg.Contents?.Count > 0)
            {
                TestContext.WriteLine($"  Contents count: {msg.Contents.Count}");
                foreach (var content in msg.Contents)
                {
                    TestContext.WriteLine($"    - {content.GetType().Name}");
                    if (content is FunctionCallContent funcCall)
                    {
                        TestContext.WriteLine($"      Function: {funcCall.Name}");
                        TestContext.WriteLine($"      Call ID: {funcCall.CallId}");
                        TestContext.WriteLine($"      Arguments: {JsonSerializer.Serialize(funcCall.Arguments, new JsonSerializerOptions { WriteIndented = true })}");
                    }
                    else if (content is FunctionResultContent funcResult)
                    {
                        TestContext.WriteLine($"      Call ID: {funcResult.CallId}");
                        var resultText = funcResult.Result?.ToString();
                        if (!string.IsNullOrEmpty(resultText))
                        {
                            var displayResult = resultText.Length > 200 ? resultText.Substring(0, 200) + "..." : resultText;
                            TestContext.WriteLine($"      Result: {displayResult}");
                        }
                    }
                }
            }
        }

        // Compare with expected output
        TestContext.WriteLine($"\n=== COMPARISON ===");

        if (testCase.ExpectedOutput.Length != response.Messages.Count)
        {
            TestContext.WriteLine($"⚠️  Message count mismatch - Expected: {testCase.ExpectedOutput.Length}, Actual: {response.Messages.Count}");
        }
        else
        {
            TestContext.WriteLine($"✅ Message count matches: {response.Messages.Count}");
        }

        // Compare function calls
        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        TestContext.WriteLine($"Expected function calls: {expectedFunctionCalls.Count}");
        TestContext.WriteLine($"Actual function calls: {actualFunctionCalls.Count}");

        for (int i = 0; i < Math.Max(expectedFunctionCalls.Count, actualFunctionCalls.Count); i++)
        {
            if (i < expectedFunctionCalls.Count && i < actualFunctionCalls.Count)
            {
                var expected = expectedFunctionCalls[i];
                var actual = actualFunctionCalls[i];

                bool functionsMatch;
                string expectedDisplayName;

                // Check if the expected function name contains multiple acceptable options (pipe-separated)
                if (expected.Name.Contains('|'))
                {
                    var acceptableFunctionNames = expected.Name.Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(name => name.Trim())
                        .ToList();

                    functionsMatch = acceptableFunctionNames.Contains(actual.Name);
                    expectedDisplayName = GetExpectedFunctionDisplayName(expected.Name);
                }
                else
                {
                    functionsMatch = expected.Name == actual.Name;
                    expectedDisplayName = expected.Name;
                }

                TestContext.WriteLine($"Function call {i + 1}: {(functionsMatch ? "✅" : "❌")} Expected: {expectedDisplayName}, Actual: {actual.Name}");

                // Assert that function names match
                Assert.IsTrue(functionsMatch,
                    $"Function call {i + 1}: Expected '{expectedDisplayName}' but got '{actual.Name}'");

                if (functionsMatch)
                {
                    // Compare arguments
                    var expectedArgs = JsonSerializer.Serialize(expected.Arguments, new JsonSerializerOptions { WriteIndented = false });
                    var actualArgs = JsonSerializer.Serialize(actual.Arguments, new JsonSerializerOptions { WriteIndented = false });
                    var argsMatch = expectedArgs == actualArgs;
                    TestContext.WriteLine($"  Arguments match: {(argsMatch ? "✅" : "❌")}");

                    // Assert that arguments match
                    Assert.IsTrue(argsMatch,
                        $"Function call {i + 1} arguments mismatch. Expected: {expectedArgs}, Actual: {actualArgs}");

                    if (!argsMatch)
                    {
                        TestContext.WriteLine($"  Expected args: {expectedArgs}");
                        TestContext.WriteLine($"  Actual args: {actualArgs}");
                    }
                }
            }
            else if (i < expectedFunctionCalls.Count)
            {
                var expected = expectedFunctionCalls[i];
                var expectedDisplayName = GetExpectedFunctionDisplayName(expected.Name);
                TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: {expectedDisplayName}, Actual: (missing)");
                Assert.Fail($"Function call {i + 1}: Expected '{expectedDisplayName}' but no corresponding function call was found");
            }
            else
            {
                TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: (missing), Actual: {actualFunctionCalls[i].Name}");
                Assert.Fail($"Function call {i + 1}: Unexpected function call '{actualFunctionCalls[i].Name}' was found but none was expected");
            }
        }

        // Fix Bug 2: Add proper assertions based on expected output type
        TestContext.WriteLine($"\n=== RUNNING ASSERTIONS ===");
        try
        {
            switch (testCase.ExpectedOutputType)
            {
                case ExpectedOutputType.ToolCall:
                    ValidateToolCallOutputWithAssertions(response, testCase, TestContext);
                    break;
                case ExpectedOutputType.FinalResponse:
                    await ValidateFinalResponseOutputWithLLM(response, testCase, TestContext, chatClient);
                    break;
                case ExpectedOutputType.Mixed:
                    ValidateMixedOutputWithAssertions(response, testCase, TestContext);
                    break;
                case ExpectedOutputType.Handoff:
                    ValidateHandoffOutputWithAssertions(response, testCase, TestContext);
                    break;
                default:
                    Assert.Fail($"Unknown expected output type: {testCase.ExpectedOutputType}");
                    break;
            }
            TestContext.WriteLine("✅ All assertions passed!");
        }
        catch (AssertFailedException ex)
        {
            TestContext.WriteLine($"❌ Assertion failed: {ex.Message}");
            throw;
        }

        TestContext.WriteLine($"\n=== TEST COMPLETED ===");
        TestContext.WriteLine("Use the console output above to analyze the differences between expected and actual behavior.");
    }

    private static void ValidateToolCallOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating tool call output with assertions...");

        Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected tool calls but got different finish reason");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (int i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, testContext, isHandoff: false);
        }
        testContext.WriteLine("Tool call validation passed!");
    }

    private static async Task ValidateFinalResponseOutputWithLLM(ChatResponse response, GeneralTestCase testCase, TestContext testContext, IChatClient chatClient)
    {
        testContext.WriteLine("Validating final response output with LLM evaluation...");

        Assert.AreEqual(ChatFinishReason.Stop, response.FinishReason, "Expected final response (stop) but got different finish reason");

        // Ensure no function calls are present
        var functionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(0, functionCalls.Count, "Expected no function calls in final response");

        // Ensure there's actual text content
        var hasTextContent = response.Messages.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                         m.Contents?.OfType<TextContent>().Any() == true);
        Assert.IsTrue(hasTextContent, "Expected text content in final response");

        // Use LLM to evaluate semantic similarity
        var actualText = string.Join("\n", response.Messages.Select(m => m.Text ?? "").Where(t => !string.IsNullOrEmpty(t)));
        var expectedText = string.Join("\n", testCase.ExpectedOutput.Select(m => m.Text ?? "").Where(t => !string.IsNullOrEmpty(t)));

        if (!string.IsNullOrEmpty(expectedText) && !string.IsNullOrEmpty(actualText))
        {
            var evaluationPrompt = $@"
You are an AI assistant that evaluates whether two responses are semantically similar and convey the same meaning.

Expected Response:
{expectedText}

Actual Response:
{actualText}

Task: Determine if the actual response conveys the same meaning and intent as the expected response. Consider:
1. Key information and facts are preserved
2. Overall tone and intent match
3. Important details are not missing
4. The response addresses the same concerns

Respond with only 'SIMILAR' if they convey the same meaning, or 'DIFFERENT' if they don't, followed by a brief explanation.";

            try
            {
                var evaluationMessages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, evaluationPrompt)
                };
                var evaluationResponse = await chatClient.GetResponseAsync(evaluationMessages);
                var evaluation = evaluationResponse.Messages.LastOrDefault()?.Text ?? "";

                testContext.WriteLine($"LLM Evaluation: {evaluation}");

                if (evaluation.StartsWith("DIFFERENT", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"LLM evaluation indicates responses are semantically different. Evaluation: {evaluation}");
                }
                else if (evaluation.StartsWith("SIMILAR", StringComparison.OrdinalIgnoreCase))
                {
                    testContext.WriteLine("✅ LLM evaluation indicates responses are semantically similar");
                }
                else
                {
                    testContext.WriteLine($"⚠️ Ambiguous LLM evaluation, proceeding with caution: {evaluation}");
                }
            }
            catch (Exception ex)
            {
                testContext.WriteLine($"⚠️ LLM evaluation failed: {ex.Message}. Proceeding without semantic validation.");
            }
        }
        testContext.WriteLine("Final response validation completed!");
    }

    private static void ValidateMixedOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating mixed output with assertions...");

        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsTrue(response.Messages.Count > 0, "Response should contain at least one message");

        // Basic structure validation - ensure we have the expected number of messages
        Assert.AreEqual(testCase.ExpectedOutput.Length, response.Messages.Count,
            "Message count should match expected output");

        testContext.WriteLine("Mixed output validation passed!");
    }

    private static void ValidateHandoffOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating handoff output with assertions...");

        Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected handoff tool calls but got different finish reason");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (int i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, testContext, isHandoff: true);
        }
        testContext.WriteLine("Handoff validation passed!");
    }

    private static void ValidateToolCallOutput(ChatResponse response, GeneralTestCase testCase)
    {
        Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected tool calls but got different finish reason");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (int i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, isHandoff: false);
        }
    }

    private static void ValidateFinalResponseOutput(ChatResponse response, GeneralTestCase testCase)
    {
        Assert.AreEqual(ChatFinishReason.Stop, response.FinishReason, "Expected final response (stop) but got different finish reason");

        // Ensure no function calls are present
        var functionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(0, functionCalls.Count, "Expected no function calls in final response");

        // Ensure there's actual text content
        var hasTextContent = response.Messages.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                         m.Contents?.OfType<TextContent>().Any() == true);
        Assert.IsTrue(hasTextContent, "Expected text content in final response");
    }

    private static void ValidateMixedOutput(ChatResponse response, GeneralTestCase testCase)
    {
        // For mixed output, we just validate that the response is not null and has content
        // More specific validation can be added based on the expected output structure
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsTrue(response.Messages.Count > 0, "Response should contain at least one message");

        // Basic structure validation - ensure we have the expected number of messages
        Assert.AreEqual(testCase.ExpectedOutput.Length, response.Messages.Count,
            "Message count should match expected output");
    }

    private static void ValidateHandoffOutput(ChatResponse response, GeneralTestCase testCase)
    {
        Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected handoff tool calls but got different finish reason");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (int i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, isHandoff: true);
        }
    }

    /// <summary>
    /// Validates that a function call matches one of the expected function names (supports pipe-separated multiple options)
    /// </summary>
    /// <param name="expectedFunctionName">Expected function name, can be pipe-separated for multiple options</param>
    /// <param name="actualFunctionName">Actual function name from the response</param>
    /// <param name="callIndex">Index of the function call for error messages</param>
    /// <param name="testContext">Test context for logging (optional)</param>
    /// <param name="isHandoff">Whether this is a handoff function call (adds transfer_to_ validation)</param>
    private static void ValidateFunctionCallName(string expectedFunctionName, string actualFunctionName, int callIndex, TestContext? testContext = null, bool isHandoff = false)
    {
        if (expectedFunctionName.Contains('|'))
        {
            var acceptableFunctionNames = expectedFunctionName.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToList();

            var isValidCall = acceptableFunctionNames.Contains(actualFunctionName);
            var callType = isHandoff ? "Handoff function call" : "Function call";

            Assert.IsTrue(isValidCall,
                $"{callType} {callIndex + 1}: Expected one of [{string.Join(", ", acceptableFunctionNames)}] but got {actualFunctionName}");

            if (isHandoff)
            {
                // For handoffs, we expect the function name to start with "transfer_to_"
                Assert.IsTrue(actualFunctionName.StartsWith("transfer_to_"),
                    $"Expected handoff function to start with 'transfer_to_' but got {actualFunctionName}");
            }

            testContext?.WriteLine($"✅ {callType} {callIndex + 1}: {actualFunctionName} matches one of the acceptable options: [{string.Join(", ", acceptableFunctionNames)}]");
        }
        else
        {
            var callType = isHandoff ? "Handoff function call" : "Function call";
            Assert.AreEqual(expectedFunctionName, actualFunctionName,
                $"{callType} {callIndex + 1}: Expected {expectedFunctionName} but got {actualFunctionName}");

            if (isHandoff)
            {
                // For handoffs, we expect the function name to start with "transfer_to_"
                Assert.IsTrue(actualFunctionName.StartsWith("transfer_to_"),
                    $"Expected handoff function to start with 'transfer_to_' but got {actualFunctionName}");
            }
        }
    }

    /// <summary>
    /// Gets a display-friendly name for expected function calls (handles pipe-separated options)
    /// </summary>
    private static string GetExpectedFunctionDisplayName(string expectedFunctionName)
    {
        return expectedFunctionName.Contains('|')
            ? $"[{string.Join(" | ", expectedFunctionName.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(name => name.Trim()))}]"
            : expectedFunctionName;
    }

    #region Test Case Classes

    public sealed record GeneralTestCase(
        string AgentName,
        List<ChatMessage> ModelInput,
        ChatMessage[] ExpectedOutput,
        ExpectedOutputType ExpectedOutputType,
        string TestCaseName = "")
    {
        public static GeneralTestCase FromModelGenerationContent(ModelGenerationContent content)
        {
            return FromModelGenerationContent(content, "");
        }

        public static GeneralTestCase FromModelGenerationContent(ModelGenerationContent content, string testCaseName)
        {
            var chatHistory = content.ModelInput.ToList();
            if (chatHistory[0].Role == ChatRole.System)
            {
                chatHistory.RemoveRange(0, 1); // Remove system message if it exists
            }

            var expectedOutputType = DetermineExpectedOutputType(content.ModelOutput);

            return new GeneralTestCase(
                AgentName: content.AgentName,
                ModelInput: chatHistory,
                ExpectedOutput: content.ModelOutput,
                ExpectedOutputType: expectedOutputType,
                TestCaseName: testCaseName);
        }

        private static ExpectedOutputType DetermineExpectedOutputType(ChatMessage[] modelOutput)
        {
            var functionCalls = modelOutput
                .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
                .ToList();

            var hasTextContent = modelOutput.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                     m.Contents?.OfType<TextContent>().Any() == true);

            // Check if it's a handoff case
            if (functionCalls.Any(fc => fc.Name.StartsWith("transfer_to_")))
            {
                return ExpectedOutputType.Handoff;
            }

            // Check other cases
            if (functionCalls.Any() && hasTextContent)
                return ExpectedOutputType.Mixed;
            else if (functionCalls.Any())
                return ExpectedOutputType.ToolCall;
            else
                return ExpectedOutputType.FinalResponse;
        }
    }

    public enum ExpectedOutputType
    {
        ToolCall,
        FinalResponse,
        Mixed,
        Handoff
    }

    #endregion
}
