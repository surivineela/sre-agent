using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class HandOffEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    public static IEnumerable<object[]> HandOffTestCases => LoadTestCasesFromFiles().Select(i => new object[] { i });

    private static HandOffTestCase[] LoadTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "HandOff");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFiles(dataFolderPath);
        var result = data.Values.Select(HandOffTestCase.FromModelGenerationContent).ToArray();
        return result;
    }

    [TestMethod]
    [DynamicData(nameof(HandOffTestCases))]
    public async Task HandOffTests(HandOffTestCase handOffTest)
    {
        var startAgent = TestHost.AgentFactory.GetAgent(handOffTest.StartAgent);

        // Handle pipe-separated multiple acceptable target agents
        string expectedHandoffPattern;
        if (handOffTest.TargetAgent.Contains('|'))
        {
            // For multiple options, create a pipe-separated list of expected function names
            var acceptableTargets = handOffTest.TargetAgent.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(target => target.Trim())
                .Select(target => Handoff<AgentContext>.DefaultToolName(TestHost.AgentFactory.GetAgent(target)))
                .ToList();
            expectedHandoffPattern = string.Join("|", acceptableTargets);
        }
        else
        {
            var targetAgent = TestHost.AgentFactory.GetAgent(handOffTest.TargetAgent);
            expectedHandoffPattern = Handoff<AgentContext>.DefaultToolName(targetAgent);
        }

        List<ChatMessage> modelInput = [
            new ChatMessage(ChatRole.System, startAgent.Instructions),
            .. handOffTest.ChatHistory,
        ];

        var chatClient = startAgent.GetChatClient(TestHost.RunConfig);
        var chatOptions = startAgent.GetChatOptions(TestHost);

        ChatResponse response;
        if (startAgent.HasStructuredOutput)
        {
            (response, _) = await chatClient.GetResponseAsync(modelInput, startAgent.OutputType, chatOptions);
        }
        else
        {
            response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        }

        Assert.AreEqual(ChatFinishReason.ToolCalls, response.FinishReason);
        var fnCalls = response.Messages.Last().Contents.OfType<FunctionCallContent>().ToArray();
        Assert.ContainsSingle(fnCalls);
        Assert.IsTrue(IsValidFunctionCall(expectedHandoffPattern, fnCalls[0].Name),
            $"Expected handoff to match pattern '{expectedHandoffPattern}' but got '{fnCalls[0].Name}'");
    }

    /// <summary>
    /// Validates that a function call matches one of the expected function names (supports pipe-separated multiple options)
    /// </summary>
    /// <param name="expectedFunctionName">Expected function name, can be pipe-separated for multiple options</param>
    /// <param name="actualFunctionName">Actual function name from the response</param>
    private static bool IsValidFunctionCall(string expectedFunctionName, string actualFunctionName)
    {
        if (expectedFunctionName.Contains('|'))
        {
            var acceptableFunctionNames = expectedFunctionName.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToList();

            return acceptableFunctionNames.Contains(actualFunctionName);
        }
        else
        {
            return expectedFunctionName == actualFunctionName;
        }
    }

    #region Test Case Classes

    public sealed record HandOffTestCase(
        string StartAgent,
        string TargetAgent,
        List<ChatMessage> ChatHistory)
    {
        public HandOffTestCase(
            string userMessage,
            string targetAgent) :
            this(
                StartAgent: "meta_agent",
                TargetAgent: targetAgent,
                ChatHistory: [new(ChatRole.User, userMessage)])
        {
        }

        public static HandOffTestCase FromModelGenerationContent(ModelGenerationContent content)
        {
            var outputMessage = content.ModelOutput.Single();
            if (outputMessage.Role != ChatRole.Assistant)
            {
                throw new InvalidOperationException("Model output must be from the assistant role.");
            }
            if (outputMessage.Contents.Count != 1 || outputMessage.Contents[0] is not FunctionCallContent functionCall)
            {
                throw new InvalidOperationException("Model output must contain a single function call content.");
            }

            string functionName = functionCall.Name;
            string targetAgent;

            // Handle pipe-separated multiple acceptable function calls
            if (functionName.Contains('|'))
            {
                // For multiple options, extract all target agents from the pipe-separated function names
                var functionNames = functionName.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .ToList();

                // Validate all function names start with "transfer_to_"
                foreach (var fname in functionNames)
                {
                    if (!fname.StartsWith("transfer_to_"))
                    {
                        throw new InvalidOperationException($"Model output function call must be a handoff to an agent, but was: {fname}.");
                    }
                }

                // Extract target agents and join them back with pipes
                var targetAgents = functionNames.Select(fname => fname.Substring("transfer_to_".Length)).ToList();
                targetAgent = string.Join(" | ", targetAgents);
            }
            else
            {
                if (!functionName.StartsWith("transfer_to_"))
                {
                    throw new InvalidOperationException($"Model output function call must be a handoff to an agent, but was: {functionName}.");
                }
                targetAgent = functionName.Substring("transfer_to_".Length);
            }

            var chatHistory = content.ModelInput.ToList();
            if (chatHistory[0].Role == ChatRole.System)
            {
                chatHistory.RemoveRange(0, 1); // Remove system message if it exists
            }

            return new HandOffTestCase(
                StartAgent: content.AgentName,
                TargetAgent: targetAgent,
                ChatHistory: chatHistory);
        }
    }

    #endregion
}
