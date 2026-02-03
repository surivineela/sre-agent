// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Framework.Skills;
using Agent.Plugins.Tools;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

public class AgentHookLoadingTests
{
    private readonly Mock<ILogger<AgentFactory<AgentContext>>> _mockLogger;
    private readonly Mock<ILogger<ToolFactory<AgentContext>>> _mockToolFactoryLogger;
    private readonly Mock<IChatClientProvider> _mockChatClientProvider;
    private readonly Mock<IAgentModeConfigurator<AgentContext>> _mockAgentModeConfigurator;
    private readonly Mock<IExtensibilityLoader> _mockExtensibilityLoader;
    private readonly Mock<IMcpConnectable> _mockMcpToolsRepository;
    private readonly IServiceProvider _serviceProvider;

    public AgentHookLoadingTests()
    {
        _mockLogger = new Mock<ILogger<AgentFactory<AgentContext>>>();
        _mockToolFactoryLogger = new Mock<ILogger<ToolFactory<AgentContext>>>();
        _mockChatClientProvider = new Mock<IChatClientProvider>();
        _mockAgentModeConfigurator = new Mock<IAgentModeConfigurator<AgentContext>>();
        _mockExtensibilityLoader = new Mock<IExtensibilityLoader>();
        _mockMcpToolsRepository = new Mock<IMcpConnectable>();
        _mockMcpToolsRepository.Setup(m => m.GetAllFunctions()).Returns(new List<AIFunction>());

        // Setup mock extensibility loader to return empty lists
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlToolDefinitionBase>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlAgentDescriptor>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedCommonPromptsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlPromptDescriptor>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedCommonToolsListsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlCommonToolsDescriptor>());

        var services = new ServiceCollection();
        services.AddSingleton(_mockLogger.Object);
        services.AddSingleton(_mockToolFactoryLogger.Object);
        services.AddSingleton(_mockChatClientProvider.Object);

        var mockHostEnvironment = new Mock<IHostEnvironment>();
        mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        mockHostEnvironment.Setup(e => e.ApplicationName).Returns("TestApp");
        mockHostEnvironment.Setup(e => e.ContentRootPath).Returns("/test/root");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddSingleton<IHostEnvironment>(mockHostEnvironment.Object);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new ExperimentalSettings());
        services.AddSingleton(new CoreSettings());

        _serviceProvider = services.BuildServiceProvider();
    }

    private ToolFactory<AgentContext> CreateToolFactory()
    {
        var yamlToolFunctionFactory = new YamlToolFunctionFactory<AgentContext>(
            _serviceProvider,
            _serviceProvider.GetServices<IYamlToolExecutorFactory>());

        return new ToolFactory<AgentContext>(
            logger: _mockToolFactoryLogger.Object,
            serviceProvider: _serviceProvider,
            assembliesToScan: [],
            extensibilityLoader: _mockExtensibilityLoader.Object,
            mcpToolsRepository: _mockMcpToolsRepository.Object,
            skillRegistry: new EmptySkillRegistry(),
            yamlToolFunctionFactory: yamlToolFunctionFactory);
    }

    private async Task<AgentFactory<AgentContext>> CreateAgentFactoryAsync()
    {
        var toolFactory = CreateToolFactory();

        var factory = new AgentFactory<AgentContext>(
            logger: _mockLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _mockChatClientProvider.Object,
            assembliesToScan: new List<Assembly>(),
            modeConfigurator: _mockAgentModeConfigurator.Object,
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"));

        await factory.InitializeAsync();

        return factory;
    }

    [Fact]
    public async Task LoadAgentFromYaml_LoadsHookConfiguration()
    {
        var agentFactory = await CreateAgentFactoryAsync();

        // Load the test agent YAML file directly
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents", "agent_with_stop_hook.yaml");
        var yamlContent = File.ReadAllText(yamlPath);
        var descriptor = YamlAgentDescriptor.FromYaml(yamlContent);

        var agent = agentFactory.LoadAgentFromDescriptor(descriptor, isCustomAgent: true);

        // Verify the hook configuration was loaded
        Assert.NotNull(agent.HookConfiguration);
        Assert.True(agent.HookConfiguration.HasHooksForEvent(HookEventType.Stop));

        var stopHooks = agent.HookConfiguration.GetHooksForEvent(HookEventType.Stop);
        Assert.Single(stopHooks);

        var hook = stopHooks[0];
        Assert.Equal(HookType.Prompt, hook.Type);
        Assert.Equal(30, hook.Timeout);
        Assert.Equal("gpt-4.1", hook.Model);
        Assert.Contains("evaluating whether an AI agent should stop", hook.Prompt);
        Assert.Contains("$ARGUMENTS", hook.Prompt);
    }

    [Fact]
    public async Task LoadAgentFromDescriptor_NoHooks_HookConfigurationIsNull()
    {
        var agentFactory = await CreateAgentFactoryAsync();

        var yaml = """
            name: agent_without_hooks
            system_prompt: A simple agent without hooks.
            vanilla_mode: true
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);
        var agent = agentFactory.LoadAgentFromDescriptor(descriptor, isCustomAgent: true);

        Assert.Null(agent.HookConfiguration);
    }

    [Fact]
    public async Task LoadAgentFromDescriptor_MultipleHooks_AllLoaded()
    {
        var agentFactory = await CreateAgentFactoryAsync();

        var yaml = """
            name: agent_multiple_hooks
            system_prompt: Agent with multiple stop hooks.
            vanilla_mode: true
            hooks:
              Stop:
                - type: prompt
                  prompt: "First validation check"
                  timeout: 15
                - type: prompt
                  prompt: "Second validation check"
                  timeout: 45
                  model: gpt-4o
            """;

        var descriptor = YamlAgentDescriptor.FromYaml(yaml);
        var agent = agentFactory.LoadAgentFromDescriptor(descriptor, isCustomAgent: true);

        Assert.NotNull(agent.HookConfiguration);
        var stopHooks = agent.HookConfiguration.GetHooksForEvent(HookEventType.Stop);
        Assert.Equal(2, stopHooks.Count);

        Assert.Equal("First validation check", stopHooks[0].Prompt);
        Assert.Equal(15, stopHooks[0].Timeout);
        Assert.Null(stopHooks[0].Model);

        Assert.Equal("Second validation check", stopHooks[1].Prompt);
        Assert.Equal(45, stopHooks[1].Timeout);
        Assert.Equal("gpt-4o", stopHooks[1].Model);
    }
}
