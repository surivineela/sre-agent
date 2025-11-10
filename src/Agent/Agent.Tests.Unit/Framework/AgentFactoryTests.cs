// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Models;
using Agent.Framework.Skills;
using Agent.Plugins;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Tests.Unit.Framework;

public class AgentFactoryTests
{
    private readonly Mock<ILogger<AgentFactory<AgentContext>>> _mockLogger;
    private readonly Mock<ILogger<ToolFactory<AgentContext>>> _mockToolFactoryLogger;
    private readonly Mock<IChatClientProvider> _mockChatClientProvider;
    private readonly Mock<IAgentModeConfigurator<AgentContext>> _mockAgentModeConfigurator;
    private readonly Mock<IExtensibilityLoader> _mockExtendedAgentRepository;
    private readonly Mock<IMcpConnectable> _mockMcpToolsRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceCollection _services;

    public AgentFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory<AgentContext>>>();
        _mockToolFactoryLogger = new Mock<ILogger<ToolFactory<AgentContext>>>();
        _mockChatClientProvider = new Mock<IChatClientProvider>();
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
        _services.AddSingleton(_mockChatClientProvider.Object);

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent1 = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent1);
        Assert.Equal("TestAgent1", agent1.Name);
        Assert.Contains(TestCommonPrompt.PromptText, agent1.Instructions.ToString());

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
        Assert.Contains(prompt1.Prompt, agent1.Instructions.ToString());

        Assert.Contains(agent2.Name, agent1.Handoffs.Select(h => h.AgentName));

        // Test that common tools are loaded
        Assert.Contains("TestTool1", agent1.FactoryTools);
        Assert.Contains("TestTool2", agent1.FactoryTools);
    }

    [Fact]
    public async Task VanillaMode_Agent_SkipsFrameworkInstructions()
    {
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            defaultOutputType: typeof(DefaultAgentOutput)
        );

        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("vanilla_test_agent");
        Assert.NotNull(agent);
        Assert.True(agent.EnableVanillaMode);
        Assert.Equal(typeof(string), agent.OutputType);
        Assert.False(agent.CriticOnHandOff);
        Assert.Equal(0, agent.MaxReflectionCount);
        Assert.Contains(ToDoWriteTool<AgentContext>.ToolName, agent.FactoryTools);

        var instructions = agent.Instructions.ToString();
        Assert.Contains("You are a vanilla agent with minimal instructions.", instructions);
        Assert.DoesNotContain("# Handoff System Context", instructions);
        Assert.DoesNotContain(TestCommonPrompt.PromptText, instructions);
        // todo prompt automatically added
        Assert.Contains(ToDoWriteTool<AgentContext>.ToolName, instructions);
    }

    [Fact]
    public async Task VanillaMode_Disabled_IncludesFrameworkInstructions()
    {
        const string ModeMarker = "MODE_CONFIG_MARKER";

        _mockAgentModeConfigurator
            .Setup(c => c.ConfigureAgent(
                It.IsAny<Agent<AgentContext>>(),
                It.IsAny<IAgentDescriptor>(),
                It.IsAny<IReadOnlyDictionary<string, IPromptDescriptor>>()))
            .Callback<Agent<AgentContext>, IAgentDescriptor, IReadOnlyDictionary<string, IPromptDescriptor>>(
                (agent, descriptor, prompts) => agent.Instructions.AddCommonPrompt(ModeMarker));

        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );

        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("agent1");
        Assert.NotNull(agent);
        Assert.False(agent.EnableVanillaMode);

        var instructions = agent.Instructions.ToString();
        const string Prompt1Text = "This is a test prompt.";
        Assert.Contains("# Handoff System Context", instructions);
        Assert.Contains(Prompt1Text, instructions);
        Assert.Contains(ModeMarker, instructions);
    }

    [Fact]
    public async Task VanillaMode_WithUserPromptOverride_PreservesOverride()
    {
        _mockAgentModeConfigurator.Invocations.Clear();

        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );

        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("vanilla_with_override_agent");
        Assert.NotNull(agent);
        Assert.True(agent.EnableVanillaMode);
        Assert.Equal(typeof(string), agent.OutputType);
        Assert.False(agent.CriticOnHandOff);
        Assert.Equal(0, agent.MaxReflectionCount);
        Assert.Equal("vanilla_with_override_agent", agent.Name);
        Assert.Equal("Custom user instructions here.\nThis should be preserved.\n", agent.UserPromptOverride);

        var instructions = agent.Instructions.ToString();
        Assert.DoesNotContain("# Handoff System Context", instructions);

        var reasoningLoop = (ReasoningLoop)RuntimeHelpers.GetUninitializedObject(typeof(ReasoningLoop));

        typeof(ReasoningLoop)
            .GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(reasoningLoop, NullLogger<ReasoningLoop>.Instance);

        typeof(ReasoningLoop)
            .GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(reasoningLoop, new AgentContext(Guid.NewGuid(), Guid.NewGuid(), AgentTypeEnum.Meta, ContextStateEnum.Processing, null, null));

        typeof(ReasoningLoop)
            .GetField("_currentAgent", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(reasoningLoop, agent);

        var constructUserMessage = typeof(ReasoningLoop)
            .GetMethod("ConstructUserMessage", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not access ConstructUserMessage");

        var chatMessage = new ReasoningLoopChatMessage(new ChatMessage(ChatRole.User, "Help me debug this issue"));
        var userMessage = (string)constructUserMessage.Invoke(reasoningLoop, new object[] { chatMessage })!;

        Assert.Contains("Custom user instructions here.", userMessage);
        Assert.Contains(Markers.UserQuestionMarker, userMessage);
        Assert.Contains("Help me debug this issue", userMessage);
        Assert.DoesNotContain("Try your best to answer the user's questions", userMessage);
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
                chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Assert
        Assert.Contains("READ-ONLY MODE", agent.Instructions.ToString());
        Assert.Contains("You can only perform READ operations", agent.Instructions.ToString());
        Assert.Contains("You CANNOT make any changes", agent.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Should NOT contain the readonly prompt instructions
        Assert.DoesNotContain("READ-ONLY MODE", agent.Instructions.ToString());
        Assert.DoesNotContain("You can only perform READ operations", agent.Instructions.ToString());
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
                chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
                chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
                chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
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

    [Fact]
    public async Task LoadsExperiments()
    {
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [],
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments")
        );

        await agentFactory.InitializeAsync();

        Assert.NotEmpty(agentFactory.Experiments);
    }

    [Fact]
    public async Task RaisesAgentChangedEventWhenAgentIsAdded()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            agentsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        AgentChangedEventArgs? receivedEventArgs = null;
        agentFactory.AgentChanged += (sender, args) =>
        {
            receivedEventArgs = args;
        };

        var yamlContent = @"
name: test_dynamic_agent
system_prompt: Test dynamic agent instructions
tools:
  - TestAutoTool
handoffs: []
";

        // Act
        agentFactory.LoadAgentFromYamlContent(yamlContent, isCustomAgent: true);

        // Assert
        Assert.NotNull(receivedEventArgs);
        Assert.Equal("test_dynamic_agent", receivedEventArgs.AgentName);
        Assert.Equal(AgentChangeType.Added, receivedEventArgs.ChangeType);
    }

    [Fact]
    public async Task RaisesAgentChangedEventWhenAgentIsUpdated()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [],

            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );
        await agentFactory.InitializeAsync();

        var receivedEvents = new List<AgentChangedEventArgs>();
        agentFactory.AgentChanged += (sender, args) =>
        {
            receivedEvents.Add(args);
        };

        // Act - Load agent with same name as existing agent with overwrite
        agentFactory.LoadYamlAgentsFromFolder(
            Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            overwriteExistingAgents: true,
            recursive: false);

        // Assert - Verify agent1 was updated (multiple agents may be loaded)
        Assert.NotEmpty(receivedEvents);
        var agent1Event = receivedEvents.FirstOrDefault(e => e.AgentName == "agent1");
        Assert.NotNull(agent1Event);
        Assert.Equal(AgentChangeType.Updated, agent1Event.ChangeType);
    }

    [Fact]
    public async Task AgentChangedEventCanHaveMultipleSubscribers()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            agentsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        var subscriber1Received = false;
        var subscriber2Received = false;

        agentFactory.AgentChanged += (sender, args) =>
        {
            subscriber1Received = true;
        };

        agentFactory.AgentChanged += (sender, args) =>
        {
            subscriber2Received = true;
        };

        var yamlContent = @"
