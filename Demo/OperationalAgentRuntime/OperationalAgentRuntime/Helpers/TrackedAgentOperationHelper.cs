using Azure;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Helpers
{
    // Supports adding new operations, adding annotations to existing operations, and getting all operations
    // No deletions, append-only
    public static class TrackedAgentOperationActionHelper
    {
        public static readonly EntityInstanceId InstanceId;

        static TrackedAgentOperationActionHelper()
        {
            InstanceId = new EntityInstanceId("TrackedAgentOperationsMemory", "SharedTrackedAgentOperationsMemoryEntity");
        }

        public static async Task ResetAsync(TaskOrchestrationContext context)
        {
            await context.Entities.CallEntityAsync(InstanceId, "reset");
        }

        public static async Task AddOperation(TaskOrchestrationContext context, TrackedAgentOperation operation)
        {
            await context.Entities.CallEntityAsync(InstanceId, "add", operation);
        }

        public static async Task AppendAnnotation(TaskOrchestrationContext context, TrackedAgentOperation operation, string annotation)
        {
            await context.Entities.CallEntityAsync(InstanceId, "addAnnotation", Tuple.Create(operation.Id, annotation));
        }
        public static async Task<Dictionary<Guid, TrackedAgentOperation>> GetAllOperations(TaskOrchestrationContext context)
        {
            return await context.Entities.CallEntityAsync<Dictionary<Guid, TrackedAgentOperation>>(InstanceId, "get");
        }
    }
}
