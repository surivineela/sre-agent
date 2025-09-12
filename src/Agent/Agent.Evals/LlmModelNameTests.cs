using Agent.Core.Configuration;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;

namespace Agent.Evals;

[TestClass]
public class LlmModelNameTests
{
    private static TestHost? _testHost;

    private static async ValueTask<TestHost> GetTestHostAsync()
    {
        _testHost ??= await TestHelpers.InitializeTestHost();
        return _testHost;
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TestAgentWithGpt5Model()
    {
        var testHost = await GetTestHostAsync();
        var yamlContent = CreateTestAgentYaml(llmModelName: LlmModels.Gpt5);
        var agentDescriptor = ParseYamlToAgentDescriptor(yamlContent);

        TestContext.WriteLine($"Testing agent with LLM model: {LlmModels.Gpt5}");
        TestContext.WriteLine($"YAML Content:\n{yamlContent}");

        var agent = testHost.AgentFactory.LoadAgentFromDescriptor(agentDescriptor, isCustomAgent: true);
        var chatClient = agent.GetChatClient(testHost.RunConfig);
        var clientMetadata = chatClient.GetRequiredService<ChatClientMetadata>();

        ValidateClientMetadata(clientMetadata, LlmModels.Gpt5);

        TestContext.WriteLine($"✅ Agent loaded successfully with LLM model: {clientMetadata!.DefaultModelId}");
        TestContext.WriteLine($"✅ Provider name: {clientMetadata.ProviderName}");
        TestContext.WriteLine($"✅ Agent name: {agent.Name}");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Test Agent Without LLM Model Name")]
    [DataRow("", DisplayName = "Test Agent With Empty LLM Model Name")]
    public async Task TestAgentWithoutSpecificLlmModelName(string? llmModelName)
    {
        var testHost = await GetTestHostAsync();
        var openAISettings = testHost.Host.Services.GetRequiredService<OpenAISettings>();
        var yamlContent = CreateTestAgentYaml(llmModelName: llmModelName);
        var agentDescriptor = ParseYamlToAgentDescriptor(yamlContent);

        var testCase = llmModelName == null ? "null" : "empty";
        TestContext.WriteLine($"Testing agent with {testCase} LLM model name");
        TestContext.WriteLine($"Expected to use default from OpenAISettings: {openAISettings.LLMDeploymentName}");
        TestContext.WriteLine($"YAML Content:\n{yamlContent}");

        var agent = testHost.AgentFactory.LoadAgentFromDescriptor(agentDescriptor, isCustomAgent: true);
        var chatClient = agent.GetChatClient(testHost.RunConfig);
        var clientMetadata = chatClient.GetRequiredService<ChatClientMetadata>();

        ValidateClientMetadata(clientMetadata, openAISettings.LLMDeploymentName);
    }

    [TestMethod]
    public async Task TestAgentWithUnknownLlmModelName()
    {
        var testHost = await GetTestHostAsync();
        var unknownModel = "unknown-model-xyz";
        var yamlContent = CreateTestAgentYaml(llmModelName: unknownModel);
        var agentDescriptor = ParseYamlToAgentDescriptor(yamlContent);

        TestContext.WriteLine($"Testing agent with unknown LLM model: {unknownModel}");
        TestContext.WriteLine($"YAML Content:\n{yamlContent}");

        var exception = Assert.ThrowsExactly<Exception>(() =>
        {
            var agent = testHost.AgentFactory.LoadAgentFromDescriptor(agentDescriptor, isCustomAgent: true);
        });

        Assert.IsTrue(exception.Message.Contains("unsupported model deployment"));
        Assert.IsTrue(exception.Message.Contains(unknownModel));
        TestContext.WriteLine($"✅ Exception thrown as expected: {exception.Message}");
    }

    private static string CreateTestAgentYaml(string? llmModelName)
    {
        var agentName = $"test-agent-{Guid.NewGuid().ToString()[..8]}";

        var yaml = $@"name: {agentName}

system_prompt: |
  You are a test agent for validating LLM model configuration.
  This agent is used in integration tests to verify the llm_model_name feature works correctly.";

        // Only add llm_model_name if it's not null
        if (llmModelName != null)
        {
            yaml += $@"

llm_model_name: {llmModelName}";
        }

        yaml += @"

tools: []
handoffs: []
common_prompts: []
common_tools: []
agents_as_tools: []
orchestration_start_agents: []
next_agent_mappings: []";

        return yaml;
    }

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    private static YamlAgentDescriptor ParseYamlToAgentDescriptor(string yamlContent)
    {
        return YamlDeserializer.Deserialize<YamlAgentDescriptor>(yamlContent);
    }

    private static void ValidateClientMetadata(ChatClientMetadata clientMetadata, string expectedModelId)
    {
        Assert.IsNotNull(clientMetadata.DefaultModelId, "Default model ID should be set");
        Assert.AreEqual(expectedModelId, clientMetadata.DefaultModelId,
            $"Expected model ID '{expectedModelId}' but got '{clientMetadata.DefaultModelId}'");
        Assert.IsNotNull(clientMetadata.ProviderName, "Provider name should be set");
    }
}
