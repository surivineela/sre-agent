// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Models;
using Agent.Plugins;
using Agent.Runtime.Reasoning;
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
    private readonly Mock<IAgentModeConfigurator<AgentContext>> _mockAgentModeConfigurator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceCollection _services;

    public AgentFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory<AgentContext>>>();
        _mockToolFactoryLogger = new Mock<ILogger<ToolFactory<AgentContext>>>();
        _mockAgentModeConfigurator = new Mock<IAgentModeConfigurator<AgentContext>>();
        _services = new ServiceCollection();
        _services.AddSingleton(_mockLogger.Object);
        _services.AddSingleton(_mockToolFactoryLogger.Object);
        _services.AddTransient<TestTools>();
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
    }

    [Fact]
    public void LoadsAgentsFromAssembly()
    {
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: new ToolFactory<AgentContext>(
                logger: _mockToolFactoryLogger.Object,
                serviceProvider: _serviceProvider,
                assembliesToScan: [Assembly.GetExecutingAssembly()]
            ),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            commonToolsYamlDirectory: null
        );

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
    public void LoadsAgentsFromYaml()
    {
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: new ToolFactory<AgentContext>(
                logger: _mockToolFactoryLogger.Object,
                serviceProvider: _serviceProvider,
                assembliesToScan: [Assembly.GetExecutingAssembly()]
            ),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools")
        );

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
    public void AutomaticallyAddsReadOnlyPromptWhenAgentModeIsReadOnly()
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

        var toolFactory = new ToolFactory<AgentContext>(
            logger: _mockToolFactoryLogger.Object,
            serviceProvider: _serviceProvider,
            assembliesToScan: [Assembly.GetExecutingAssembly()]
        );

        // Act
        // Pass the mockAgentModeConfigurator.Object to the AgentFactory constructor
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Assert
        Assert.Contains("READ-ONLY MODE", agent.Instructions);
        Assert.Contains("You can only perform READ operations", agent.Instructions);
        Assert.Contains("You CANNOT make any changes", agent.Instructions);
    }

    [Fact]
    public void DoesNotAddReadOnlyPromptWhenAgentModeIsNotReadOnly()
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

        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: new ToolFactory<AgentContext>(
                logger: _mockToolFactoryLogger.Object,
                serviceProvider: _serviceProvider,
                assembliesToScan: [Assembly.GetExecutingAssembly()]
            ),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonToolsYamlDirectory: null
        );

        var agent = agentFactory.GetAgent("TestAgent1");
        Assert.NotNull(agent);

        // Should NOT contain the readonly prompt instructions
        Assert.DoesNotContain("READ-ONLY MODE", agent.Instructions);
        Assert.DoesNotContain("You can only perform READ operations", agent.Instructions);
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
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
}

public class TestAgent2Descriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent2";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> Tools { get; set; } = ["TestAutoTool", "TestManualTool"];
    public bool AllowParallelToolCalls { get; set; } = false;
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = "Test Custom Reflection Note";
    public List<string> CommonPrompts { get; set; } = [];
    public List<string> CommonTools { get; set; } = [];
    public string CriticPromptPath { get; set; } = string.Empty;
    public bool CriticOnHandOff { get; set; } = false;
    public float? Temperature { get; set; } = null;
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];
    public string? OutputType { get; set; } = null;
    public string? UserPromptOverride { get; set; } = null;
    public bool DisableDocumentRetrieval { get; set; } = false;
    public bool EnableHandoffPromptOverride { get; set; } = false;
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

[AgentToolPlugin]
internal class TestTools
{
    [AgentTool(ToolMode.Auto)]
    [Description("Test Auto Tool")]
    public string TestAutoTool()
    {
        return "Test auto tool";
    }

    [Description("Test Manual Tool")]
    public string TestManualTool()
    {
        return "Test manual tool";
    }
}
