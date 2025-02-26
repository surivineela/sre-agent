using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            private bool _first = true;
            private IChatClient _innerClient;
            private ILogger<AgenticLoggingChatClient> _logger;
            public AgenticLoggingChatClient(IChatClient innerClient, ILogger<AgenticLoggingChatClient> logger)
            {
                _innerClient = innerClient;
                _logger = logger;
            }

            public void Dispose()
            {
                _innerClient.Dispose();
            }

            public async Task<ChatResponse> GetResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                // TODO - this doesnt work, logs are still duplicated, might be due to IChatClient lifetime
                // maybe just store a hash for each message and log any message that we haven't stored.
                
                if (_first)
                {
                    _first = false;
                    foreach (var chatMessage in chatMessages.SkipLast(1))
                    {
                        _logger.LogTrace($"{chatMessage.Role}: {chatMessage.Text}");
                    }
                }

                var last = chatMessages.Last();

                foreach (var functionResultContent in last.Contents.OfType<FunctionResultContent>())
                {
                    _logger.LogTrace($"Function call {functionResultContent.CallId} completed with result {functionResultContent.Result}");
                }

                if (!string.IsNullOrEmpty(last.Text))
                {
                    _logger.LogTrace($"{last.Role}: {last.Text}");
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
