using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore;

public class ApprovalPlugin
{
    [KernelFunction("start_approval_process")]
    [Description("To start a new approval process for user to approve a specific remediation operation for a given resource.")]
    public ApprovalStatus StartApprovalProcess(
        [Description("The resource ID of the App Service.")]
        string resourceId,
        [Description("The name of remediation operation that to be approved.")]
        string operationName,
        [Description("The concise description of what the operation is doing to be displayed on the approval page")]
        string operationDescription)
    {
        var guid = Guid.NewGuid();
        return GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(resourceId, operationName),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, operationDescription));
    }

    [KernelFunction("get_approval_status")]
    [Description("To get the status of an approval, returns null if the approval process hasn't started.")]
    public ApprovalStatus? GetApprovalStatus(
        [Description("The resource ID of the App Service.")]
        string resourceId,
        [Description("The name of remediation operation that to be approved.")]
        string operationName)
    {
        return GlobalStatic.ApprovalStatus.TryGetValue(new ApprovalDescriptor(resourceId, operationName), out var status)
            ? status
            : null;
    }
}
