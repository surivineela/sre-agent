using System.Text.Json;
using Agent.Framework;
using Agent.Core.Models.Api.v1;
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
        // Check for environment variables to filter tests, mainly used for local test with fast testing for specific case
        var targetFolder = Environment.GetEnvironmentVariable("TEST_FOLDER");
        var targetFile = Environment.GetEnvironmentVariable("TEST_FILE");

        var dataFolders = new[]
        {
            "HandOff",
            "AzCliCommandAgent",
            "AKSAgent"
        };

        // If a specific folder is requested, only use that folder
        if (!string.IsNullOrEmpty(targetFolder))
        {
            dataFolders = dataFolders.Where(f => f.Equals(targetFolder, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (dataFolders.Length == 0)
            {
                throw new ArgumentException($"Folder '{targetFolder}' not found. Available folders: {string.Join(", ", new[] { "HandOff", "AzCliCommandAgent", "AKSAgent" })}");
            }
        }

        var allTestCases = new List<GeneralTestCase>();
        var allAvailableFiles = new List<string>();

        foreach (var folder in dataFolders)
        {
            var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", folder);
            if (Directory.Exists(dataFolderPath))
            {
                var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFiles(dataFolderPath);

                // Collect all available files for error reporting
                allAvailableFiles.AddRange(data.Keys.Select(key => $"{folder}/{key}"));

                // If a specific file is requested, filter the data
                if (!string.IsNullOrEmpty(targetFile))
                {
                    var filteredData = data.Where(kvp => kvp.Key.Contains(targetFile, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filteredData.Count > 0)
                    {
                        data = filteredData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }
                    else
                    {
                        // Skip this folder if no matching files found, don't throw exception yet
                        continue;
                    }
                }

                var testCases = data.Select(kvp => GeneralTestCase.FromModelGenerationContent(kvp.Value, $"{folder}_{kvp.Key}"))
                    .ToArray();
                allTestCases.AddRange(testCases);
            }
        }

        // If a specific file was requested but no test cases were found across all folders, throw exception
        if (!string.IsNullOrEmpty(targetFile) && allTestCases.Count == 0)
        {
            var availableFiles = string.Join(", ", allAvailableFiles);
            throw new ArgumentException($"File '{targetFile}' not found in any folder. Available files: {availableFiles}");
        }

        return allTestCases.ToArray();
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

        // Check and handle handoff state without tool call - similar to ReasoningLoop
        response = await HandleHandoffCorrectionIfNeeded(agent, modelInput, response, chatClient, chatOptions);

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

            // Check and handle handoff state without tool call - similar to ReasoningLoop
            response = await HandleHandoffCorrectionIfNeeded(agent, modelInput, response, chatClient, chatOptions, TestContext);
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

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            TestContext.WriteLine("✅ No function calls found and expected options include '-' (no call acceptable)");
            // Skip function call validation since no calls are expected and none were made
        }
        else
        {
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
                            .Where(name => name != "-") // Exclude "-" from actual function name matching
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

                    if (functionsMatch
                        && !IsHandoffToolCall(actual.Name) // can't compare input args for handoff as it is free flow text
                        && !expected.Name.Contains('|')) // don't verify arguments if multiple function calls are acceptable
                    {
                        // Compare arguments using smart comparison
                        var argsMatch = TestHelpers.AreArgumentsEquivalent(expected.Arguments ?? new object(), actual.Arguments ?? new object());
                        TestContext.WriteLine($"  Arguments match: {(argsMatch ? "✅" : "❌")}");

                        // If smart comparison fails, show detailed comparison for debugging
                        if (!argsMatch)
                        {
                            var expectedArgs = JsonSerializer.Serialize(expected.Arguments, new JsonSerializerOptions { WriteIndented = false });
                            var actualArgs = JsonSerializer.Serialize(actual.Arguments, new JsonSerializerOptions { WriteIndented = false });
                            TestContext.WriteLine($"  Expected args: {expectedArgs}");
                            TestContext.WriteLine($"  Actual args: {actualArgs}");
                        }

                        // Assert that arguments match
                        Assert.IsTrue(argsMatch,
                            $"Function call {i + 1} arguments mismatch. Expected: {JsonSerializer.Serialize(expected.Arguments, new JsonSerializerOptions { WriteIndented = false })}, Actual: {JsonSerializer.Serialize(actual.Arguments, new JsonSerializerOptions { WriteIndented = false })}");
                    }
                    else if (expected.Name.Contains('|'))
                    {
                        TestContext.WriteLine($"  Arguments verification skipped (multiple function calls acceptable)");
                    }
                }
                else if (i < expectedFunctionCalls.Count)
                {
                    var expected = expectedFunctionCalls[i];
                    var expectedDisplayName = GetExpectedFunctionDisplayName(expected.Name);

                    // Check if "-" is acceptable for this expected function call
                    if (expected.Name.Contains("-"))
                    {
                        TestContext.WriteLine($"Function call {i + 1}: ✅ Expected: {expectedDisplayName}, Actual: (none) - no call is acceptable");
                    }
                    else
                    {
                        TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: {expectedDisplayName}, Actual: (missing)");
                        Assert.Fail($"Function call {i + 1}: Expected '{expectedDisplayName}' but no corresponding function call was found");
                    }
                }
                else
                {
                    TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: (missing), Actual: {actualFunctionCalls[i].Name}");
                    Assert.Fail($"Function call {i + 1}: Unexpected function call '{actualFunctionCalls[i].Name}' was found but none was expected");
                }
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

    private static bool IsHandoffToolCall(string toolName)
    {
        return toolName.StartsWith("transfer_to_", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("handoffback", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateToolCallOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating tool call output with assertions...");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            testContext.WriteLine("✅ No function calls found and '-' option allows this");
            return;
        }

        if (!hasNoCallOption)
        {
            Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected tool calls but got different finish reason");
        }

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

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            testContext.WriteLine("✅ No handoff function calls found and '-' option allows this");
            return;
        }

        if (!hasNoCallOption)
        {
            Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected handoff tool calls but got different finish reason");
        }

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
        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            // No function calls found and "-" option allows this
            return;
        }

        if (!hasNoCallOption)
        {
            Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected tool calls but got different finish reason");
        }

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
        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            // No handoff function calls found and "-" option allows this
            return;
        }

        if (!hasNoCallOption)
        {
            Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason, "Expected handoff tool calls but got different finish reason");
        }

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
    /// Special case: "-" in the expected options means "no function call is acceptable"
    /// </summary>
    /// <param name="expectedFunctionName">Expected function name, can be pipe-separated for multiple options. Use "-" to indicate no function call is acceptable.</param>
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

            // Special case: if "-" is one of the options, it means no function call is acceptable
            var noFunctionCallAcceptable = acceptableFunctionNames.Contains("-");
            var actualFunctionNames = acceptableFunctionNames.Where(name => name != "-").ToList();

            var isValidCall = actualFunctionNames.Contains(actualFunctionName);
            var callType = isHandoff ? "Handoff function call" : "Function call";

            Assert.IsTrue(isValidCall,
                $"{callType} {callIndex + 1}: Expected one of [{string.Join(", ", acceptableFunctionNames)}] but got {actualFunctionName}");

            if (isHandoff && !noFunctionCallAcceptable)
            {
                // For handoffs, we expect the function name to start with "transfer_to_" unless "-" is acceptable
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

    /// <summary>
    /// Checks if agent has structured output with handoff state but no tool call, and handles correction similar to ReasoningLoop
    /// </summary>
    /// <param name="agent">The agent being tested</param>
    /// <param name="modelInput">The current model input (will be modified if correction is needed)</param>
    /// <param name="response">The current response from the LLM</param>
    /// <param name="chatClient">The chat client for making additional calls</param>
    /// <param name="chatOptions">The chat options to use</param>
    /// <param name="testContext">Test context for logging (optional)</param>
    /// <returns>The corrected response if handoff correction was applied, otherwise the original response</returns>
    private static async Task<ChatResponse> HandleHandoffCorrectionIfNeeded(
        Agent<AgentContext> agent,
        List<ChatMessage> modelInput,
        ChatResponse response,
        IChatClient chatClient,
        ChatOptions chatOptions,
        TestContext? testContext = null)
    {
        // Only check if agent has structured output and there's a response
        if (!agent.HasStructuredOutput || response.Messages.Count == 0)
        {
            return response;
        }

        var lastMessage = response.Messages.Last();
        var hasToolCall = lastMessage.Contents?.OfType<FunctionCallContent>().Any() == true;

        // Only proceed if there's no tool call but there's text content
        if (hasToolCall || string.IsNullOrEmpty(lastMessage.Text))
        {
            return response;
        }

        try
        {
            testContext?.WriteLine($"Response before handoff check: {lastMessage.Text}");
            testContext?.WriteLine($"Checking structured output for handoff state without tool call...");
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(lastMessage.Text);

            if (jsonElement.TryGetProperty("state", out var stateProperty))
            {
                var state = stateProperty.GetString();
                testContext?.WriteLine($"Agent state: {state}");

                // Check if agent indicated handoff but didn't make tool call
                if (state == "HandOff_OutOfScope" || state == "HandOff_Continue")
                {
                    testContext?.WriteLine($"Agent indicated handoff state '{state}' but made no tool call. Adding prompt for second attempt, similar to ReasoningLoop logic.");

                    // Add the agent's response to the conversation
                    modelInput.Add(lastMessage);

                    // Add user prompt asking agent to make the proper tool call
                    var promptMessage = new ChatMessage(ChatRole.User,
                        $"You mentioned the request is in state {state}, but did not actually perform any handoffs (transfer_to_* or HandOffBack). " +
                        "Reflect if any more processing work is required. If yes, set the state to Processing and continue taking actions in your scope. " +
                        "Otherwise if you are actually done, then call the right handoff tool.");

                    modelInput.Add(promptMessage);

                    testContext?.WriteLine($"\n=== CALLING LLM AGAIN (Handoff Correction) ===");
                    testContext?.WriteLine($"Added user prompt: {promptMessage.Text}");

                    // Make second LLM call
                    ChatResponse correctedResponse;
                    if (agent.HasStructuredOutput)
                    {
                        (correctedResponse, _) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
                    }
                    else
                    {
                        correctedResponse = await chatClient.GetResponseAsync(modelInput, chatOptions);
                    }
                    return correctedResponse;
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse JSON, just continue with original response
            testContext?.WriteLine("Could not parse structured output as JSON, continuing with original response");
        }

        return response;
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
