using Agent.Core.Models;
using Agent.Runtime;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                var orchestrationMetadata = await durableTaskClient.GetInstanceAsync(instanceID, getInputsAndOutputs: true);

                if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    Assert.Fail(orchestrationMetadata.FailureDetails.ToString());
                }

                if (orchestrationMetadata.SerializedCustomStatus == null)
                {
                    continue;
                }

                var orchestrationStatus = orchestrationMetadata.ReadCustomStatusAs<string>();

                if (orchestrationStatus.StartsWith("Pending approval:"))
                {
                    var approvalId = orchestrationStatus.Split(":")[1];
                    var approvalStatus = new ApprovalStatus(approvalId, timeProvider.GetUtcNow().DateTime, timeProvider.GetUtcNow().DateTime, "unit test", ProcessedTime: null, "description");
                    await durableTaskClient.RaiseEventAsync(approvalId, "ApprovalEvent", approvalStatus);
                    break;
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