name: test_agent_multi_sub
system_prompt: Test instructions
tools:
  - TestAutoTool
handoffs: []
";

        // Act
        agentFactory.LoadAgentFromYamlContent(yamlContent, isCustomAgent: true);

        // Assert
        Assert.True(subscriber1Received, "First subscriber should receive event");
        Assert.True(subscriber2Received, "Second subscriber should receive event");
    }

    [Fact]
    public async Task AgentWithEnableSkillsTrueAddsReadSkillFileTool()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [typeof(TestAgentWithSkillsEnabledDescriptor).Assembly],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgentWithSkillsEnabled");

        // Assert
        Assert.NotNull(agent);
        Assert.True(agent.EnableSkills);
        Assert.Contains(ReadSkillFileTool<AgentContext>.ToolName, agent.FactoryTools);
    }

    [Fact]
    public async Task AgentWithEnableSkillsFalseDoesNotAddReadSkillFileTool()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgent1");

        // Assert
        Assert.NotNull(agent);
        Assert.False(agent.EnableSkills);
        Assert.DoesNotContain(ReadSkillFileTool<AgentContext>.ToolName, agent.FactoryTools);
    }

    [Fact]
    public async Task AgentLoadedFromYamlWithEnableSkillsTrueAddsReadSkillFileTool()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("agent_with_skills");

        // Assert
        Assert.NotNull(agent);
        Assert.True(agent.EnableSkills);
        Assert.Contains(ReadSkillFileTool<AgentContext>.ToolName, agent.FactoryTools);
    }

    [Fact]
    public async Task AgentLoadedFromYamlWithEnableSkillsFalseDoesNotAddReadSkillFileTool()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("agent1");

        // Assert
        Assert.NotNull(agent);
        Assert.False(agent.EnableSkills);
        Assert.DoesNotContain(ReadSkillFileTool<AgentContext>.ToolName, agent.FactoryTools);
    }

    [Fact]
    public async Task ReadSkillFileToolIsAutomaticallyAddedOnlyOnce()
    {
        // Arrange
        var toolFactory = CreateToolFactory();

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [typeof(TestAgentWithSkillsAndReadSkillFileToolDescriptor).Assembly],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );
        await agentFactory.InitializeAsync();

        // Act
        var agent = agentFactory.GetAgent("TestAgentWithSkillsAndReadSkillFileTool");

        // Assert
        Assert.NotNull(agent);
        Assert.True(agent.EnableSkills);
        // Should only contain the tool once, even though it's in the agent's tools list
        var readSkillFileCount = agent.FactoryTools.Count(t => t == ReadSkillFileTool<AgentContext>.ToolName);
        Assert.Equal(1, readSkillFileCount);
    }

    private ToolFactory<AgentContext> CreateToolFactory()
    {
        var toolFactory = new ToolFactory<AgentContext>(
            logger: _mockToolFactoryLogger.Object,
            serviceProvider: _serviceProvider,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            extensibilityLoader: _mockExtendedAgentRepository.Object,
            mcpToolsRepository: _mockMcpToolsRepository.Object,
            skillRegistry: new EmptySkillRegistry()
        );

        return toolFactory;
    }

    private SkillRegistry CreateSkillRegistry()
    {
        return new SkillRegistry(
            logger: Mock.Of<ILogger<SkillRegistry>>(),
            systemSkillsDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestSkills"),
            extensibilityLoader: _mockExtendedAgentRepository.Object
        );
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
}

