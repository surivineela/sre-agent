// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

        public TokenLoggingChatClient(IChatClient inner, ILogger<TokenLoggingChatClient> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = await _inner.GetResponseAsync(chatMessages, options, cancellationToken);

                try
                {
                    var modelId = response?.ModelId?.ToString() ?? string.Empty;
                    var inputTokens = response?.Usage?.InputTokenCount ?? 0L;
                    var outputTokens = response?.Usage?.OutputTokenCount ?? 0L;

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
                    _logger.LogTokenConsumption(model, modelVersion, inputTokens, outputTokens);
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

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            // Streaming scenarios may report usage at the end; for now, delegate to inner client.
            return _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
        }
    }
}
