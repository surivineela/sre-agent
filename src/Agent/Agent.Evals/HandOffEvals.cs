using System.Linq;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class HandOffEvals
{
    private static TestHost TestHost { get; } = TestHelpers.InitializeTestHost();

    public static readonly HandOffTestCase[] HandOffInputs = [
        new(
            userMessage: "Create a simple deployment nginx with image nginx:latest in namespace default in my AKS cluster, the AKS cluster resource id is `/subscriptions/ea2aa16c-c257-4359-aaea-ff2b0f3b3d10/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/prod-shopping-c1`",
            targetAgent: "aks_general_agent"),
    ];

    public static IEnumerable<object[]> HandOffTestCases => HandOffInputs.Concat(LoadTestCasesFromFiles()).Select(i => new object[] { i });

    private static HandOffTestCase[] LoadTestCasesFromFiles()
    {
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFilesAsync().GetAwaiter().GetResult();
        var result = data.Values.Select(HandOffTestCase.FromModelGenerationContent).ToArray();
        return result;
    }

    [TestMethod]
    [DynamicData(nameof(HandOffTestCases))]
    public async Task HandOffTests(HandOffTestCase handOffTest)
    {
        var startAgent = TestHost.AgentFactory.GetAgent(handOffTest.StartAgent);
        var targetAgent = TestHost.AgentFactory.GetAgent(handOffTest.TargetAgent);
        var targetAgentHandoff = Handoff<AgentContext>.DefaultToolName(targetAgent);

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
        Assert.AreEqual(targetAgentHandoff, fnCalls[0].Name);
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
            if (!functionName.StartsWith("transfer_to_"))
            {
                throw new InvalidOperationException($"Model output function call must be a handoff to an agent, but was: {functionName}.");
            }
            string targetAgent = functionName.Substring("transfer_to_".Length);

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