public class TestTodoWritePrompt : IPromptDescriptor
{
    public const string PromptText = "# Task Management\nYou have access to the TodoWrite tool to help you manage and plan operational tasks.";

    public string Name { get; set; } = "todo_write";
    public string Prompt { get; set; } = PromptText;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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
    public bool EnableVanillaMode { get; set; } = false;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableSkills { get; set; } = false;
    public bool AddSystemSkills { get; set; } = false;
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

public class TestAgentWithSkillsEnabledDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgentWithSkillsEnabled";
    public string Instructions { get; set; } = "Test Instructions for agent with skills enabled";
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
    public bool EnableSkills { get; set; } = true; // Skills enabled
    public bool AddSystemSkills { get; set; } = true;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableVanillaMode { get; set; } = false;
}

public class TestAgentWithSkillsAndReadSkillFileToolDescriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgentWithSkillsAndReadSkillFileTool";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool", ReadSkillFileTool<AgentContext>.ToolName];
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
    public bool EnableSkills { get; set; } = true; // Skills enabled, and tool already in list
    public bool AddSystemSkills { get; set; } = true;
    public AgentType AgentType { get; set; } = AgentType.Autonomous;
    public string? ParameterExtractionAgent { get; set; } = string.Empty;
    public List<string> OrchestrationStartAgents { get; set; } = [];
    public string? ResultSummarizationPrompt { get; set; } = string.Empty;
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];
    public bool EnableVanillaMode { get; set; } = false;
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
