using Microsoft.Azure.Functions.Worker;
using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.State
{
    public static class ResourcesEntity
    {
        [Function("ResourceMemory")]
        public static Task DispatchAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
        {
            return dispatcher.DispatchAsync(operation =>
            {
                if (operation.State.GetState<List<AzureSubscription>>() is null)
                {
                    operation.State.SetState(new List<AzureSubscription>());
                }

                switch (operation.Name.ToLowerInvariant())
                {
                    case "add":
                        List<AzureSubscription> state = operation.State.GetState<List<AzureSubscription>>() ?? new List<AzureSubscription>();
                        var item = operation.GetInput<AzureSubscription>();
                        if (item != null)// && !state.Any(p=>p.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            var existing = state.FirstOrDefault(p => p.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
                            if (existing is not null) { state.Remove(existing); }
                            state.Add(item);
                            operation.State.SetState(state);
                        }
                        return new(state);
                    case "reset":
                        operation.State.SetState(new List<AzureSubscription>());
                        break;
                    case "get":
                        return new(operation.State.GetState< List<AzureSubscription>>());
                    case "delete":
                        operation.State.SetState(null);
                        break;
                }

                return default;
            });
        }
    }
}
