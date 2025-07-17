
using System;
using System.Collections.Generic;

namespace FirstPartyAgent.Helper.Models;

public abstract class ApprovalAuditEvent
{
    public DateTime AuditTime { get; set; }
    public required string CorrelationId { get; set; }
    public required string OperationId { get; set; }
    public required string ApprovalDocumentId { get; set; }
}

public class ApprovalCreationRequestAuditEvent: ApprovalAuditEvent
{
    public List<string> ReleaseApproversAllowed { get; set; } = new List<string>();
    public required string Title { get; set; }
    public required string RequestDescription { get; set; }
    public required string Submitter { get; set; }
    public required string ServiceTreeGuid { get; set; }
}

public class ApprovalActionAuditEvent : ApprovalAuditEvent
{
    public required string Subject { get; set; }
    public required string Principal { get; set; }
    public required string Action { get; set; }
    public required string Comments { get; set; }
    public required DateTime EventTime { get; set; }
    public required string Topic { get; set; }
    public required string EventType { get; set; }
}
