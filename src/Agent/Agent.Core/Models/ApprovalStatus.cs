// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Models;

public sealed record ApprovalStatus(
    string OperationId,
    DateTime StartTime,
    ApprovalDecision Status,
    DateTime? ApprovedTime,
    string? DecisionMaker,
    DateTime? ProcessedTime,
    string description = "",
    string? OboToken = null)
{
    public bool IsApproved => Status == ApprovalDecision.Approved;

    public string ApprovalLinkUri
    {
        get
        {
            var approvalUrl = Environment.GetEnvironmentVariable("Azure:ApprovalUrl");
            if (!string.IsNullOrEmpty(approvalUrl))
            {
                var parsed = new Uri(approvalUrl);
                return $"{parsed.Scheme}://{parsed.Host}/?action_name={OperationId}&description={Uri.EscapeDataString(description)}";
            }

            return $"https://approval-app-affhfqdfcfc8gkgq.westus-01.azurewebsites.net/?action_name={OperationId}&description={Uri.EscapeDataString(description)}";
        }
    }
}

public enum ToolApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    NotRequired,
}
