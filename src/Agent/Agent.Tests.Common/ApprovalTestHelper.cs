using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Runtime;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common;
public class ApprovalTestHelper
{
    public static async Task<Tuple<bool,string>> DoApproval(
        DurableTaskClient durableTaskClient,
        TimeProvider timeProvider,
        string instanceID,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
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
                    return new (true, "");
                }
            }

            var parent = await durableTaskClient.GetInstanceAsync(instanceID, true, linkedCts.Token);
            if (parent.IsCompleted)
            {
                var errorMessage = $"Orchestration {instanceID} completed unexpectedly with status {parent.RuntimeStatus}. Details: {parent.FailureDetails} ";
                logger?.LogError(errorMessage);
                return new (false, errorMessage);
            }

        }
    }
}
