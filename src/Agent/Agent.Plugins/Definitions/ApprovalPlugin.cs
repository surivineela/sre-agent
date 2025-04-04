// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace OperationalAgentCore;

public class ApprovalPlugin
{
    [KernelFunction("start_plan_approval_process")]
    [Description("To start a new approval process for user to approve a specific plan for migration. Returns a Approval URL for the plan")]
    public ApprovalStatus StartApprovalProcess(
        [Description("Short Name for the agreed Plan")]
        string nameOfThePlan,
        [Description("The name of remediation operation that to be approved.")]
        string operationName,
        [Description("The concise description of what the operation is doing to be displayed on the approval page")]
        string operationDescription)
    {
        var guid = Guid.NewGuid();
        return GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(nameOfThePlan, operationName),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, operationDescription));
    }

    [KernelFunction("get_plan_approval_status")]
    [Description("To get the status of an approval, returns null if the approval process hasn't started.")]
    public ApprovalStatus? GetApprovalStatus(
        [Description("Short Name for the agreed Plan")]
        string nameOfThePlan,
        [Description("The name of remediation operation that to be approved.")]
        string operationName)
    {
        return GlobalStatic.ApprovalStatus.TryGetValue(new ApprovalDescriptor(nameOfThePlan, operationName), out var status)
            ? status
            : null;
    }
}

