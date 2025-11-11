// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Core.Services
{
    /// <summary>
    /// Service that provides access to different AI models based on the scenario
    /// </summary>
    public class ChatClientProvider : IChatClientProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ChatClientProviderSettings _chatClientProviderSettings;
        private readonly ILogger<ChatClientProvider> _logger;
        private readonly IList<string> _availableModels;

        private readonly string _generalPurposeModelName;
        private readonly string _reasoningHeavyModelName;
        private readonly string _reasoningFastModelName;
        private readonly string _largeContextModelName;
        private readonly string _smallFastModelName;
        private readonly string _evalModelName;
        private readonly string _embeddingModelName;

        public IChatClient GeneralPurposeModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving general purpose model: {ModelName}", _generalPurposeModelName);
                return GetModelByKey<IChatClient>(_generalPurposeModelName);
            }
        }

        public IChatClient ReasoningHeavyModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving reasoning heavy model: {ModelName}", _reasoningHeavyModelName);
                return GetModelByKey<IChatClient>(_reasoningHeavyModelName);
            }
        }

        public IChatClient ReasoningFastModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving reasoning fast model: {ModelName}", _reasoningFastModelName);
                return GetModelByKey<IChatClient>(_reasoningFastModelName);
            }
        }

        public IChatClient LargeContextModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving large context model: {ModelName}", _largeContextModelName);
                return GetModelByKey<IChatClient>(_largeContextModelName);
            }
        }

        public IChatClient SmallFastModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving small fast model: {ModelName}", _smallFastModelName);
                return GetModelByKey<IChatClient>(_smallFastModelName);
            }
        }

        public IChatClient EvalModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving eval model: {ModelName}", _evalModelName);
                return GetModelByKey<IChatClient>(_evalModelName);
            }
        }

        public IEmbeddingGenerator<string, Embedding<float>> EmbeddingModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving embedding model: {ModelName}", _embeddingModelName);
                return GetModelByKey<IEmbeddingGenerator<string, Embedding<float>>>(_embeddingModelName);
            }
        }

        public ChatClientProvider(
            IServiceProvider serviceProvider,
            IOptions<OpenAISettings> openAISettings,
            IOptions<ChatClientProviderSettings> chatClientProviderSettings,
            IOptions<AgentModelSettings> agentModelSettings,
            ILogger<ChatClientProvider> logger)
        {
            _serviceProvider = serviceProvider;
            _chatClientProviderSettings = chatClientProviderSettings.Value;
            _logger = logger;

            // Validate ChatClientProviderSettings
            if (_chatClientProviderSettings == null)
            {
                throw new ArgumentNullException(nameof(chatClientProviderSettings), "ChatClientProviderSettings cannot be null.");
            }

            ValidateScenarioConfiguration(_chatClientProviderSettings.ScenarioConfiguration);

            _availableModels = agentModelSettings.Value?.AvailableModelList.Count > 0 ? agentModelSettings.Value.AvailableModelList : _chatClientProviderSettings.ModelNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            // Resolve model names at construction time (cheap operation)
            _embeddingModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.EmbeddingModelName) ? openAISettings.Value.EmbeddingGeneratorDeploymentName : _chatClientProviderSettings.EmbeddingModelName;
            _generalPurposeModelName = GetBestModelNameByScenario(ModelScenarioType.GeneralPurpose);
            _reasoningHeavyModelName = GetBestModelNameByScenario(ModelScenarioType.ReasoningHeavy);
            _reasoningFastModelName = GetBestModelNameByScenario(ModelScenarioType.ReasoningFast);
            _largeContextModelName = GetBestModelNameByScenario(ModelScenarioType.LongContext);
            _smallFastModelName = GetBestModelNameByScenario(ModelScenarioType.SmallFast);
            _evalModelName = GetBestModelNameByScenario(ModelScenarioType.Eval);
        }

        /// <summary>
        /// Gets a model by its deployment name
        /// </summary>
        public T GetModelByKey<T>(string keyName) where T : notnull
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                throw new ArgumentException("Key name cannot be null or empty.", nameof(keyName));
            }
            _logger.LogInternalDebug("Retrieving model by key: {KeyName}", keyName);
            return _serviceProvider.GetRequiredKeyedService<T>(keyName);
        }

        /// <summary>
        /// Gets the best available model for a specific scenario type based on priority configuration
        /// </summary>
        public IChatClient GetBestModelByScenario(ModelScenarioType scenarioType)
        {
            var modelName = GetBestModelNameByScenario(scenarioType);
            return GetModelByKey<IChatClient>(modelName);
        }

        /// <summary>
        /// Gets the best available model for a specific scenario type based on priority configuration
        /// </summary>
        public string GetBestModelNameByScenario(ModelScenarioType scenarioType)
        {
            _logger.LogInternalDebug($"Selecting best model for scenario: {scenarioType}");

            var scenarioPriority = _chatClientProviderSettings.ScenarioConfiguration.GetScenarioPriority(scenarioType);

            if (scenarioPriority == null)
            {
                _logger.LogInternalWarning($"No scenario priority configuration found for {scenarioType}, using GeneralPurpose fallback");
                scenarioPriority = _chatClientProviderSettings.ScenarioConfiguration.GetScenarioPriority(ModelScenarioType.GeneralPurpose)!;
            }

            // If AvailableModelList is null or empty, skip PriorityModels and use scenario's DefaultModel
            if (_availableModels.Any())
            {
                // Find the first model from priority list that is in available models
                foreach (var priorityModel in scenarioPriority.PriorityModels)
                {
                    if (_availableModels.Contains(priorityModel, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInternalInformation($"Selected model {priorityModel} for scenario {scenarioType} from PriorityModels");
                        return priorityModel;
                    }
                }
            }

            _logger.LogInternalInformation($"No priority model available in AvailableModelList, using scenario default model {scenarioPriority.DefaultModel} for scenario {scenarioType}");
            return scenarioPriority.DefaultModel;
        }

        public bool IsModelSupported(string modelName)
        {
            return _availableModels.Contains(modelName);
        }

        public IList<string> GetAvailableModels()
        {
            return _availableModels;
        }

        /// <summary>
        /// Validates the scenario configuration to ensure all scenarios have valid priority models and default models
        /// </summary>
        /// <param name="config">The scenario configuration to validate</param>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        private void ValidateScenarioConfiguration(ModelScenarioConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "ScenarioConfiguration cannot be null.");
            }

            var errors = new List<string>();

            // Validate all required scenario types (except Embedding)  
            var requiredScenarios = Enum.GetValues<ModelScenarioType>()
                .Where(type => type != ModelScenarioType.Embedding);

            foreach (var scenarioType in requiredScenarios)
            {
                if (!config.ContainsKey(scenarioType))
                {
                    errors.Add($"{scenarioType}: Missing configuration.");
                    continue;
                }

                var priority = config[scenarioType];
                ValidateScenarioPriority(priority, scenarioType.ToString(), errors);
            }

            if (errors.Count > 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                throw new ArgumentException($"ScenarioConfiguration validation failed:{Environment.NewLine}{errorMessage}");
            }
        }

        /// <summary>
        /// Validates a single scenario priority configuration
        /// </summary>
        /// <param name="priority">The scenario priority to validate</param>
        /// <param name="scenarioName">The name of the scenario for error reporting</param>
        /// <param name="errors">List to collect validation errors</param>
        private void ValidateScenarioPriority(ModelScenarioPriority priority, string scenarioName, List<string> errors)
        {
            if (priority == null)
            {
                errors.Add($"{scenarioName}: Cannot be null.");
                return;
            }

            // Validate PriorityModels
            if (priority.PriorityModels == null || priority.PriorityModels.Count == 0)
            {
                errors.Add($"{scenarioName}.PriorityModels: Must contain at least one model.");
            }
            else
            {
                // Check for null or empty strings in PriorityModels
                var invalidModels = priority.PriorityModels
                    .Select((model, index) => new { model, index })
                    .Where(x => string.IsNullOrWhiteSpace(x.model))
                    .ToList();

                if (invalidModels.Any())
                {
                    var indices = string.Join(", ", invalidModels.Select(x => x.index));
                    errors.Add($"{scenarioName}.PriorityModels: Contains null or empty model names at indices: {indices}");
                }
            }

            // Validate DefaultModel
            if (string.IsNullOrWhiteSpace(priority.DefaultModel))
            {
                errors.Add($"{scenarioName}.DefaultModel: Cannot be null or empty.");
            }
        }
    }
}
