using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record ApprovalDocumentEncryptedProperties(
    string? OboToken);

public record ApprovalDocument(
    string Id,
    string ThreadId,
    string Title,
    string Description,
    ToolApprovalStatus Status,
    DateTime CreatedTimestamp,
    DateTime? DecisionTimestamp,
    Author? DecisionUser,
    string? OrchestrationId,
    string? AgentContextId,
    string? OboTokenScope,
    ApprovalDocumentEncryptedProperties? EncryptedProperties
    ) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public const string DocumentTypeName = "Approval";
    public string DocumentType => DocumentTypeName;
    public string PartitionKey => ThreadId;

    // Conversion to/from domain model
    public static ApprovalDocument FromDomainModel(Approval approval) =>
        new(
            Id: approval.Id.ToString(),
            ThreadId: approval.ThreadId.ToString(),
            Title: approval.Title,
            Description: approval.Description,
            Status: ToToolApprovalStatus(approval.Status),
            CreatedTimestamp: approval.CreatedTimestamp,
            DecisionTimestamp: approval.DecisionTimestamp,
            DecisionUser: approval.DecisionUser,
            OrchestrationId: approval.OrchestrationId,
            AgentContextId: approval.AgentContextId?.ToString(),
            OboTokenScope: string.IsNullOrEmpty(approval.OboTokenScope) ? Constants.DefaultOboTokenScope : approval.OboTokenScope,
            EncryptedProperties: new ApprovalDocumentEncryptedProperties(approval.OboToken)
        );

    public Approval ToDomainModel() =>
        new(
            Id: Guid.Parse(Id),
            ThreadId: ThreadId,
            Title: Title,
            Description: Description,
            Status: ToApprovalDecision(Status),
            CreatedTimestamp: CreatedTimestamp,
            DecisionTimestamp: DecisionTimestamp,
            OrchestrationId: OrchestrationId,
            AgentContextId: Guid.TryParse(AgentContextId, out var agentContextId) ? agentContextId : null,
            OboToken: EncryptedProperties?.OboToken,
            OboTokenScope: string.IsNullOrEmpty(OboTokenScope) ? Constants.DefaultOboTokenScope : OboTokenScope,
            DecisionUser: DecisionUser
        );

    public static ToolApprovalStatus ToToolApprovalStatus(ApprovalDecision decision)
    {
        return decision switch
        {
            ApprovalDecision.Pending => ToolApprovalStatus.Pending,
            ApprovalDecision.Approved => ToolApprovalStatus.Approved,
            ApprovalDecision.Rejected => ToolApprovalStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, null)
        };
    }

    public static ApprovalDecision ToApprovalDecision(ToolApprovalStatus status)
    {
        return status switch
        {
            ToolApprovalStatus.Pending => ApprovalDecision.Pending,
            ToolApprovalStatus.Approved => ApprovalDecision.Approved,
            ToolApprovalStatus.Rejected => ApprovalDecision.Rejected,
            ToolApprovalStatus.NotRequired => ApprovalDecision.Approved,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
