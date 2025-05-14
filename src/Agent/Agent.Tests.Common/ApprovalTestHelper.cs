using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common;
public class ApprovalTestHelper
{
    public static async Task DoApproval(
        DurableTaskClient durableTaskClient,
        IThreadRepository threadRepository,
        Guid threadId,
        ILogger? logger,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(7));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        if (timeProvider == null)
        {
            timeProvider = TimeProvider.System;
        }

        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), linkedCts.Token);
            var approvals = await threadRepository.GetApprovalsAsync(threadId);

            if (approvals.Count == 0)
            {
                continue;
            }

            foreach (var approval in approvals)
            {
                if (approval.Status == ApprovalDecision.Pending)
                {
                    var updated = approval with
                    {
                        Status = ApprovalDecision.Approved,
                        DecisionTimestamp = timeProvider.GetUtcNow().DateTime,
                        DecisionUser = new Author(Role.User, "TestUser", "TestUserId")
                    };

                    await threadRepository.UpdateApprovalAsync(updated);

                    var approvalStatus = new ApprovalStatus(
                        updated.Id.ToString(),
                        StartTime: timeProvider.GetUtcNow().DateTime,
                        Status: ApprovalDecision.Approved,
                        ApprovedTime: timeProvider.GetUtcNow().DateTime,
                        DecisionMaker: updated.DecisionUser?.DisplayName,
                        ProcessedTime: null,
                        OboToken: updated.OboToken
                        );

                    await durableTaskClient.RaiseEventAsync(updated.OrchestrationId, "ApprovalEvent", approvalStatus);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Waits for the orchestration to finish, automatically approving any pending approvals.
    /// </summary>
    public static async Task<OrchestrationMetadata> WaitForCompletionWithAutomaticApprovals(
        DurableTaskClient durableTaskClient,
        string instanceId,
        IThreadRepository threadRepository,
        Guid threadId,
        ILogger? logger,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        Func<Task>? customAction = null)
    {
        OrchestrationMetadata? orchestrationMetadata = null;

        if (timeProvider == null)
        {
            timeProvider = TimeProvider.System;
        }

        while (orchestrationMetadata == null || orchestrationMetadata.IsRunning)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            var approvals = await threadRepository.GetApprovalsAsync(threadId);

            foreach (var approval in approvals)
            {
                if (approval.Status == ApprovalDecision.Pending)
                {
                    var updated = approval with
                    {
                        Status = ApprovalDecision.Approved,
                        DecisionTimestamp = timeProvider.GetUtcNow().DateTime,
                        DecisionUser = new Author(Role.User, "TestUser", "TestUserId")
                    };

                    await threadRepository.UpdateApprovalAsync(updated);

                    var approvalStatus = new ApprovalStatus(
                        updated.Id.ToString(),
                        StartTime: timeProvider.GetUtcNow().DateTime,
                        Status: ApprovalDecision.Approved,
                        ApprovedTime: timeProvider.GetUtcNow().DateTime,
                        DecisionMaker: updated.DecisionUser?.DisplayName,
                        ProcessedTime: null,
                        OboToken: updated.OboToken
                        );

                    await durableTaskClient.RaiseEventAsync(updated.OrchestrationId, "ApprovalEvent", approvalStatus);
                    break;
                }
            }

            orchestrationMetadata = await durableTaskClient.GetInstanceAsync(instanceId, true, cancellationToken);

            if (customAction != null)
                await customAction();
        }

        if (orchestrationMetadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
        {
            throw new InvalidOperationException($"Orchestration failed: {orchestrationMetadata.FailureDetails}");
        }

        return orchestrationMetadata;
    }
}
