// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Models;
using Agent.Plugins;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Tests.Unit.Framework;

public class AgentFactoryTests
{
    private readonly Mock<ILogger<AgentFactory<AgentContext>>> _mockLogger;
    private readonly Mock<ILogger<ToolFactory<AgentContext>>> _mockToolFactoryLogger;
    private readonly Mock<ChatClientProvider> _mockChatClientProvider;
    private readonly Mock<IAgentModeConfigurator<AgentContext>> _mockAgentModeConfigurator;
    private readonly Mock<IExtensibilityLoader> _mockExtendedAgentRepository;
    private readonly Mock<IMcpConnectable> _mockMcpToolsRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceCollection _services;

    public AgentFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory<AgentContext>>>();
        _mockToolFactoryLogger = new Mock<ILogger<ToolFactory<AgentContext>>>();
        _mockChatClientProvider = new Mock<ChatClientProvider>(_serviceProvider);
        _mockAgentModeConfigurator = new Mock<IAgentModeConfigurator<AgentContext>>();
        _mockExtendedAgentRepository = new Mock<IExtensibilityLoader>();
        _mockMcpToolsRepository = new Mock<IMcpConnectable>();
        _mockMcpToolsRepository.Setup(m => m.GetAllFunctions()).Returns(new List<AIFunction>());

        // Setup mock extensibility loader to return empty lists
        _mockExtendedAgentRepository.Setup(x => x.LoadExtendedToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlToolDefinitionBase>());
        _mockExtendedAgentRepository.Setup(x => x.LoadExtendedAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlAgentDescriptor>());
        _mockExtendedAgentRepository.Setup(x => x.LoadExtendedCommonPromptsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlPromptDescriptor>());
        _mockExtendedAgentRepository.Setup(x => x.LoadExtendedCommonToolsListsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlCommonToolsDescriptor>());

