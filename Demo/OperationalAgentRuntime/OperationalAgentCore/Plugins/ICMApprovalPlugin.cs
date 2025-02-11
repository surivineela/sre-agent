using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore;

public class ICMApprovalPlugin
{
    [KernelFunction("start_icm_incident_approval_process")]
    [Description("To start a new approval process for user to approve operations related to an ICM incident.")]
    public ApprovalStatus StartApprovalProcess(
        [Description("The ICM Incident Id.")]
        string incidentId,
        [Description("The name of the operation that is to be approved.")]
        string operationName,
        [Description("The concise description with incident id of what the operation is doing to be displayed on the approval page")]
        string operationDescription)
    {
        var guid = Guid.NewGuid();
        return GlobalStatic.ApprovalStatus.GetOrAdd(
            new ApprovalDescriptor(incidentId, operationName),
            new ApprovalStatus(guid.ToString(), DateTime.Now, null, null, null, operationDescription));
    }

    [KernelFunction("get_icm_incident_approval_status")]
    [Description("To get the status of an approval, returns null if the approval process hasn't started.")]
    public ApprovalStatus? GetApprovalStatus(
        [Description("The ICM Incident Id.")]
        string incidentId,
        [Description("The name of remediation operation that to be approved.")]
        string operationName)
    {
        return GlobalStatic.ApprovalStatus.TryGetValue(new ApprovalDescriptor(incidentId, operationName), out var status)
            ? status
            : null;
    }
}
