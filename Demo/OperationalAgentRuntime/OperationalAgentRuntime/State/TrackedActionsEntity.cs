using System.Text.Json;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.State
{
    public static class TrackedActionsEntity
    {
        [Function("TrackedActionsMemory")]
        public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher, FunctionContext executionContext)
        {
            ILogger _logger = executionContext.GetLogger(nameof(TrackedActionsEntity));

            return dispatcher.DispatchAsync(operation =>
            {
                try
                {
                    if (operation.State.GetState<List<TrackedAction>>() is null)
                    {
                        operation.State.SetState(new List<TrackedAction>());
                    }
                }
                catch (JsonException e)
                {
                    operation.State.SetState(new List<TrackedAction>());
                    _logger.LogWarning(e, $"{nameof(TrackedActionsEntity)} state changed. Purging state.");
                }

                switch (operation.Name.ToLowerInvariant())
                {
                    case "add":
                        List<TrackedAction> state = operation.State.GetState<List<TrackedAction>>() ?? [];
                        var item = operation.GetInput<TrackedAction>();
                        state.Add(item);
                        operation.State.SetState(state);
                        return new(state);
                    case "reset":
                        operation.State.SetState(new List<TrackedAction>());
                        break;
                    case "get":
                        return new(operation.State.GetState<List<TrackedAction>>());
                    case "delete":
                        operation.State.SetState(null);
                        break;
                }

                return default;
            });
        }
    }
}
