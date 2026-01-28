// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Framework;
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
        // Pre-compiled regex to match trailing date YYYY-MM-DD or YYYYMMDD in model IDs
        private static readonly Regex ModelVersionRegex = new(
            @"^(.*)-((\d{4}-\d{2}-\d{2})|(\d{8}))$",
            RegexOptions.Compiled);

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
                var cachedTokens = response?.Usage?.CachedInputTokenCount ?? 0L;
                var reasoningTokens = response?.Usage?.ReasoningTokenCount ?? 0L;

                // Split modelId into Model and ModelVersion when in format "model-modelVersion" (modelVersion is a date like 2025-04-14 or 20250414)
                // Prefer parsing the trailing date first because model names can contain hyphens (e.g. gpt-4.1-2025-04-14).
                string model = modelId;
                string modelVersion = string.Empty;

                if (!string.IsNullOrEmpty(modelId))
                {
                    var m = ModelVersionRegex.Match(modelId);
                    if (m.Success && m.Groups.Count >= 3)
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

                var effectiveReasoningEffort = GetEffectiveReasoningEffort(options, model);

                // Log token consumption (structured record with model and modelVersion)
                _logger.LogTokenConsumption(model, modelVersion, inputTokens, outputTokens, cachedTokens, 0L, reasoningTokens, effectiveReasoningEffort);
            }
            catch (Exception ex)
            {
                _logger.LogInternalDebug("Failed to log token usage: {Exception}", ex);
            }

            // Ensure we never return null to satisfy callers expecting a ChatResponse
            if (response == null)
            {
                return new ChatResponse([]);
            }

            return response;
        }

        private static string GetEffectiveReasoningEffort(ChatOptions? options, string model)
        {
            var effectiveReasoningEffort = ReasoningConstants.NonReasoningModel;
            if (ChatOptionsExtensions.IsReasoningModel(model))
            {
                var reasoningEffortValue = default(object);
                var reasoningKeyFound = options?.AdditionalProperties?.TryGetValue(FrameworkConstants.ReasoningEffortKey, out reasoningEffortValue) ?? false;
                if (reasoningKeyFound
                    && reasoningEffortValue is string effort
                    && !string.IsNullOrEmpty(effort))
                {
                    effectiveReasoningEffort = effort;
                }
                else
                {
                    // if not provided, gpt models default to medium reasoning
                    effectiveReasoningEffort = ReasoningConstants.MediumReasoningEffort;
                }
            }

            return effectiveReasoningEffort;
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
            long cacheCreationInputTokens = 0L;

            await foreach (var update in _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            {
                // Capture model ID from updates
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    if (!string.IsNullOrWhiteSpace(update.ModelId))
                    {
                        modelId = update.ModelId;
                    }
                    else if (update.RawRepresentation is OpenAI.Chat.StreamingChatCompletionUpdate openAIUpdate
                             && !string.IsNullOrWhiteSpace(openAIUpdate.Model))
                    {
                        modelId = openAIUpdate.Model;
                    }

                }

                // Capture cache creation input tokens from Anthropic streaming updates
                if (update.RawRepresentation is Anthropic.Models.Beta.Messages.BetaRawMessageDeltaEvent anthropicDeltaEvent
                    && anthropicDeltaEvent.Usage?.CacheCreationInputTokens != null)
                {
                    cacheCreationInputTokens = Math.Max(cacheCreationInputTokens, anthropicDeltaEvent.Usage.CacheCreationInputTokens.Value);
                }

                // Extract usage information from UsageContent in the update contents
                foreach (var content in update.Contents)
                {
                    var usage = content as UsageContent;
                    if (usage != null)
                    {
                        if (usage.Details.InputTokenCount.HasValue)
                        {
                            inputTokens = Math.Max(inputTokens, usage.Details.InputTokenCount.Value);
                        }

                        if (usage.Details.OutputTokenCount.HasValue)
                        {
                            outputTokens += usage.Details.OutputTokenCount.Value;
                        }

                        if (usage.Details.CachedInputTokenCount.HasValue)
                        {
                            cachedTokens = Math.Max(cachedTokens, usage.Details.CachedInputTokenCount.Value);
                        }

                        if (usage.Details.ReasoningTokenCount.HasValue)
                        {
                            reasoningTokens += usage.Details.ReasoningTokenCount.Value;
                        }

                        // Also check AdditionalCounts for CacheCreationInputTokens (used by some SDK versions)
                        if (usage.Details.AdditionalCounts != null
                            && usage.Details.AdditionalCounts.TryGetValue("CacheCreationInputTokens", out var additionalCacheCreation))
                        {
                            cacheCreationInputTokens = Math.Max(cacheCreationInputTokens, additionalCacheCreation);
                        }
                    }
                }

                yield return update;
            }

            // Log accumulated token consumption at the end of the stream
            try
            {
                // Split modelId into Model and ModelVersion when in format "model-modelVersion" (modelVersion is a date like 2025-04-14 or 20250414)
                string model = modelId;
                string modelVersion = string.Empty;

                if (!string.IsNullOrEmpty(modelId))
                {
                    var m = ModelVersionRegex.Match(modelId);
                    if (m.Success && m.Groups.Count >= 3)
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

                var effectiveReasoningEffort = GetEffectiveReasoningEffort(options, model);

                // This is a workaround for Claude models which report double token usage in streaming mode as of Anthropic SDK v12.2.0
                // This bug has been fixed in
                // https://github.com/anthropics/anthropic-sdk-csharp/pull/99/changes#diff-dcc8ae7a379bf0e30728ddfac2154a44dd543e968d60acbc45fddcdea650e67c
                // but not released yet. Remove this workaround when upgrading to Anthropic SDK version.
                if (modelId.StartsWith("claude"))
                {
                    inputTokens /= 2;
                    cachedTokens /= 2;
                    cacheCreationInputTokens /= 2;
                }

                // Log token consumption (structured record with model and modelVersion)
                _logger.LogTokenConsumption(model, modelVersion, inputTokens, outputTokens, cachedTokens, cacheCreationInputTokens, reasoningTokens, effectiveReasoningEffort);
            }
            catch (Exception ex)
            {
                _logger.LogInternalDebug("Failed to log streaming token usage: {Exception}", ex);
            }
        }
    }
}
