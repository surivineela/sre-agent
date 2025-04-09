// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Runtime;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.Helpers
{
    internal static class Helper
    {
        public static async Task SendMessageAndWait(IChatClient chatClient, string message, ITestOutputHelper _output, int delayInSeconds = 5)
        {
            await SendMessage(chatClient, message, _output);

            await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
        }

        public static async Task<ChatResponse> SendMessage(IChatClient chatClient, string message, ITestOutputHelper _output)
        {
            _output.WriteLine($"Sending message: {message}");
            return await chatClient.GetResponseAsync(message);
        }

        public static async Task DoApproval(
            DurableTaskClient durableTaskClient,
            TimeProvider timeProvider,
            string instanceID,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), linkedCts.Token);

                await foreach (var orchestrationMetadata in durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
                {
                    Statuses = new[] { OrchestrationRuntimeStatus.Running },
                    InstanceIdPrefix = "approval"
                }))
                {
                    OrchestrationMetadata? approvalOrchestration = await durableTaskClient.GetInstanceAsync(orchestrationMetadata.InstanceId, true, linkedCts.Token);
                    if (approvalOrchestration.ReadInputAs<ApprovalInput>()?.ParentInstanceId == instanceID)
                    {
                        var approvalId = approvalOrchestration.InstanceId;
                        var approvalStatus = new ApprovalStatus(approvalId, timeProvider.GetUtcNow().DateTime, timeProvider.GetUtcNow().DateTime, "unit test", ProcessedTime: null, "description");
                        await durableTaskClient.RaiseEventAsync(approvalId, "ApprovalEvent", approvalStatus);
                        return;
                    }
                }
            }
        }

        public static async Task CleanupAllOrchestration<T>(
            DurableTaskClient durableTaskClient)
        {
            // todo - this might cause problems once we have tests running in parallel

            var query = new OrchestrationQuery
            {
                Statuses = [OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Pending]
            };

            var instances = durableTaskClient.GetAllInstancesAsync(query);

            await foreach (var instance in instances.Where(x => x.Name == nameof(T)))
            {
                await durableTaskClient.TerminateInstanceAsync(instance.InstanceId, new TerminateInstanceOptions { Output = "Test cleanup", Recursive = true });
                await durableTaskClient.WaitForInstanceCompletionAsync(instance.InstanceId);
            }
        }
    }
}

