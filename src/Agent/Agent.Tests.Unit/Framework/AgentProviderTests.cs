// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Reflection;
using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Framework.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Framework;

public class AgentProviderTests
{
    private readonly Mock<ILogger<AgentFactory<AgentContext>>> _mockFactoryLogger = new();
    private readonly Mock<ILogger<ToolFactory<AgentContext>>> _mockToolFactoryLogger = new();
    private readonly Mock<ILogger<AgentProvider<AgentContext>>> _mockProviderLogger = new();
    private readonly Mock<IAgentModeConfigurator<AgentContext>> _mockAgentModeConfigurator = new();
    private readonly Mock<IExtensibilityLoader> _mockExtensibilityLoader = new();
    private readonly Mock<IMcpConnectable> _mockMcpToolsRepository = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceCollection _services = new();

    public AgentProviderTests()
    {
        _mockMcpToolsRepository.Setup(m => m.GetAllFunctions()).Returns(new List<AIFunction>()); // returns empty tool list
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlToolDefinitionBase>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlAgentDescriptor>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedCommonPromptsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlPromptDescriptor>());
        _mockExtensibilityLoader.Setup(x => x.LoadExtendedCommonToolsListsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YamlCommonToolsDescriptor>());

        _services.AddSingleton(_mockFactoryLogger.Object);
        _services.AddSingleton(_mockToolFactoryLogger.Object);
        _services.AddSingleton(_mockProviderLogger.Object);
        _services.AddTransient<TestTools>();
        _services.AddSingleton<ChatClientProvider>();
        foreach (var (llmModel, clientName) in LlmModels.LlmClients)
        {
            _services.AddKeyedSingleton(clientName, Mock.Of<IChatClient>());
        }
        SetupHostEnvAndConfig();
        _serviceProvider = _services.BuildServiceProvider();
    }

    private void SetupHostEnvAndConfig()
    {
        var mockHostEnvironment = new Mock<IHostEnvironment>();
        mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        mockHostEnvironment.Setup(e => e.ApplicationName).Returns("TestApp");
        mockHostEnvironment.Setup(e => e.ContentRootPath).Returns("/test/root");

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:Core:Azure:Crawler:TenantId", "72f988bf-86f1-41af-91ab-2d7cd011db47"}
            })
            .Build();

        _services.AddSingleton(mockHostEnvironment.Object);
        _services.AddSingleton(configuration);
        _services.AddSingleton(new ExperimentalSettings
        {
            AutoHandoffToMeta = true,
            EnableHandoffReasoning = true,
        });
    }

    private ToolFactory<AgentContext> CreateToolFactory() => new(
        logger: _mockToolFactoryLogger.Object,
        serviceProvider: _serviceProvider,
        assembliesToScan: [Assembly.GetExecutingAssembly()],
        extensibilityLoader: _mockExtensibilityLoader.Object,
        mcpToolsRepository: _mockMcpToolsRepository.Object);

    [Fact]
    public async Task AppliesForcedVariantPromptOverlay()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "prompt_experiment=prompt_overlay");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.Contains("Replaced system prompt for agent1.", agent1.Instructions);
        Assert.Contains("Prepended text.", agent1.Instructions);
        Assert.Contains("Appended text.", agent1.Instructions);
        Assert.NotNull(agent1.HandoffDescription);
        Assert.Equal("New handoff instructions.".Trim(), agent1.HandoffDescription.ToString().Trim());
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task AppliesToolOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "tool_experiment=tools_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        Assert.Contains("ReplaceToolA", agent1.FactoryTools);
        Assert.Contains("ReplaceToolB", agent1.FactoryTools);
        Assert.DoesNotContain("TestAutoTool", agent1.FactoryTools);
        Assert.Contains("ExtraTool1", agent2.FactoryTools);
        Assert.Contains("ExtraTool2", agent2.FactoryTools);
        Assert.DoesNotContain("TestManualTool", agent2.FactoryTools);
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task AppliesHandoffOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "handoff_experiment=handoffs_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        var agent3 = provider.GetAgent("agent3");
        Assert.Single(agent1.Handoffs);
        Assert.Equal("agent3", agent1.Handoffs[0].AgentName);
        Assert.Empty(agent2.Handoffs);
        Assert.Single(agent3.Handoffs);
        Assert.Equal("agent1", agent3.Handoffs[0].AgentName);
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task AppliesParamOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "param_experiment=params_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        Assert.Equal("high", agent1.ReasoningEffortLevel);
        Assert.Equal("high", agent2.ReasoningEffortLevel);
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    private sealed class TestVariantAssignerNotInExperiment : IVariantAssigner
    {
        public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null) => new(experiment.ExperimentId, "prompt_overlay", false);
    }

    [Fact]
    public async Task DoesNotApplyOverlayWhenNotInExperiment()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new TestVariantAssignerNotInExperiment(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions);
        Assert.NotEqual("New handoff instructions.", agent1.HandoffDescription?.ToString());
    }

    private sealed class TestVariantAssignerAlwaysIn : IVariantAssigner
    {
        public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null) => new(experiment.ExperimentId, experiment.Variants.First().Name, true);
    }

    [Fact]
    public async Task DisabledExperimentNotApplied()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new TestVariantAssignerAlwaysIn(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("DISABLED_EXPERIMENT_SHOULD_NOT_APPLY", agent1.Instructions);
    }

    private sealed class TestVariantAssignerAlwaysVariant : IVariantAssigner
    {
        public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null)
        {
            return new AssignedVariant(experiment.ExperimentId, experiment.Variants.First().Name, true);
        }
    }

    [Fact]
    public async Task CoverageZeroPreventsVariantApplication()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var assigner = new HashVariantAssigner();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, assigner, _mockProviderLogger.Object, "test-instance-for-coverage0");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("COVERAGE_SHOULD_NOT_APPLY", agent1.Instructions);
        var variants = provider.GetActiveVariants("thread-x");
        Assert.DoesNotContain("coverage_experiment", variants.Keys);
    }

    private sealed class DeterministicThreadVariantAssigner : IVariantAssigner
    {
        public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null)
        {
            if (!experiment.Enabled)
            {
                return new(experiment.ExperimentId, "control", false);
            }
            var variants = experiment.Variants.ToList();
            if (experiment.Unit == ExperimentUnit.Global)
            {
                return new(experiment.ExperimentId, variants.First().Name, true);
            }
            var id = threadId ?? instanceId;
            return id.EndsWith("A")
                ? new(experiment.ExperimentId, variants.First().Name, true)
                : new(experiment.ExperimentId, variants.Last().Name, true);
        }
    }

    [Fact]
    public async Task GlobalExperimentAppliesSameVariantAcrossThreads()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "global-instance");
        var agentThread1 = provider.GetAgent("agent1", threadId: "thread-1");
        var agentThread2 = provider.GetAgent("agent1", threadId: "thread-2");
        Assert.Contains("GLOBAL_VARIANT_APPLIED", agentThread1.Instructions);
        Assert.Contains("GLOBAL_VARIANT_APPLIED", agentThread2.Instructions);
        var variants1 = provider.GetActiveVariants("thread-1");
        var variants2 = provider.GetActiveVariants("thread-2");
        Assert.Equal(variants1["global_experiment"].Name, variants2["global_experiment"].Name);
    }

    [Fact]
    public async Task PerThreadExperimentAppliesDifferentVariants()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "per-thread-instance");
        var agentA = provider.GetAgent("agent1", threadId: "threadA");
        var agentB = provider.GetAgent("agent1", threadId: "threadB");
        Assert.Contains("PER_THREAD_VARIANT_A", agentA.Instructions);
        Assert.Contains("PER_THREAD_VARIANT_B", agentB.Instructions);
        var variantsA = provider.GetActiveVariants("threadA");
        var variantsB = provider.GetActiveVariants("threadB");
        Assert.NotEqual(variantsA["perthread_experiment"].Name, variantsB["perthread_experiment"].Name);
    }

    [Fact]
    public async Task ThreadsWithSameVariantCombinationShareCachedGraph()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "cache-test-instance");

        // Both threads end with "A", so they should get the same variant combination
        var agent1Thread1 = provider.GetAgent("agent1", threadId: "thread1A");
        var agent1Thread2 = provider.GetAgent("agent1", threadId: "thread2A");

        // Agents should have same instructions (same variant)
        Assert.Equal(agent1Thread1.Instructions, agent1Thread2.Instructions);

        // Get variants for both threads - should be identical
        var variants1 = provider.GetActiveVariants("thread1A");
        var variants2 = provider.GetActiveVariants("thread2A");

        foreach (var (expId, variant1) in variants1)
        {
            Assert.True(variants2.ContainsKey(expId));
            Assert.Equal(variant1.Name, variants2[expId].Name);
        }
    }

    [Fact]
    public async Task DifferentVariantCombinationsCreateSeparateCacheEntries()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "cache-diff-instance");

        // Thread ending with "A" vs "B" should get different variants
        var agentA = provider.GetAgent("agent1", threadId: "threadA");
        var agentB = provider.GetAgent("agent1", threadId: "threadB");

        // Should have different instructions due to different variants
        Assert.NotEqual(agentA.Instructions, agentB.Instructions);

        // Variants should be different
        var variantsA = provider.GetActiveVariants("threadA");
        var variantsB = provider.GetActiveVariants("threadB");

        // At least one experiment should have different variants
        var hasDifferentVariant = false;
        foreach (var (expId, variantA) in variantsA)
        {
            if (variantsB.TryGetValue(expId, out var variantB) && variantA.Name != variantB.Name)
            {
                hasDifferentVariant = true;
                break;
            }
        }
        Assert.True(hasDifferentVariant);
    }

    [Fact]
    public void VariantCombinationKeyEquality()
    {
        var assignments1 = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", true)
        };

        var assignments2 = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", true)
        };

        var key1 = new VariantCombinationKey(assignments1);
        var key2 = new VariantCombinationKey(assignments2);

        // Same assignments should produce equal keys
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void VariantCombinationKeyInequality()
    {
        var assignments1 = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", true)
        };

        var assignments2 = new List<AssignedVariant>
        {
            new("exp1", "treatment", true),
            new("exp2", "treatment", true)
        };

        var key1 = new VariantCombinationKey(assignments1);
        var key2 = new VariantCombinationKey(assignments2);

        // Different variants should produce different keys
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void VariantCombinationKeyIgnoresNotInExperiment()
    {
        var assignments1 = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", false) // Not in experiment
        };

        var assignments2 = new List<AssignedVariant>
        {
            new("exp1", "control", true)
        };

        var key1 = new VariantCombinationKey(assignments1);
        var key2 = new VariantCombinationKey(assignments2);

        // Should be equal because exp2 is not in experiment for assignments1
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void VariantCombinationKeyOrderIndependent()
    {
        var assignments1 = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", true)
        };

        var assignments2 = new List<AssignedVariant>
        {
            new("exp2", "treatment", true),
            new("exp1", "control", true)
        };

        var key1 = new VariantCombinationKey(assignments1);
        var key2 = new VariantCombinationKey(assignments2);

        // Order shouldn't matter - should be equal
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void VariantCombinationKeyToString()
    {
        var assignments = new List<AssignedVariant>
        {
            new("exp1", "control", true),
            new("exp2", "treatment", true)
        };

        var key = new VariantCombinationKey(assignments);
        var str = key.ToString();

        // Should contain both experiment IDs and variant names
        Assert.Contains("exp1", str);
        Assert.Contains("control", str);
        Assert.Contains("exp2", str);
        Assert.Contains("treatment", str);
    }

    [Fact]
    public void VariantCombinationKeyEmptyAssignments()
    {
        var assignments = new List<AssignedVariant>();
        var key = new VariantCombinationKey(assignments);
        var str = key.ToString();

        // Should handle empty assignments gracefully
        Assert.Equal("no-experiments", str);
    }

    [Fact]
    public async Task ForceDisableExperimentsSkipsDisabledExperiments()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, "prompt_experiment;tool_experiment");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment and tool_experiment should NOT be applied
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions);
        Assert.DoesNotContain("ReplaceToolA", agent1.FactoryTools);

        // Get active variants - disabled experiments should not appear
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);
        Assert.DoesNotContain("tool_experiment", variants.Keys);

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, null);
    }

    [Fact]
    public async Task ForceDisableSpecificExperimentWhileOthersStillApply()
    {
        var toolFactory = CreateToolFactory();
        // Disable only prompt_experiment, but keep tool_experiment enabled
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, "prompt_experiment");
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "tool_experiment=tools_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment should NOT be applied (disabled)
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions);

        // tool_experiment SHOULD be applied (not disabled, and forced)
        Assert.Contains("ReplaceToolA", agent1.FactoryTools);
        Assert.Contains("ReplaceToolB", agent1.FactoryTools);

        // Get active variants
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);
        Assert.Contains("tool_experiment", variants.Keys);

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, null);
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task ForceDisableOverridesForcedVariants()
    {
        var toolFactory = CreateToolFactory();
        // Try to force a variant for an experiment that's also disabled - disable should win
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, "prompt_experiment");
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "prompt_experiment=prompt_overlay");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment should NOT be applied even though it's forced (disabled takes precedence)
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions);

        // Get active variants - disabled experiment should not appear
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceDisableExperimentsEnvVar, null);
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task AgentAsToolIsProperlyCloned()
    {
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Get agent4 which has agent3 as a tool
        var agent4 = provider.GetAgent("agent4", threadId: "thread1");

        // Verify AgentsAsTools is populated
        Assert.NotEmpty(agent4.AgentsAsTools);
        Assert.Single(agent4.AgentsAsTools);

        // Verify the tool name is correct
        var agentAsTool = agent4.AgentsAsTools[0];
        Assert.Equal("use_agent3_as_tool", agentAsTool.Name);
        Assert.Equal("agent3", agentAsTool.TargetAgentName);
    }

    [Fact]
    public async Task AgentAsToolIsSharedForSameVariantCombination()
    {
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Get agent4 for two different threads (no experiments, so same variant combination)
        var agent4Thread1 = provider.GetAgent("agent4", threadId: "thread1");
        var agent4Thread2 = provider.GetAgent("agent4", threadId: "thread2");

        // Both should have AgentsAsTools
        Assert.NotEmpty(agent4Thread1.AgentsAsTools);
        Assert.NotEmpty(agent4Thread2.AgentsAsTools);

        // When threads have the same variant combination, they share the cached graph
        // so the AgentAsTool instances SHOULD be the same object
        Assert.Same(agent4Thread1.AgentsAsTools[0], agent4Thread2.AgentsAsTools[0]);

        // They should have the same metadata
        Assert.Equal(agent4Thread1.AgentsAsTools[0].Name, agent4Thread2.AgentsAsTools[0].Name);
        Assert.Equal(agent4Thread1.AgentsAsTools[0].TargetAgentName, agent4Thread2.AgentsAsTools[0].TargetAgentName);
    }

    [Fact]
    public async Task AgentAsToolIsAlsoInToolsList()
    {
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Get agent4 which has agent3 as a tool
        var agent4 = provider.GetAgent("agent4", threadId: "thread1");

        // Verify AgentAsTool is in the Tools list
        var agentAsToolInToolsList = agent4.Tools.FirstOrDefault(t => t.Name == "use_agent3_as_tool");
        Assert.NotNull(agentAsToolInToolsList);
        Assert.IsType<AgentAsTool<AgentContext>>(agentAsToolInToolsList);
    }

    [Fact]
    public async Task ClonedAgentAsToolIsDifferentFromFactoryAgentAsTool()
    {
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Get the base agent from the factory
        var factoryAgent4 = factory.GetAgent("agent4");

        // Get the cloned agent from the provider
        var clonedAgent4 = provider.GetAgent("agent4", threadId: "thread1");

        // Both should have AgentsAsTools
        Assert.NotEmpty(factoryAgent4.AgentsAsTools);
        Assert.NotEmpty(clonedAgent4.AgentsAsTools);

        // The AgentAsTool instances should be DIFFERENT objects (cloned, not shared)
        Assert.NotSame(factoryAgent4.AgentsAsTools[0], clonedAgent4.AgentsAsTools[0]);

        // But they should have the same metadata
        Assert.Equal(factoryAgent4.AgentsAsTools[0].Name, clonedAgent4.AgentsAsTools[0].Name);
        Assert.Equal(factoryAgent4.AgentsAsTools[0].TargetAgentName, clonedAgent4.AgentsAsTools[0].TargetAgentName);
        Assert.Equal(factoryAgent4.AgentsAsTools[0].Description, clonedAgent4.AgentsAsTools[0].Description);
        Assert.Equal(factoryAgent4.AgentsAsTools[0].MaxTurns, clonedAgent4.AgentsAsTools[0].MaxTurns);
    }

    [Fact]
    public async Task CommonPromptsOverlayApplied()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "prompt_experiment=common_prompts_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt for testing common_prompts.", agent1.Instructions);

        // Verify the common prompt was added
        Assert.Contains("This is another test prompt (2).", agent1.Instructions);

        // Verify original common prompt from base agent is NOT present (apply_standard_modifiers: false)
        Assert.DoesNotContain("This is a test prompt.", agent1.Instructions);

        // Verify standard modifiers were NOT applied (apply_standard_modifiers: false)
        // This means the base agent's common prompts (like todo_write) should NOT be present
        // Note: We can't easily verify this without knowing what todo_write contains

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task HasHandoffInstructionsDisabled()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "prompt_experiment=no_handoff_instructions_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt without handoff instructions.", agent1.Instructions);

        // Verify handoff instructions were NOT added (has_handoff_instructions: false)
        // The PromptText.HasHandoffInstructions should be false
        Assert.False(agent1.Instructions.HasHandoffInstructions);

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }

    [Fact]
    public async Task ApplyStandardModifiersEnabled()
    {
        var toolFactory = CreateToolFactory();
        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, "prompt_experiment=apply_standard_modifiers_variant");
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<ChatClientProvider>(),
            assembliesToScan: [],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"),
            experimentsYamlDirectory: experimentsDir);
        await factory.InitializeAsync();
        var provider = new AgentProvider<AgentContext>(factory, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt with standard modifiers.", agent1.Instructions);

        // Verify standard modifiers WERE applied (apply_standard_modifiers: true by default)
        // This means handoff instructions should be present
        Assert.True(agent1.Instructions.HasHandoffInstructions);

        // Base agent's common prompts should be present (prompt1 is in the base agent's common_prompts)
        Assert.Contains("This is a test prompt.", agent1.Instructions);

        Environment.SetEnvironmentVariable(FrameworkConstants.ForceExperimentVariantsEnvVar, null);
    }
}
