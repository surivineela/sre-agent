// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Framework.Skills;
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
        _services.AddSingleton<IChatClientProvider, ChatClientProvider>();
        _services.AddLogging();
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

        var modelNames = "gpt-5,gpt-5-mini";
        List<string> models = modelNames.Split(',').ToList();
        var firstMode = models.First();
        foreach (var name in models)
        {
            _services.AddKeyedSingleton(name, Mock.Of<IChatClient>());
        }
        var embeddingModelName = "text-embedding-3-large";
        _services.Configure<ChatClientProviderSettings>(o =>
        {
            o.ScenarioConfiguration = new ModelScenarioConfiguration
            {
                [ModelScenarioType.GeneralPurpose] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.ReasoningHeavy] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.ReasoningFast] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.SmallFast] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.LongContext] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.Eval] = new ModelScenarioPriority
                {
                    PriorityModels = models,
                    DefaultModel = firstMode
                },
                [ModelScenarioType.Embedding] = new ModelScenarioPriority
                {
                    PriorityModels = new List<string> { embeddingModelName },
                    DefaultModel = embeddingModelName
                }
            };
        });
        _services.Configure<OpenAISettings>(o =>
        {
            o.LLMDeploymentName = modelNames.Split(',').First();
        });
    }

    private ToolFactory<AgentContext> CreateToolFactory()
    {
        var yamlToolFunctionFactory = new YamlToolFunctionFactory<AgentContext>(
            _serviceProvider,
            _serviceProvider.GetServices<Agent.Plugins.Tools.IYamlToolExecutorFactory>());

        return new ToolFactory<AgentContext>(
            logger: _mockToolFactoryLogger.Object,
            serviceProvider: _serviceProvider,
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            extensibilityLoader: _mockExtensibilityLoader.Object,
            mcpToolsRepository: _mockMcpToolsRepository.Object,
            skillRegistry: CreateSkillRegistry(),
            yamlToolFunctionFactory: yamlToolFunctionFactory
        );
    }

    private SkillRegistry CreateSkillRegistry()
    {
        return new SkillRegistry(
            logger: Mock.Of<ILogger<SkillRegistry>>(),
            systemSkillsDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestSkills"),
            extensibilityLoader: _mockExtensibilityLoader.Object
        );
    }

    [Fact]
    public async Task AppliesForcedVariantPromptOverlay()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("prompt_experiment=prompt_overlay");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.Contains("Replaced system prompt for agent1.", agent1.Instructions.ToString());
        Assert.Contains("Prepended text.", agent1.Instructions.ToString());
        Assert.Contains("Appended text.", agent1.Instructions.ToString());
        Assert.NotNull(agent1.HandoffDescription);
        Assert.Equal("New handoff instructions.".Trim(), agent1.HandoffDescription.ToString().Trim());
    }

    [Fact]
    public async Task AppliesToolOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("tool_experiment=tools_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        Assert.Contains("ReplaceToolA", agent1.FactoryTools);
        Assert.Contains("ReplaceToolB", agent1.FactoryTools);
        Assert.DoesNotContain("TestAutoTool", agent1.FactoryTools);
        Assert.Contains("ExtraTool1", agent2.FactoryTools);
        Assert.Contains("ExtraTool2", agent2.FactoryTools);
        Assert.DoesNotContain("TestManualTool", agent2.FactoryTools);
    }

    [Fact]
    public async Task AppliesHandoffOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("handoff_experiment=handoffs_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        var agent3 = provider.GetAgent("agent3");
        Assert.Single(agent1.Handoffs);
        Assert.Equal("agent3", agent1.Handoffs[0].AgentName);
        Assert.Empty(agent2.Handoffs);
        Assert.Single(agent3.Handoffs);
        Assert.Equal("agent1", agent3.Handoffs[0].AgentName);
    }

    [Fact]
    public async Task AppliesParamOverlayOperations()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("param_experiment=params_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        var agent2 = provider.GetAgent("agent2");
        Assert.Equal("high", agent1.ReasoningEffortLevel);
        Assert.Equal("high", agent2.ReasoningEffortLevel);
    }

    private sealed class TestVariantAssignerNotInExperiment : IVariantAssigner
    {
        public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null) => new(experiment.ExperimentId, "prompt_overlay", false);
    }

    [Fact]
    public async Task DoesNotApplyOverlayWhenNotInExperiment()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new TestVariantAssignerNotInExperiment(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new TestVariantAssignerAlwaysIn(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("DISABLED_EXPERIMENT_SHOULD_NOT_APPLY", agent1.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, assigner, _mockProviderLogger.Object, "test-instance-for-coverage0");
        var agent1 = provider.GetAgent("agent1");
        Assert.DoesNotContain("COVERAGE_SHOULD_NOT_APPLY", agent1.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "global-instance");
        var agentThread1 = provider.GetAgent("agent1", threadId: "thread-1");
        var agentThread2 = provider.GetAgent("agent1", threadId: "thread-2");
        Assert.Contains("GLOBAL_VARIANT_APPLIED", agentThread1.Instructions.ToString());
        Assert.Contains("GLOBAL_VARIANT_APPLIED", agentThread2.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "per-thread-instance");
        var agentA = provider.GetAgent("agent1", threadId: "threadA");
        var agentB = provider.GetAgent("agent1", threadId: "threadB");
        Assert.Contains("PER_THREAD_VARIANT_A", agentA.Instructions.ToString());
        Assert.Contains("PER_THREAD_VARIANT_B", agentB.Instructions.ToString());
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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "cache-test-instance");

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new DeterministicThreadVariantAssigner(), _mockProviderLogger.Object, "cache-diff-instance");

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
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddDisabledExperiments("prompt_experiment;tool_experiment");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment and tool_experiment should NOT be applied
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions.ToString());
        Assert.DoesNotContain("ReplaceToolA", agent1.FactoryTools);

        // Get active variants - disabled experiments should not appear
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);
        Assert.DoesNotContain("tool_experiment", variants.Keys);

    }

    [Fact]
    public async Task ForceDisableSpecificExperimentWhileOthersStillApply()
    {
        var toolFactory = CreateToolFactory();
        // Disable only prompt_experiment, but keep tool_experiment enabled
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddDisabledExperiments("prompt_experiment");
        experimentLoader.ParseAndAddForcedVariants("tool_experiment=tools_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment should NOT be applied (disabled)
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions.ToString());

        // tool_experiment SHOULD be applied (not disabled, and forced)
        Assert.Contains("ReplaceToolA", agent1.FactoryTools);
        Assert.Contains("ReplaceToolB", agent1.FactoryTools);

        // Get active variants
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);
        Assert.Contains("tool_experiment", variants.Keys);

    }

    [Fact]
    public async Task ForceDisableOverridesForcedVariants()
    {
        var toolFactory = CreateToolFactory();
        // Try to force a variant for an experiment that's also disabled - disable should win
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddDisabledExperiments("prompt_experiment");
        experimentLoader.ParseAndAddForcedVariants("prompt_experiment=prompt_overlay");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        var agent1 = provider.GetAgent("agent1");

        // prompt_experiment should NOT be applied even though it's forced (disabled takes precedence)
        Assert.DoesNotContain("Replaced system prompt for agent1.", agent1.Instructions.ToString());

        // Get active variants - disabled experiment should not appear
        var variants = provider.GetActiveVariants("test-thread");
        Assert.DoesNotContain("prompt_experiment", variants.Keys);

    }

    [Fact]
    public async Task AgentAsToolIsProperlyCloned()
    {
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

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
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

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
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("prompt_experiment=common_prompts_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt for testing common_prompts.", agent1.Instructions.ToString());

        // Verify the common prompt was added
        Assert.Contains("This is another test prompt (2).", agent1.Instructions.ToString());

        // Verify original common prompt from base agent is NOT present (apply_standard_modifiers: false)
        Assert.DoesNotContain("This is a test prompt.", agent1.Instructions.ToString());

        // Verify standard modifiers were NOT applied (apply_standard_modifiers: false)
        // This means the base agent's common prompts (like todo_write) should NOT be present
        // Note: We can't easily verify this without knowing what todo_write contains

    }

    [Fact]
    public async Task HasHandoffInstructionsDisabled()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("prompt_experiment=no_handoff_instructions_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt without handoff instructions.", agent1.Instructions.ToString());

        // Verify handoff instructions were NOT added (has_handoff_instructions: false)
        // The PromptText.HasHandoffInstructions should be false
        Assert.False(agent1.Instructions.HasHandoffInstructions);

    }

    [Fact]
    public async Task ApplyStandardModifiersEnabled()
    {
        var toolFactory = CreateToolFactory();
        var experimentsDir = Path.Combine(AppContext.BaseDirectory, "Framework", "TestExperiments");
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance", experimentsDir);
        experimentLoader.ParseAndAddForcedVariants("prompt_experiment=apply_standard_modifiers_variant");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");
        var agent1 = provider.GetAgent("agent1");

        // Verify the system prompt was replaced
        Assert.Contains("Replaced system prompt with standard modifiers.", agent1.Instructions.ToString());

        // Verify standard modifiers WERE applied (apply_standard_modifiers: true by default)
        // This means handoff instructions should be present
        Assert.True(agent1.Instructions.HasHandoffInstructions);

        // Base agent's common prompts should be present (prompt1 is in the base agent's common_prompts)
        Assert.Contains("This is a test prompt.", agent1.Instructions.ToString());

    }

    [Fact]
    public async Task CacheIsInvalidatedWhenDynamicAgentIsAdded()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Act - Get an agent to populate the cache
        var agent1Before = provider.GetAgent("agent1");
        Assert.NotNull(agent1Before);

        // Add a dynamic agent (should trigger cache invalidation)
        var yamlContent = @"
