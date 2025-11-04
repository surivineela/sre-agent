// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime
{
    public static class TokenLoggingChatClientExtensions
    {
        public static ChatClientBuilder UseTokenLogging(this ChatClientBuilder builder, ILoggerFactory loggerFactory)
        {
            return builder.Use((IChatClient inner, IServiceProvider services) =>
            {
                return new TokenLoggingChatClient(inner, loggerFactory.CreateLogger<TokenLoggingChatClient>());
            });
        }
    }

    internal class TokenLoggingChatClient : IChatClient
    {
        private readonly IChatClient _inner;
        private readonly ILogger<TokenLoggingChatClient> _logger;

        public TokenLoggingChatClient(
            IChatClient inner,
            ILogger<TokenLoggingChatClient> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _inner.GetResponseAsync(chatMessages, options, cancellationToken);

            try
            {
                var modelId = response?.ModelId?.ToString() ?? string.Empty;
                var inputTokens = response?.Usage?.InputTokenCount ?? 0L;
                var outputTokens = response?.Usage?.OutputTokenCount ?? 0L;

                // Extract cached token count from AdditionalCounts (same pattern as ReasoningLoop.cs)
                var cachedTokens = 0L;
                try
                {
                    if (response?.Usage?.AdditionalCounts is not null)
                    {
                        response.Usage.AdditionalCounts.TryGetValue("InputTokenDetails.CachedTokenCount", out cachedTokens);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalDebug("Failed to parse cached token count from AdditionalCounts: {Exception}", ex);
                }

                // Extract reasoning token count from AdditionalCounts (same pattern as ReasoningLoop.cs)
                var reasoningTokens = 0L;
                try
                {
                    if (response?.Usage?.AdditionalCounts is not null)
                    {
                        response.Usage.AdditionalCounts.TryGetValue("OutputTokenDetails.ReasoningTokenCount", out reasoningTokens);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalDebug("Failed to parse reasoning token count from AdditionalCounts: {Exception}", ex);
                }

                // Split modelId into Model and ModelVersion when in format "model-modelVersion" (modelVersion is a date like 2025-04-14)
                // Prefer parsing the trailing date first because model names can contain hyphens (e.g. gpt-4.1-2025-04-14).
                string model = modelId;
                string modelVersion = string.Empty;

                if (!string.IsNullOrEmpty(modelId))
                {
                    // Match a trailing date YYYY-MM-DD and capture the preceding model name (handles hyphens in model)
                    var m = System.Text.RegularExpressions.Regex.Match(modelId, "^(.*)-(\\d{4}-\\d{2}-\\d{2})$");
                    if (m.Success && m.Groups.Count == 3)
                    {
                        model = m.Groups[1].Value;
                        modelVersion = m.Groups[2].Value;
                    }
                    else
                    {
                        // Fallback: put full modelId into model and leave modelVersion empty
                        model = modelId;
                        modelVersion = string.Empty;
                    }
                }

                // Log token consumption (structured record with model and modelVersion)
                _logger.LogTokenConsumption(model, modelVersion, inputTokens, outputTokens, cachedTokens, reasoningTokens);
            }
            catch (Exception ex)
            {
                _logger.LogInternalDebug("Failed to log token usage: {Exception}", ex);
            }

            // Ensure we never return null to satisfy callers expecting a ChatResponse
            if (response == null)
            {
                return new ChatResponse(new List<ChatMessage>());
            }

            return response;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return _inner.GetService(serviceType, serviceKey);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string modelId = string.Empty;
            long inputTokens = 0L;
            long outputTokens = 0L;
            long cachedTokens = 0L;
            long reasoningTokens = 0L;

            await foreach (var update in _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            {
                // Capture model ID from updates
                var tempModelId = (update.RawRepresentation as OpenAI.Chat.StreamingChatCompletionUpdate)?.Model;
                if (!string.IsNullOrEmpty(tempModelId))
                {
                    modelId = tempModelId;
                }

                // Extract usage information from UsageContent in the update contents
                foreach (var content in update.Contents)
                {
                    var usage = content as UsageContent;
                    if (usage != null)
                    {
                        if (usage.Details.InputTokenCount.HasValue)
                        {
                            inputTokens += usage.Details.InputTokenCount.Value;
                        }

                        if (usage.Details.OutputTokenCount.HasValue)
                        {
                            outputTokens += usage.Details.OutputTokenCount.Value;
                        }

                        // Extract cached token count from AdditionalCounts
                        try
                        {
                            if (usage.Details.AdditionalCounts is not null)
                            {
                                if (usage.Details.AdditionalCounts.TryGetValue("InputTokenDetails.CachedTokenCount", out var cached))
                                {
                                    cachedTokens += cached;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalDebug("Failed to parse cached token count from streaming update: {Exception}", ex);
                        }

                        // Extract reasoning token count from AdditionalCounts
                        try
                        {
                            if (usage.Details.AdditionalCounts is not null)
                            {
                                if (usage.Details.AdditionalCounts.TryGetValue("OutputTokenDetails.ReasoningTokenCount", out var reasoning))
                                {
                                    reasoningTokens += reasoning;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalDebug("Failed to parse reasoning token count from streaming update: {Exception}", ex);
                        }
                    }
                }

                yield return update;
            }

            // Log accumulated token consumption at the end of the stream
            try
            {
                // Split modelId into Model and ModelVersion when in format "model-modelVersion" (modelVersion is a date like 2025-04-14)
                string model = modelId;
                string modelVersion = string.Empty;

                if (!string.IsNullOrEmpty(modelId))
                {
                    // Match a trailing date YYYY-MM-DD and capture the preceding model name (handles hyphens in model)
                    var m = System.Text.RegularExpressions.Regex.Match(modelId, "^(.*)-(\\d{4}-\\d{2}-\\d{2})$");
                    if (m.Success && m.Groups.Count == 3)
                    {
                        model = m.Groups[1].Value;
                        modelVersion = m.Groups[2].Value;
                    }
                    else
                    {
                        // Fallback: put full modelId into model and leave modelVersion empty
                        model = modelId;
                        modelVersion = string.Empty;
                    }
                }

                // Log token consumption (structured record with model and modelVersion)
                _logger.LogTokenConsumption(model, modelVersion, inputTokens, outputTokens, cachedTokens, reasoningTokens);
            }
            catch (Exception ex)
            {
                _logger.LogInternalDebug("Failed to log streaming token usage: {Exception}", ex);
            }
        }
    }
}
