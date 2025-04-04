// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins.Mocks;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime
{

    public record ApprovalInput(string ParentInstanceId, string OperationName, string ThreadId, string ApprovalId);
    
    [DurableTask]
    public class ApprovalOrchestration : TaskOrchestrator<ApprovalInput, ApprovalStatus>
    {
        public async override Task<ApprovalStatus> RunAsync(TaskOrchestrationContext context, ApprovalInput input)
        {
            ILogger logger = context.CreateReplaySafeLogger<ApprovalOrchestration>();

            var startTime = context.CurrentUtcDateTime;

            logger.LogInformation($"Waiting for approval of operation {input.OperationName} with ID {context.InstanceId} started at {startTime:o}");
            // todo - send user approval link

            using (var cts = new CancellationTokenSource())
            {
                Task approvalTimeoutTask = context.CreateTimer(TimeSpan.FromDays(1), cts.Token);
                Task<ApprovalStatus> approvalTask = context.WaitForExternalEvent<ApprovalStatus>("ApprovalEvent", cts.Token);

                if(approvalTask == await Task.WhenAny(approvalTask, approvalTimeoutTask))
                {
                    var approvalEvent = await approvalTask;
                    cts.Cancel();
                    
                    await context.CallActivityAsync(new TaskName(nameof(HandleApprovalActivity)), Tuple.Create(input, approvalEvent));
                    return approvalEvent;
                }
                else
                {
                    var timeoutAt = context.CurrentUtcDateTime;
                    var approvalEvent = new ApprovalStatus(context.InstanceId, startTime, ApprovedTime: null, DecisionMaker: null, ProcessedTime: timeoutAt);

                    await context.CallActivityAsync(new TaskName(nameof(HandleApprovalTimeoutActivity)), Tuple.Create(input, approvalEvent));
                    return approvalEvent;
                }
            }
        }
    }

    [DurableTask]
    public class HandleApprovalActivity : TaskActivity<Tuple<ApprovalInput,ApprovalStatus>, string>
    {
        private readonly ILogger<HandleApprovalActivity> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IApprovalPlugin _approvalPlugin;

        public HandleApprovalActivity(ILogger<HandleApprovalActivity> logger, DurableTaskClient durableTaskClient, IApprovalPlugin approvalPlugin)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _approvalPlugin = approvalPlugin;
        }

        public override async Task<string> RunAsync(TaskActivityContext context, Tuple<ApprovalInput, ApprovalStatus> input)
        {
            ChatMessage outputMessage;

            var (approvalInput, approvalEvent) = input;

            if (approvalEvent.IsApproved)
            {
                var approvalString = $"Approval by **{approvalEvent.DecisionMaker}** received at {approvalEvent.ApprovedTime}";
                _logger.LogInformation(approvalString);

                // HACK HACK HACK
                if (_approvalPlugin is MockApprovalPlugin)
                {
                    ((MockApprovalPlugin)_approvalPlugin).ApprovedOperations.Add(approvalInput.OperationName);
                }

                outputMessage = new ChatMessage(ChatRole.System, approvalString);
            }
            else
            {
                var rejectionString = $"Operation was not approved. Rejected by **{approvalEvent.DecisionMaker}** at {approvalEvent.ApprovedTime}";
                _logger.LogInformation(rejectionString);
                outputMessage = new ChatMessage(ChatRole.System, rejectionString);
            }

            await _durableTaskClient.RaiseEventAsync(approvalInput.ParentInstanceId, "NewChatMessage", outputMessage);

            return "done";
        }
    }

    [DurableTask]
    public class HandleApprovalTimeoutActivity : TaskActivity<Tuple<ApprovalInput, ApprovalStatus>, string>
    {
        private readonly ILogger<HandleApprovalTimeoutActivity> _logger;
        private readonly DurableTaskClient _durableTaskClient;

        public HandleApprovalTimeoutActivity(ILogger<HandleApprovalTimeoutActivity> logger, DurableTaskClient durableTaskClient)
        {
            _logger = logger;
            _durableTaskClient = durableTaskClient;
        }
        public override async Task<string> RunAsync(TaskActivityContext context, Tuple<ApprovalInput, ApprovalStatus> input)
        {
            var (approvalInput, approvalEvent) = input;
            
            string timeoutMessage = $"Approval was not received within the timeout period. Operation timed out at {approvalEvent.ProcessedTime}";
            _logger.LogInformation(timeoutMessage);
            var outputMessage = new ChatMessage(ChatRole.System, timeoutMessage);

            await _durableTaskClient.RaiseEventAsync(approvalInput.ParentInstanceId, "NewChatMessage", outputMessage);
            return "done";
        }
    }
}

