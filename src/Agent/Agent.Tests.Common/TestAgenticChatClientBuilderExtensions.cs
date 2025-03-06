using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common
{
    public static class TestAgenticChatClientBuilderExtensions
    {
        public static ChatClientBuilder UseAgenticLogging(this ChatClientBuilder builder)
        {
            return builder.Use(delegate (IChatClient innerClient, IServiceProvider services)
            {

                var lf = services.GetRequiredService<ILoggerFactory>();
                AgenticLoggingChatClient loggingChatClient = new AgenticLoggingChatClient(innerClient, lf.CreateLogger<AgenticLoggingChatClient>());
                return loggingChatClient;
            });
        }

        public class AgenticLoggingChatClient : IChatClient
        {
            private HashSet<string> _hashes = new HashSet<string>();
            private ConcurrentDictionary<string,ChatMessage> _debugTracking = new ConcurrentDictionary<string, ChatMessage>();
            private IChatClient _innerClient;
            private ILogger<AgenticLoggingChatClient> _logger;
            private JsonSerializerOptions _hashingOptions;

            public AgenticLoggingChatClient(IChatClient innerClient, ILogger<AgenticLoggingChatClient> logger)
            {
                _innerClient = innerClient;
                _logger = logger;
                _hashingOptions = new JsonSerializerOptions();
                _hashingOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
                _hashingOptions.MakeReadOnly();
            }

            public void Dispose()
            {
                _innerClient.Dispose();
            }

            public async Task<ChatResponse> GetResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                
                string hash = null;
                  
                foreach (var chatMessage in chatMessages)
                {
                    hash = CachingHelpers.GetCacheKey([chatMessage], _hashingOptions);
                    if (_hashes.Contains(hash))
                    {
                        continue;
                    }
                    _hashes.Add(hash);

                    _debugTracking[hash] = chatMessage;



                    //var last = chatMessages.Last();

                    foreach (var functionResultContent in chatMessage.Contents.OfType<FunctionResultContent>())
                    {
                        _logger.LogTrace($"Function call {functionResultContent.CallId} completed with result {functionResultContent.Result}");
                    }

                    if (!string.IsNullOrEmpty(chatMessage.Text))
                    {
                        _logger.LogTrace($"{chatMessage.Role}: {chatMessage.Text}");
                    }
                }

                var res = await _innerClient.GetResponseAsync(chatMessages, options, cancellationToken);

                foreach (var responseContent in res.Message.Contents)
                {
                    if (responseContent is FunctionCallContent functionCallContent)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Function call {functionCallContent.Name}({functionCallContent.CallId}) invoked with arguments:");

                        foreach (var arg in functionCallContent.Arguments)
                        {
                            sb.AppendLine($"  {arg.Key}: {arg.Value}");
                        }

                        _logger.LogTrace(sb.ToString());
                    }
                }

                if (!string.IsNullOrEmpty(res.Message.Text))
                {
                    _logger.LogTrace($"{res.Message.Role}: {res.Message.Text}");
                }

                hash = CachingHelpers.GetCacheKey([res.Message], _hashingOptions);
                _hashes.Add(hash);
                _debugTracking[hash] = res.Message;


                return res;
            }

            public object? GetService(Type serviceType, object? serviceKey = null)
            {
                return _innerClient.GetService(serviceType, serviceKey);
            }

            public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                return _innerClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
            }
        }
    }
}