        _services = new ServiceCollection();
        _services.AddSingleton(_mockLogger.Object);
        _services.AddSingleton(_mockToolFactoryLogger.Object);
        _services.AddTransient<TestTools>();
        _services.AddTransient<TestDataConnectorTools>();
        _services.AddTransient<TestSlackTools>();
        SetupServiceProviderWithHostEnvironmentAndConfiguration();
        _serviceProvider = _services.BuildServiceProvider();
    }

    private void SetupServiceProviderWithHostEnvironmentAndConfiguration()
    {
        var mockHostEnvironment = new Mock<IHostEnvironment>();
        mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        mockHostEnvironment.Setup(e => e.ApplicationName).Returns("TestApp");
        mockHostEnvironment.Setup(e => e.ContentRootPath).Returns("/test/root");

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"AppSettings:Core:Azure:Crawler:TenantId", "72f988bf-86f1-41af-91ab-2d7cd011db47"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Register the mocks to the existing ServiceCollection
        _services.AddSingleton(mockHostEnvironment.Object);
        _services.AddSingleton(configuration);

        _services.AddSingleton(new ExperimentalSettings
        {
            AutoHandoffToMeta = true,
            EnableHandoffReasoning = true,
        });

        // Add CoreSettings with DataConnectors for testing DataConnectorType conditions
        _services.AddSingleton(new CoreSettings
        {
            DataConnectors = new List<DataConnectorInstanceSettings>
            {
                new DataConnectorInstanceSettings
                {
                    Name = "TestTeamsConnector",
                    DataConnectorType = "Teams",
                    DataSource = "https://test.teams.com"
                }
            }
        });
    }

    [Fact]
    public async Task LoadsAgentsFromAssembly()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent1 = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent1);
        Assert.Equal("TestAgent1", agent1.Name);
        Assert.Contains(TestCommonPrompt.PromptText, agent1.Instructions);

        var agent2 = agentFactory.GetAgent("TestAgent2");
        Assert.NotNull(agent2);
        Assert.Equal("TestAgent2", agent2.Name);

        Assert.Contains(agent2.Name, agent1.Handoffs.Select(h => h.AgentName));
    }

    [Fact]
    public async Task LoadsAgentsFromYaml()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );
        await agentFactory.InitializeAsync();

        var agent1 = agentFactory.GetAgent("agent1");
        Assert.NotNull(agent1);
        Assert.Equal("agent1", agent1.Name);

        var agent2 = agentFactory.GetAgent("agent2");
        Assert.NotNull(agent2);
        Assert.Equal("agent2", agent2.Name);

        var prompt1Path = Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts", "prompt1.yaml");
        var prompt1Content = File.ReadAllText(prompt1Path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var prompt1 = deserializer.Deserialize<YamlPromptDescriptor>(prompt1Content);
        Assert.Contains(prompt1.Prompt, agent1.Instructions);

        Assert.Contains(agent2.Name, agent1.Handoffs.Select(h => h.AgentName));

        // Test that common tools are loaded
        Assert.Contains("TestTool1", agent1.FactoryTools);
        Assert.Contains("TestTool2", agent1.FactoryTools);
    }

    [Fact]
    public async Task YamlAgentLoadsOptionalToolsCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("YamlTestFeature", "On");
        try
        {
            var toolFactory = CreateToolFactory();

            var agentFactory = new AgentFactory<AgentContext>(
                logger: _mockLogger.Object,
                toolFactory: toolFactory,
                chatClientProvider: _mockChatClientProvider.Object,
                assembliesToScan: [],
                modeConfigurator: _mockAgentModeConfigurator.Object,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
                commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
            );
            await agentFactory.InitializeAsync();

            // Act
            var agent1 = agentFactory.GetAgent("agent1");

            // Assert
            Assert.NotNull(agent1);
            // TestManualTool should be included from both regular tools and optional tools (when condition is met)
            Assert.Contains("TestManualTool", agent1.FactoryTools);
        }
        finally
        {
            Environment.SetEnvironmentVariable("YamlTestFeature", null);
        }
    }

    [Fact]
    public async Task AutomaticallyAddsReadOnlyPromptWhenAgentModeIsReadOnly()
    {
        // Arrange
        // Set up the mock configurator to add the read-only prompt
        _mockAgentModeConfigurator
            .Setup(c => c.ConfigureAgent(
                It.IsAny<Agent<AgentContext>>(), // Corrected type: The concrete Agent type
                It.IsAny<IAgentDescriptor>(),
                It.IsAny<IReadOnlyDictionary<string, IPromptDescriptor>>()
            ))
            .Callback<Agent<AgentContext>, IAgentDescriptor, IReadOnlyDictionary<string, IPromptDescriptor>>(
                (agent, agentDescriptor, promptDescriptors) =>
                {
                    agent.Instructions += "\n\n**READ-ONLY MODE**";
                    agent.Instructions += "\nYou can only perform READ operations.";
                    agent.Instructions += "\nYou CANNOT make any changes.";
                });

        var toolFactory = CreateToolFactory();

        // Act
        // Pass the mockAgentModeConfigurator.Object to the AgentFactory constructor
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Assert
        Assert.Contains("READ-ONLY MODE", agent.Instructions);
        Assert.Contains("You can only perform READ operations", agent.Instructions);
        Assert.Contains("You CANNOT make any changes", agent.Instructions);
    }

    [Fact]
    public async Task DoesNotAddReadOnlyPromptWhenAgentModeIsNotReadOnly()
    {
        _mockAgentModeConfigurator
            .Setup(c => c.ConfigureAgent(
                It.IsAny<Agent<AgentContext>>(), // Corrected type
                It.IsAny<IAgentDescriptor>(),
                It.IsAny<IReadOnlyDictionary<string, IPromptDescriptor>>()
            ))
            .Callback<Agent<AgentContext>, IAgentDescriptor, IReadOnlyDictionary<string, IPromptDescriptor>>(
                (agent, agentDescriptor, promptDescriptors) =>
                {

                });

        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Should NOT contain the readonly prompt instructions
        Assert.DoesNotContain("READ-ONLY MODE", agent.Instructions);
        Assert.DoesNotContain("You can only perform READ operations", agent.Instructions);
    }

    [Fact]
    public async Task OptionalToolsAreIncludedWhenConditionIsMet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TestFeature", "Enabled");
        try
        {
            var toolFactory = CreateToolFactory();

            var agentFactory = new AgentFactory<AgentContext>(
                logger: _mockLogger.Object,
                toolFactory: toolFactory,
                chatClientProvider: _mockChatClientProvider.Object,
                assembliesToScan: [Assembly.GetExecutingAssembly()],
                modeConfigurator: _mockAgentModeConfigurator.Object,
                commonToolsYamlDirectory: null
            );
            await agentFactory.InitializeAsync();

            // Act
            var agent = agentFactory.GetAgent("TestAgent3WithOptionalTools");

            // Assert
            Assert.NotNull(agent);
            Assert.Contains("TestAutoTool", agent.FactoryTools);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TestFeature", null);
        }
    }

    [Fact]
    public async Task OptionalToolsAreNotIncludedWhenConditionIsNotMet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TestFeature", "Disabled");
        try
        {
            var toolFactory = CreateToolFactory();

            var agentFactory = new AgentFactory<AgentContext>(
                logger: _mockLogger.Object,
                toolFactory: toolFactory,
                chatClientProvider: _mockChatClientProvider.Object,
                assembliesToScan: [Assembly.GetExecutingAssembly()],
                modeConfigurator: _mockAgentModeConfigurator.Object,
                commonToolsYamlDirectory: null
            );
            await agentFactory.InitializeAsync();

            // Act
            var agent = agentFactory.GetAgent("TestAgent3WithOptionalTools");

            // Assert
            Assert.NotNull(agent);
            // The tool is in FactoryTools, but should be marked as disabled
            Assert.Contains("TestAutoTool", agent.FactoryTools);
            Assert.True(toolFactory.IsToolDisabled("TestAutoTool"), "TestAutoTool should be disabled when condition is not met");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TestFeature", null);
        }
    }

    [Fact]
    public async Task OptionalToolsAreNotIncludedWhenEnvironmentVariableIsNotSet()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgent4WithOptionalToolsOnly");

        // Assert
        Assert.NotNull(agent);
        // The tool is in FactoryTools, but should be marked as disabled since env var is not set
        Assert.Contains("TestAutoTool", agent.FactoryTools);
        Assert.True(toolFactory.IsToolDisabled("TestAutoTool"), "TestAutoTool should be disabled when env var is not set");
    }

    [Fact]
    public async Task MultipleOptionalToolsCanBeConditionallyEnabled()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TestFeature", "Enabled");
        try
        {
            var toolFactory = CreateToolFactory();

            var agentFactory = new AgentFactory<AgentContext>(
                logger: _mockLogger.Object,
                toolFactory: toolFactory,
                chatClientProvider: _mockChatClientProvider.Object,
                assembliesToScan: [Assembly.GetExecutingAssembly()],
                modeConfigurator: _mockAgentModeConfigurator.Object,
                commonToolsYamlDirectory: null
            );
            await agentFactory.InitializeAsync();

            // Act
            var agent = agentFactory.GetAgent("TestAgent5WithMultipleOptionalTools");

            // Assert
            Assert.NotNull(agent);
            // Both tools should be in FactoryTools
            Assert.Contains("TestAutoTool", agent.FactoryTools);
            Assert.Contains("TestManualTool", agent.FactoryTools);
            // Both tools should be enabled when the EnabledIf condition is met
            Assert.False(toolFactory.IsToolDisabled("TestAutoTool"), "TestAutoTool should be enabled");
            Assert.False(toolFactory.IsToolDisabled("TestManualTool"), "TestManualTool should be enabled");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TestFeature", null);
        }
    }

    [Fact]
    public async Task OptionalToolWithEmptyConditionIsNotEnabled()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgent6WithEmptyCondition");

        // Assert
        Assert.NotNull(agent);
        // Tool is in FactoryTools but should still be disabled since the plugin has EnabledIf condition that's not met
        Assert.Contains("TestAutoTool", agent.FactoryTools);
        Assert.True(toolFactory.IsToolDisabled("TestAutoTool"), "TestAutoTool should be disabled");
    }

    [Fact]
    public async Task ToolsAreEnabledWhenDataConnectorTypeExists()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgent7WithDataConnectorCondition");

        // Assert
        Assert.NotNull(agent);
        // Tool should be in FactoryTools and enabled since Teams DataConnector exists in CoreSettings
        Assert.Contains("TestDataConnectorTool", agent.FactoryTools);
        Assert.False(toolFactory.IsToolDisabled("TestDataConnectorTool"), "TestDataConnectorTool should be enabled when Teams connector exists");
    }

    [Fact]
    public async Task ToolsAreDisabledWhenDataConnectorTypeDoesNotExist()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgent8WithMissingDataConnector");

        // Assert
        Assert.NotNull(agent);
        // Tool should be in FactoryTools but disabled since Slack DataConnector does not exist
        Assert.Contains("TestSlackTool", agent.FactoryTools);
        Assert.True(toolFactory.IsToolDisabled("TestSlackTool"), "TestSlackTool should be disabled when Slack connector does not exist");
    }

    private ToolFactory<AgentContext> CreateToolFactory()
    {
        var toolFactory = new ToolFactory<AgentContext>(
            logger: _mockToolFactoryLogger.Object,
            serviceProvider: _serviceProvider,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            extensibilityLoader: _mockExtendedAgentRepository.Object,
            mcpToolsRepository: _mockMcpToolsRepository.Object
        );

        return toolFactory;
    }
}

