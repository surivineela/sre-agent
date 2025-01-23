using System.Text.Json;

using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Models;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.DurableTask.Client.Entities;
using System.Data;

namespace OperationalAgentRuntime.Helpers
{
    public static class TrackedActionHelper
    {
        private static readonly EntityInstanceId InstanceId;

        static TrackedActionHelper()
        {
            InstanceId = new EntityInstanceId("TrackedActionsMemory", "TrackedActionsMemoryV2");
        }

        public static async Task TrackAsTool(DurableTaskClient client, object content, [CallerMemberName] string caller = "")
        {
            var obj = new Dictionary<string, object>
            {
                { "Caller", caller },
                { "Content", content }
            };

            string serializedData = JsonSerializer.Serialize(obj);
            await client.Entities.SignalEntityAsync(InstanceId, "Add", new TrackedAction() { Role = ChatRole.Tool, Content = serializedData });
        }

        public static async Task TrackAsUser(DurableTaskClient client, string content)
        {
            await client.Entities.SignalEntityAsync(InstanceId, "Add", new TrackedAction() { Role = ChatRole.User, Content = content });
        }

        public static async Task TrackAsAssistant(DurableTaskClient client, string content)
        {
            await client.Entities.SignalEntityAsync(InstanceId, "Add", new TrackedAction() { Role = ChatRole.Assistant, Content = content });
        }
    }
}
