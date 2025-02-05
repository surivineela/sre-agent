// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public sealed record ApprovalStatus(
    string OperationId,
    DateTime StartTime,
    DateTime? ApprovedTime,
    string? DecisionMaker,
    DateTime? ProcessedTime,
    string description = "")
{
    public bool IsApproved => ApprovedTime.HasValue;

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
