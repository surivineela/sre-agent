using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Agent.Runtime.SubAgents.Core;
using Grpc.Core;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common;
public static class DurableExtensions
{
    public static IHostApplicationBuilder ConfigureDurable(this IHostApplicationBuilder builder)
    {
        string durableConnectionString = builder.ResolveDtsConnectionString();

        builder.Services.AddDurableTaskWorker(durableBuilder =>
        {
            durableBuilder.AddTasks(r =>
            {
                DurableHelper.AddAllGeneratedTasks(r);
            });

            durableBuilder.UseDurableTaskScheduler(durableConnectionString);
        });

        builder.Services.AddDurableTaskClient(durableBuilder =>
        {
            durableBuilder.UseDurableTaskScheduler(durableConnectionString);
        });

        return builder;
    }

    public static ChatMessage[] ReadChatHistory(this OrchestrationMetadata orchestration)
    {
        var fullHistoryRaw = orchestration.ReadCustomStatusAs<string>();
        if (fullHistoryRaw == null)
        {
            throw new Exception("Unable to read custom status");
        }
        var fullHistory = System.Text.Json.JsonSerializer.Deserialize<ChatMessage[]>(fullHistoryRaw, new System.Text.Json.JsonSerializerOptions
        {

            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        return fullHistory ?? [];
    }

    public static async Task<OrchestrationMetadata> WaitForInstanceCompletionWithRetryAsync(
        this DurableTaskClient durableTaskClient,
        string instanceId,
        CancellationToken cancellationToken
        )
    {
        if (durableTaskClient == null) throw new ArgumentNullException(nameof(durableTaskClient));
        if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));

        OrchestrationMetadata orchestrationMetadata;

        while (true)
        {
            try
            {
                orchestrationMetadata = await durableTaskClient.WaitForInstanceCompletionAsync(instanceId, true, cancellationToken);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    throw new InvalidOperationException($"Orchestration failed: {orchestrationMetadata.FailureDetails}");
                }

                return orchestrationMetadata;
            }
            catch (RpcException)
            {
                // Handle transient gRPC errors (e.g., 504 Gateway Timeout)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
