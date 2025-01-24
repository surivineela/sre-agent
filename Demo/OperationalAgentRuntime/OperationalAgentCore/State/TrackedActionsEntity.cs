using Microsoft.Azure.Functions.Worker;
using OperationalAgentRuntime.Cli.DemoExec.Models;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.State
{
    public static class TrackedActionsEntity
    {
        [Function("TrackedActionsMemory")]
        public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync(operation =>
            {
                if (operation.State.GetState(typeof(List<TrackedAction>)) is null)
                {
                    operation.State.SetState(new List<TrackedAction>());
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
