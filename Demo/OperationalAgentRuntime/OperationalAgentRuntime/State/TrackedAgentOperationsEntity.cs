using Microsoft.Azure.Functions.Worker;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.State
{
    // append-only store for larger agent operations that
    // we want to explicitly track in the demo with ID and context
    // - likely superceded in future or merged with other store
    public static class TrackedAgentOperationsEntity
    {
        [Function("TrackedAgentOperationsMemory")]
        public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync(operation =>
            {
                var state = operation.State.GetState<Dictionary<Guid, TrackedAgentOperation>>() ?? [];
                switch (operation.Name.ToLowerInvariant())
                {
                    case "add":
                        var item = operation.GetInput<TrackedAgentOperation>();
                        if (item != null && !state.ContainsKey(item.Id))
                        {
                            state[item.Id] = item;
                            operation.State.SetState(state);
                        }
                        return new(state);
                    case "addannotation":
                        var (id, annotation) = operation.GetInput<Tuple<Guid, string>>();
                        if (annotation != null && state.TryGetValue(id, out TrackedAgentOperation? value))
                        {
                            value.Annotations = [..value.Annotations, annotation];
                            operation.State.SetState(state);
                        }
                        return new(state);
                    case "reset":
                        operation.State.SetState(new Dictionary<Guid, TrackedAgentOperation>());
                        break;
                    case "get":
                        return new(operation.State.GetState<Dictionary<Guid, TrackedAgentOperation>>());
                }

                return default;
            });
        }
    }
}
