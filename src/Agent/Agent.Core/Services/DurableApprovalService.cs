using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public class DurableApprovalService : IApprovalService
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<DurableApprovalService> _logger;

        public DurableApprovalService(DurableTaskClient durableTaskClient, ILogger<DurableApprovalService> logger)
        {
            _durableTaskClient = durableTaskClient;
            _logger = logger;
        }


        public async Task SubmitApprovalDecision(string approvalId, ApprovalDecision status)
        {
            _logger.LogInformation($"Processing approval decision for {approvalId} with status {status}");

            if (status == ApprovalDecision.Approved)
            {
                //todo - reconcile this approval status type with the new one introduced in core/models/api
                var approvalStatus = new ApprovalStatus(
                    approvalId,
                    StartTime: DateTime.UtcNow,
                    ApprovedTime: DateTime.UtcNow,
                    DecisionMaker: "someone",
                    ProcessedTime: null
                    );

                await _durableTaskClient.RaiseEventAsync(approvalId, "ApprovalEvent", approvalStatus);
            }
            else if (status == ApprovalDecision.Rejected)
            {
                throw new NotImplementedException("How do we handle rejections?");
            }
            else
            {
                throw new ArgumentException($"Invalid approval status: {status} for approvalId: {approvalId}");
            }
        }
    }
}
