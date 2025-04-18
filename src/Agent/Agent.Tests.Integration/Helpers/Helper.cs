// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Runtime;
using Agent.Tests.Common;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
            CancellationToken cancellationToken,
            ILogger? logger = null)
        {
            var (approved, msg) = await ApprovalTestHelper.DoApproval(durableTaskClient, timeProvider, instanceID, logger, cancellationToken);

            if (!approved)
            {
                Assert.Fail(msg);
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

