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

    public static IEnumerable<object[]> HandOffTestCases => HandOffInputs.Select(i => new object[] { i });

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
    }

    #endregion
}
