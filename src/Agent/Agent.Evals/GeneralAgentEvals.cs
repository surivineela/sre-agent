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

        // Use the model input from the test data
        var modelInput = testCase.ModelInput.ToList();

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

                var functionsMatch = expected.Name == actual.Name;
                TestContext.WriteLine($"Function call {i + 1}: {(functionsMatch ? "✅" : "❌")} Expected: {expected.Name}, Actual: {actual.Name}");

                if (functionsMatch)
                {
                    // Compare arguments
                    var expectedArgs = JsonSerializer.Serialize(expected.Arguments, new JsonSerializerOptions { WriteIndented = false });
                    var actualArgs = JsonSerializer.Serialize(actual.Arguments, new JsonSerializerOptions { WriteIndented = false });
                    var argsMatch = expectedArgs == actualArgs;
                    TestContext.WriteLine($"  Arguments match: {(argsMatch ? "✅" : "❌")}");

                    if (!argsMatch)
                    {
                        TestContext.WriteLine($"  Expected args: {expectedArgs}");
                        TestContext.WriteLine($"  Actual args: {actualArgs}");
                    }
                }
            }
            else if (i < expectedFunctionCalls.Count)
            {
                TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: {expectedFunctionCalls[i].Name}, Actual: (missing)");
            }
            else
            {
                TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: (missing), Actual: {actualFunctionCalls[i].Name}");
            }
        }

        // Basic assertions to make the test pass/fail
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsTrue(response.Messages.Count > 0, "Response should contain at least one message");

        TestContext.WriteLine($"\n=== TEST COMPLETED ===");
        TestContext.WriteLine("Use the console output above to analyze the differences between expected and actual behavior.");
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
            Assert.AreEqual(expectedFunctionCalls[i].Name, actualFunctionCalls[i].Name,
                $"Function call {i + 1}: Expected {expectedFunctionCalls[i].Name} but got {actualFunctionCalls[i].Name}");
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

            Assert.AreEqual(expectedCall.Name, actualCall.Name,
                $"Handoff function call {i + 1}: Expected {expectedCall.Name} but got {actualCall.Name}");

            // For handoffs, we expect the function name to start with "transfer_to_"
            Assert.IsTrue(actualCall.Name.StartsWith("transfer_to_"),
                $"Expected handoff function to start with 'transfer_to_' but got {actualCall.Name}");
        }
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
