// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

[TestClass]
public class ResourceDiscoveryAgentEvals
{
    private static async Task<TestHost> GetTestHostAsync() => await TestHelpers.InitializeTestHost();

    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> TestCases => LoadTestCasesFromFiles().Select(i => new object[] { i });

    private static ModelGenerationContent[] LoadTestCasesFromFiles()
    {
        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "ResourceDiscoveryAgent");
        var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFiles(dataFolderPath);
        return data.Values.ToArray();
    }

    [TestMethod]
    [DynamicData(nameof(TestCases))]
    public async Task ResourceDiscoveryTests(ModelGenerationContent testData)
    {
        var startAgent = (await GetTestHostAsync()).AgentFactory.GetAgent(testData.AgentName);
        var output = testData.ModelOutput.Single();
        var expectedText = output.Contents.OfType<TextContent>().SingleOrDefault();
        var expectStructuredOutput = expectedText?.Text is not null
            ? JsonSerializer.Deserialize<DefaultAgentOutput>(expectedText.Text, jsonOptions)
            : null;

        var expectedFunctionCall = output.Contents.OfType<FunctionCallContent>().SingleOrDefault();

        List<ChatMessage> modelInput = [
            new ChatMessage(ChatRole.System, startAgent.Instructions.ToString()),
            .. testData.ModelInput[1..],
        ];

        var chatClient = startAgent.GetChatClient((await GetTestHostAsync()).RunConfig);
        var chatOptions = startAgent.GetChatOptions(await GetTestHostAsync());

        ChatResponse response;
        if (startAgent.HasStructuredOutput)
        {
            (response, _) = await chatClient.GetResponseAsync(modelInput, startAgent.OutputType, chatOptions);
        }
        else
        {
            response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        }

        var actualText = response.Messages.Last().Contents.OfType<TextContent>().SingleOrDefault();
        var actualStructuredOutput = actualText?.Text is not null
            ? JsonSerializer.Deserialize<DefaultAgentOutput>(actualText.Text, jsonOptions)
            : null;
        Assert.IsNotNull(actualStructuredOutput);
        Assert.AreEqual(expectStructuredOutput?.State, actualStructuredOutput?.State);

        var actualFnCall = response.Messages.Last().Contents.OfType<FunctionCallContent>().SingleOrDefault();
        Assert.AreEqual(expectedFunctionCall?.Name, actualFnCall?.Name);
    }
}
