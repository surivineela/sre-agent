// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
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
        private readonly OpenAISettings _openAISettings;
        private readonly ChatClientProviderSettings _chatClientProviderSettings;
        private readonly ILogger<ChatClientProvider> _logger;
        private readonly IList<string> _supportedModels;

        private readonly string _defaultModelName;
        private readonly string _reasoningModelName;
        private readonly string _fastModelName;
        private readonly string _largeContextModelName;
        private readonly string _embeddingModelName;

        public IChatClient DefaultModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving default model: {ModelName}", _defaultModelName);
                return GetModelByKey<IChatClient>(_defaultModelName);
            }
        }

        public IChatClient ReasoningModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving reasoning model: {ModelName}", _reasoningModelName);
                return GetModelByKey<IChatClient>(_reasoningModelName);
            }
        }

        public IChatClient FastModel
        {
            get
            {
                _logger.LogInternalDebug("Retrieving fast model: {ModelName}", _fastModelName);
                return GetModelByKey<IChatClient>(_fastModelName);
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
            ILogger<ChatClientProvider> logger)
        {
            _serviceProvider = serviceProvider;
            _openAISettings = openAISettings.Value;
            _chatClientProviderSettings = chatClientProviderSettings.Value;
            _logger = logger;
            _supportedModels = _chatClientProviderSettings.ModelNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // backward compatibility for default model and embedding model
            _defaultModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.DefaultModelName) ? _openAISettings.LLMDeploymentName : _chatClientProviderSettings.DefaultModelName;
            _embeddingModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.EmbeddingModelName) ? _openAISettings.EmbeddingGeneratorDeploymentName : _chatClientProviderSettings.EmbeddingModelName;

            // defaults to default model if not specified
            _reasoningModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.ReasoningModelName) ? _defaultModelName : _chatClientProviderSettings.ReasoningModelName;
            _fastModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.FastModelName) ? _defaultModelName : _chatClientProviderSettings.FastModelName;
            _largeContextModelName = string.IsNullOrWhiteSpace(_chatClientProviderSettings.LargeContextModelName) ? _defaultModelName : _chatClientProviderSettings.LargeContextModelName;
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

        public bool IsModelSupported(string modelName)
        {
            return _supportedModels.Contains(modelName);
        }

        public IList<string> GetSupportedModels()
        {
            return _supportedModels;
        }
    }
}