name: dynamic_test_agent
system_prompt: Test dynamic agent instructions
tools:
  - TestAutoTool
handoffs: []
";
        factory.LoadAgentFromYamlContent(yamlContent, isCustomAgent: true);

        // Assert - Verify we can now get the dynamic agent
        var dynamicAgent = provider.GetAgent("dynamic_test_agent");
        Assert.NotNull(dynamicAgent);
        Assert.Equal("dynamic_test_agent", dynamicAgent.Name);
        Assert.Contains("Test dynamic agent instructions", dynamicAgent.Instructions.ToString());
    }

    [Fact]
    public async Task CacheIsInvalidatedWhenAgentIsUpdated()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Get agent1 to populate the cache
        var agent1Before = provider.GetAgent("agent1");
        var originalInstructions = agent1Before.Instructions.ToString();

        // Act - Update agent1 with new instructions
        factory.LoadYamlAgentsFromFolder(
            Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            overwriteExistingAgents: true,
            recursive: false);

        // Assert - Get agent1 again, it should have fresh data from the rebuilt cache
        var agent1After = provider.GetAgent("agent1");
        Assert.NotNull(agent1After);
        // Instructions should be the same as we loaded from the same folder
        // but the important thing is the cache was invalidated and rebuilt
        Assert.NotSame(agent1Before, agent1After);
    }

    [Fact]
    public async Task MultipleDynamicAgentRegistrationsInvalidateCache()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: null);
        await factory.InitializeAsync();

        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Act - Add multiple dynamic agents
        for (int i = 1; i <= 3; i++)
        {
            var yamlContent = $@"
name: dynamic_agent_{i}
system_prompt: Agent {i} instructions
tools:
  - TestAutoTool
handoffs: []
";
            factory.LoadAgentFromYamlContent(yamlContent, isCustomAgent: true);
        }

        // Assert - Verify all dynamic agents are accessible
        for (int i = 1; i <= 3; i++)
        {
            var agent = provider.GetAgent($"dynamic_agent_{i}");
            Assert.NotNull(agent);
            Assert.Equal($"dynamic_agent_{i}", agent.Name);
            Assert.Contains($"Agent {i} instructions", agent.Instructions.ToString());
        }
    }

    [Fact]
    public async Task CacheInvalidationWorksAcrossMultipleThreads()
    {
        // Arrange
        var toolFactory = CreateToolFactory();
        var factory = new AgentFactory<AgentContext>(
            logger: _mockFactoryLogger.Object,
            toolFactory: toolFactory,
            chatClientProvider: _serviceProvider.GetRequiredService<IChatClientProvider>(),
            assembliesToScan: [Assembly.GetExecutingAssembly()],
            modeConfigurator: _mockAgentModeConfigurator.Object,
            agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestAgents"),
            commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestPrompts"),
            commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "Framework", "TestCommonTools"));
        await factory.InitializeAsync();
        var experimentLoader = new ExperimentLoader(_serviceProvider.GetRequiredService<ILogger<ExperimentLoader>>(), new HashVariantAssigner(), "test-instance");
        var provider = new AgentProvider<AgentContext>(factory, experimentLoader, new HashVariantAssigner(), _mockProviderLogger.Object, "test-instance");

        // Populate cache for multiple threads
        var agent1Thread1Before = provider.GetAgent("agent1", threadId: "thread-1");
        var agent1Thread2Before = provider.GetAgent("agent1", threadId: "thread-2");
        Assert.NotNull(agent1Thread1Before);
        Assert.NotNull(agent1Thread2Before);

        // Act - Add a dynamic agent (should invalidate ALL cached graphs)
        var yamlContent = @"
name: dynamic_cross_thread_agent
system_prompt: Cross-thread dynamic agent
tools:
  - TestAutoTool
handoffs: []
";
        factory.LoadAgentFromYamlContent(yamlContent, isCustomAgent: true);

        // Assert - Both threads should be able to access the new agent
        var dynamicAgentThread1 = provider.GetAgent("dynamic_cross_thread_agent", threadId: "thread-1");
        var dynamicAgentThread2 = provider.GetAgent("dynamic_cross_thread_agent", threadId: "thread-2");

        Assert.NotNull(dynamicAgentThread1);
        Assert.NotNull(dynamicAgentThread2);
        Assert.Equal("dynamic_cross_thread_agent", dynamicAgentThread1.Name);
        Assert.Equal("dynamic_cross_thread_agent", dynamicAgentThread2.Name);
    }
}
