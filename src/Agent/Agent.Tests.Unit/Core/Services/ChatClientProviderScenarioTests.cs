// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Core.Services
{
    public class ChatClientProviderScenarioTests
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<ChatClientProvider>> _mockLogger;
        private readonly Mock<IChatClient> _mockChatClient;
        private readonly OpenAISettings _openAISettings;
        private readonly ChatClientProviderSettings _chatClientProviderSettings;
        private readonly AgentModelSettings _agentModelSettings;

        public ChatClientProviderScenarioTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<ChatClientProvider>>();
            _mockChatClient = new Mock<IChatClient>();

            _openAISettings = new OpenAISettings
            {
                LLMDeploymentName = "gpt-4o",
                EmbeddingGeneratorDeploymentName = "text-embedding-3-small"
            };

            _chatClientProviderSettings = new ChatClientProviderSettings
            {
                ModelNames = "gpt-4o,gpt-4o-mini,o1,gpt-35-turbo",
                ScenarioConfiguration = new ModelScenarioConfiguration
                {
                    [ModelScenarioType.ReasoningHeavy] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "o1", "o3-mini", "gpt-4o" },
                        DefaultModel = "gpt-4o"
                    },
                    [ModelScenarioType.ReasoningFast] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "gpt-4o-mini", "gpt-4o", "o1-mini" },
                        DefaultModel = "gpt-4o-mini"
                    },
                    [ModelScenarioType.GeneralPurpose] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-35-turbo" },
                        DefaultModel = "gpt-4o"
                    },
                    [ModelScenarioType.SmallFast] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "gpt-4o-mini", "gpt-35-turbo", "gpt-4o" },
                        DefaultModel = "gpt-4o-mini"
                    },
                    [ModelScenarioType.LongContext] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "gpt-4o", "gpt-4-turbo-128k", "o1" },
                        DefaultModel = "gpt-4o"
                    },
                    [ModelScenarioType.Eval] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "o1", "gpt-4o", "gpt-4-turbo" },
                        DefaultModel = "gpt-4o"
                    },
                    [ModelScenarioType.Embedding] = new ModelScenarioPriority
                    {
                        PriorityModels = new List<string> { "text-embedding-3-large", "text-embedding-3-small", "text-embedding-ada-002" },
                        DefaultModel = "text-embedding-3-small"
                    }
                }
            };

            _agentModelSettings = new AgentModelSettings
            {
                GPT5Enabled = false,
                AvailableModels = "gpt-4o, gpt-4o-mini,o1"
            };

            // Setup service provider to return mock chat client for any model key
            _mockServiceProvider
                .Setup(sp => sp.GetService(typeof(IChatClient)))
                .Returns(_mockChatClient.Object);

            // Setup keyed service provider - need to implement IKeyedServiceProvider
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), It.IsAny<object?>()))
                .Returns(_mockChatClient.Object);
        }

        [Fact]
        public void GetBestModelByScenario_SelectsTopPriorityAvailableModel()
        {
            // Arrange - o1 is top priority and is available
            var mockO1Client = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"))
                .Returns(mockO1Client.Object);

            var provider = CreateProvider();

            // Act
            var result = provider.GetBestModelByScenario(ModelScenarioType.ReasoningHeavy);

            // Assert
            Assert.NotNull(result);
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"), Times.Once);
        }

        [Fact]
        public void GetBestModelByScenario_SkipsUnavailableModels()
        {
            // Arrange - o3-mini is not in available models, should skip to gpt-4o
            _agentModelSettings.AvailableModels = "gpt-4o, gpt-4o-mini";
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = new List<string> { "o1", "o3-mini", "gpt-4o" };

            var mockGpt4oClient = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"))
                .Returns(mockGpt4oClient.Object);

            var provider = CreateProvider();

            // Act
            var result = provider.GetBestModelByScenario(ModelScenarioType.ReasoningHeavy);

            // Assert
            Assert.NotNull(result);
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"), Times.Once);
        }

        [Fact]
        public void GetBestModelByScenario_UsesDefaultWhenNoPriorityAvailable()
        {
            // Arrange - None of the priority models are available
            _agentModelSettings.AvailableModels = "gpt-35-turbo";
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = new List<string> { "o1", "o3-mini" };
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].DefaultModel = "gpt-4o";

            var mockDefaultClient = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"))
                .Returns(mockDefaultClient.Object);

            var provider = CreateProvider();

            // Act
            var result = provider.GetBestModelByScenario(ModelScenarioType.ReasoningHeavy);

            // Assert
            Assert.NotNull(result);
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"), Times.Once);
        }

        [Fact]
        public void GetBestModelByScenario_UsesAllSupportedWhenAvailableModelsEmpty()
        {
            // Arrange - Empty AvailableModelList means all supported models are available
            _agentModelSettings.AvailableModels = string.Empty;

            var mockGpt4oMiniClient = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"))
                .Returns(mockGpt4oMiniClient.Object);

            var provider = CreateProvider();

            // Act
            var result = provider.GetBestModelByScenario(ModelScenarioType.ReasoningFast);

            // Assert
            Assert.NotNull(result);
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"), Times.Once);
        }

        [Fact]
        public void GetBestModelByScenario_DifferentScenariosSelectDifferentModels()
        {
            // Arrange
            var mockO1Client = new Mock<IChatClient>();
            var mockGpt4oMiniClient = new Mock<IChatClient>();

            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();

            // Set up specific mocks before creating the provider
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"))
                .Returns(mockO1Client.Object);

            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"))
                .Returns(mockGpt4oMiniClient.Object);

            var provider = CreateProvider();

            // Act
            var reasoningHeavyModel = provider.GetBestModelByScenario(ModelScenarioType.ReasoningHeavy);
            var reasoningFastModel = provider.GetBestModelByScenario(ModelScenarioType.ReasoningFast);

            // Assert
            Assert.NotNull(reasoningHeavyModel);
            Assert.NotNull(reasoningFastModel);

            // Verify each model was requested exactly once
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"), Times.Once);
            mockKeyedServiceProvider.Verify(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"), Times.Once);
        }

        private ChatClientProvider CreateProvider()
        {
            return new ChatClientProvider(
                _mockServiceProvider.Object,
                Options.Create(_openAISettings),
                Options.Create(_chatClientProviderSettings),
                Options.Create(_agentModelSettings),
                _mockLogger.Object
            );
        }

        #region AvailableModels and ModelNames Fallback Tests

        [Fact]
        public void Constructor_FallsBackToModelNames_WhenAvailableModelsIsEmpty()
        {
            // Arrange - No AvailableModels, but has ModelNames
            _agentModelSettings.AvailableModels = string.Empty;
            _chatClientProviderSettings.ModelNames = "gpt-4o,gpt-4o-mini,o1";

            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"))
                .Returns(_mockChatClient.Object);

            // Act
            var provider = CreateProvider();
            var availableModels = provider.GetAvailableModels();

            // Assert
            Assert.NotNull(availableModels);
            Assert.Equal(3, availableModels.Count);
            Assert.Contains("gpt-4o", availableModels);
            Assert.Contains("gpt-4o-mini", availableModels);
            Assert.Contains("o1", availableModels);

            // Verify model selection uses ModelNames
            Assert.True(provider.IsModelSupported("gpt-4o-mini"));
            Assert.True(provider.IsModelSupported("o1"));
        }

        [Fact]
        public void Constructor_UsesAvailableModels_WhenBothAvailableModelsAndModelNamesExist()
        {
            // Arrange - Both AvailableModels and ModelNames exist, should use AvailableModels
            _agentModelSettings.AvailableModels = "gpt-4o,o1";
            _chatClientProviderSettings.ModelNames = "gpt-4o,gpt-4o-mini,o1,gpt-35-turbo";

            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"))
                .Returns(_mockChatClient.Object);

            // Act
            var provider = CreateProvider();
            var availableModels = provider.GetAvailableModels();

            // Assert
            Assert.NotNull(availableModels);
            Assert.Equal(2, availableModels.Count); // Only models from AvailableModels
            Assert.Contains("gpt-4o", availableModels);
            Assert.Contains("o1", availableModels);

            // Models from ModelNames that aren't in AvailableModels should not be supported
            Assert.True(provider.IsModelSupported("gpt-4o"));
            Assert.True(provider.IsModelSupported("o1"));
            Assert.False(provider.IsModelSupported("gpt-4o-mini"));
            Assert.False(provider.IsModelSupported("gpt-35-turbo"));
        }

        [Fact]
        public void Constructor_UsesDefaultModel_WhenNeitherAvailableModelsNorModelNamesExist()
        {
            // Arrange - No AvailableModels and no ModelNames
            _agentModelSettings.AvailableModels = string.Empty;
            _chatClientProviderSettings.ModelNames = string.Empty;

            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"))
                .Returns(_mockChatClient.Object);

            // Act
            var provider = CreateProvider();
            var availableModels = provider.GetAvailableModels();

            // Assert
            Assert.NotNull(availableModels);
            Assert.Empty(availableModels); // No available models configured

            // Should fall back to DefaultModel when selecting models
            var modelName = provider.GetBestModelNameByScenario(ModelScenarioType.ReasoningHeavy);
            Assert.Equal("gpt-4o", modelName); // Uses DefaultModel from scenario configuration
        }

        [Fact]
        public void Constructor_OnlyUsesAvailableModels_WhenModelNamesIsEmpty()
        {
            // Arrange - Only AvailableModels exists, ModelNames is empty
            _agentModelSettings.AvailableModels = "gpt-4o,o1,gpt-4o-mini";
            _chatClientProviderSettings.ModelNames = string.Empty;

            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "o1"))
                .Returns(_mockChatClient.Object);

            // Act
            var provider = CreateProvider();
            var availableModels = provider.GetAvailableModels();

            // Assert
            Assert.NotNull(availableModels);
            Assert.Equal(3, availableModels.Count);
            Assert.Contains("gpt-4o", availableModels);
            Assert.Contains("o1", availableModels);
            Assert.Contains("gpt-4o-mini", availableModels);

            // Verify model selection uses AvailableModels
            Assert.True(provider.IsModelSupported("gpt-4o"));
            Assert.True(provider.IsModelSupported("o1"));
            Assert.True(provider.IsModelSupported("gpt-4o-mini"));
        }

        [Fact]
        public void GetBestModelByScenario_SelectsFromModelNames_WhenAvailableModelsIsEmpty()
        {
            // Arrange - No AvailableModels, fallback to ModelNames
            _agentModelSettings.AvailableModels = string.Empty;
            _chatClientProviderSettings.ModelNames = "gpt-4o,gpt-4o-mini";
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningFast].PriorityModels = new List<string> { "gpt-4o-mini", "gpt-4o" };

            var mockGpt4oMiniClient = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o-mini"))
                .Returns(mockGpt4oMiniClient.Object);

            var provider = CreateProvider();

            // Act
            var modelName = provider.GetBestModelNameByScenario(ModelScenarioType.ReasoningFast);

            // Assert
            Assert.Equal("gpt-4o-mini", modelName);
        }

        [Fact]
        public void GetBestModelByScenario_PrioritizesAvailableModels_OverModelNames()
        {
            // Arrange - Both exist, AvailableModels should be used
            _agentModelSettings.AvailableModels = "gpt-4o";
            _chatClientProviderSettings.ModelNames = "gpt-4o,gpt-4o-mini,o1";
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningFast].PriorityModels = new List<string> { "gpt-4o-mini", "gpt-4o" };

            var mockGpt4oClient = new Mock<IChatClient>();
            var mockKeyedServiceProvider = _mockServiceProvider.As<IKeyedServiceProvider>();
            mockKeyedServiceProvider
                .Setup(sp => sp.GetRequiredKeyedService(typeof(IChatClient), "gpt-4o"))
                .Returns(mockGpt4oClient.Object);

            var provider = CreateProvider();

            // Act
            var modelName = provider.GetBestModelNameByScenario(ModelScenarioType.ReasoningFast);

            // Assert - Should skip gpt-4o-mini (not in AvailableModels) and use gpt-4o
            Assert.Equal("gpt-4o", modelName);
        }
        #endregion

        #region Validation Tests

        [Fact]
        public void Constructor_ThrowsWhenScenarioConfigurationIsNull()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration = null!;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => CreateProvider());
            Assert.Contains("ScenarioConfiguration cannot be null", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenScenarioIsNull()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy] = null!;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy: Cannot be null", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenPriorityModelsIsNull()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = null!;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.PriorityModels: Must contain at least one model", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenPriorityModelsIsEmpty()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = new List<string>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.PriorityModels: Must contain at least one model", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenPriorityModelsContainsEmptyString()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = new List<string> { "gpt-4o", "", "o1" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.PriorityModels: Contains null or empty model names", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenPriorityModelsContainsNullString()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].PriorityModels = new List<string> { "gpt-4o", null!, "o1" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.PriorityModels: Contains null or empty model names", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenDefaultModelIsNull()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].DefaultModel = null!;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.DefaultModel: Cannot be null or empty", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenDefaultModelIsEmpty()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].DefaultModel = string.Empty;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.DefaultModel: Cannot be null or empty", exception.Message);
        }

        [Fact]
        public void Constructor_ThrowsWhenMultipleScenariosAreInvalid()
        {
            // Arrange
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningHeavy].DefaultModel = string.Empty;
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.ReasoningFast].PriorityModels = new List<string>();
            _chatClientProviderSettings.ScenarioConfiguration[ModelScenarioType.SmallFast].DefaultModel = null!;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => CreateProvider());
            Assert.Contains("ReasoningHeavy.DefaultModel: Cannot be null or empty", exception.Message);
            Assert.Contains("ReasoningFast.PriorityModels: Must contain at least one model", exception.Message);
            Assert.Contains("SmallFast.DefaultModel: Cannot be null or empty", exception.Message);
        }

        [Fact]
        public void Constructor_SucceedsWithValidConfiguration()
        {
            // Arrange - use default valid configuration from constructor

            // Act & Assert - should not throw
            var provider = CreateProvider();
            Assert.NotNull(provider);
        }

        #endregion
    }
}