public class TestAgent1Descriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent1";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = ["TestAgent2"];
    public List<string> Tools { get; set; } = ["TestAutoTool", "TestManualTool"];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = "Test Custom Reflection Note";
    public List<string> CommonPrompts { get; set; } = ["test_prompt"];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    List<string> IAgentDescriptor.Tools { get; set; } = [];
    List<string> IAgentDescriptor.McpTools { get; set; } = [];
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent2Descriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent2";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool", "TestManualTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = "Test Custom Reflection Note";
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    List<string> IAgentDescriptor.Tools { get; set; } = [];
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent3WithOptionalToolsDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent3WithOptionalTools";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent4WithOptionalToolsOnlyDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent4WithOptionalToolsOnly";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent5WithMultipleOptionalToolsDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent5WithMultipleOptionalTools";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool", "TestManualTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent6WithEmptyConditionDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent6WithEmptyCondition";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent7WithDataConnectorConditionDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent7WithDataConnectorCondition";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestDataConnectorTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestAgent8WithMissingDataConnectorDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent8WithMissingDataConnector";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestSlackTool"];
    public List<string> McpTools { get; set; } = [];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = string.Empty;
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public string? LlmModelName { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
    public bool DisableCommonPrompts { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
}

public class TestCommonPrompt : IPromptDescriptor
{
    public const string PromptText = "test prompt text";

    public string Name { get; set; } = "test_prompt";
    public string Prompt { get; set; } = PromptText;
}

public class TestReadOnlyPrompt : IPromptDescriptor
{
    public const string PromptText = "# Read-Only Mode Instructions\n\n**IMPORTANT: You are operating in READ-ONLY MODE.**\n\n**Read-Only Restrictions:**\n- You can only perform READ operations and queries\n- You CANNOT make any changes, modifications, or write operations";

    public string Name { get; set; } = "readonly";
    public string Prompt { get; set; } = PromptText;
}

public class TestTodoWritePrompt : IPromptDescriptor
{
    public const string PromptText = "# Task Management\nYou have access to the TodoWrite tool to help you manage and plan operational tasks.";

    public string Name { get; set; } = "todo_write";
    public string Prompt { get; set; } = PromptText;
}

[AgentToolPlugin(EnabledIf = "TestFeature:Enabled")]
internal class TestTools
{
    [AgentTool(ToolMode.Auto)]
    [Description("Test Auto Tool")]
    public string TestAutoTool()
    {
        return "Test auto tool";
    }

    [AgentTool(ToolMode.Manual)]
    [Description("Test Manual Tool")]
    public string TestManualTool()
    {
        return "Test manual tool";
    }
}

[AgentToolPlugin(EnabledIf = "DataConnectorType:Teams")]
internal class TestDataConnectorTools
{
    [AgentTool(ToolMode.Auto)]
    [Description("Test Data Connector Tool for Teams")]
    public string TestDataConnectorTool()
    {
        return "Test data connector tool";
    }
}

[AgentToolPlugin(EnabledIf = "DataConnectorType:Slack")]
internal class TestSlackTools
{
    [AgentTool(ToolMode.Auto)]
    [Description("Test Slack Tool")]
    public string TestSlackTool()
    {
        return "Test Slack tool";
    }
}
