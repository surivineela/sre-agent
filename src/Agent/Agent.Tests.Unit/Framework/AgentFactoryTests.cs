// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Tests.Unit.Framework;

public class AgentFactoryTests
{
    private readonly Mock<ILogger<AgentFactory<AgentContext>>> _mockLogger;
    private readonly Mock<ILogger<ToolFactory>> _mockToolFactoryLogger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceCollection _services;

    public AgentFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory<AgentContext>>>();
        _mockToolFactoryLogger = new Mock<ILogger<ToolFactory>>();
        _services = new ServiceCollection();
        _services.AddSingleton(_mockLogger.Object);
        _services.AddSingleton(_mockToolFactoryLogger.Object);
        _services.AddTransient<TestTools>();
        _serviceProvider = _services.BuildServiceProvider();
    }

    [Fact]
    public void LoadsAgentsFromAssembly()
    {
        var agentFactory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: new ToolFactory(
                logger: _mockToolFactoryLogger.Object,
                serviceProvider: _serviceProvider,
                assembliesToScan: [Assembly.GetExecutingAssembly()]
            ),
            assembliesToScan: [Assembly.GetExecutingAssembly()]
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
            toolFactory: new ToolFactory(
                logger: _mockToolFactoryLogger.Object,
                serviceProvider: _serviceProvider,
                assembliesToScan: [Assembly.GetExecutingAssembly()]
            ),
            assembliesToScan: [],
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts")
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
    }
}

public class TestAgent1Descriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent1";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = ["TestAgent2"];
    public List<string> AutoTools { get; set; } = ["TestAutoTool"];
    public List<string> ManualTools { get; set; } = ["TestManualTool"];
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = "Test Custom Reflection Note";
    public List<string> CommonPrompts { get; set; } = ["test_prompt"];
}

public class TestAgent2Descriptor : IAgentDescriptor
{
    public string Name { get; set; } = "TestAgent2";
    public string Instructions { get; set; } = "Test Instructions";
    public string? HandoffDescription { get; set; } = "Test Handoff Description";
    public List<string> Handoffs { get; set; } = [];
    public List<string> AutoTools { get; set; } = ["TestAutoTool"];
    public List<string> ManualTools { get; set; } = ["TestManualTool"];
    public int MaxReflectionCount { get; set; } = 0;
    public string CustomReflectionNote { get; set; } = "Test Custom Reflection Note";
    public List<string> CommonPrompts { get; set; } = [];
}

public class TestCommonPrompt : IPromptDescriptor
{
    public const string PromptText = "test prompt text";

    public string Name { get; set; } = "test_prompt";
    public string Prompt { get; set; } = PromptText;
}

[AgentToolPlugin]
internal class TestTools
{
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
