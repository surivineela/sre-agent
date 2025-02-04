using System.Text.Json;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.State
{
    public static class ChatHistoryEntity
    {
        [Function("ChatHistoryEntity")]
        public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher, FunctionContext executionContext)
        {
            ILogger _logger = executionContext.GetLogger(nameof(ChatHistoryEntity));

            return dispatcher.DispatchAsync(operation =>
            {

                switch (operation.Name.ToLowerInvariant())
                {
                    case "set":                        
                        var item = operation.GetInput<string>();                        
                        operation.State.SetState(item);
                        break;                    
                    case "get":
                        return new(operation.State.GetState<string>());
                    case "appenduser":
                        var message = operation.GetInput<string>();
                        var state = operation.State.GetState<string>();
                        if (!string.IsNullOrEmpty(state))
                        {
                            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(state);
                            messages.Add(new ChatMessage(ChatRole.User, message));
                            operation.State.SetState(JsonSerializer.Serialize(messages));
                        }
                        break;
                    case "delete":
                        operation.State.SetState(null);
                        break;
                }

                return default;
            });
        }
    }
}
