
using System;
using System.Collections.Generic;

namespace FirstPartyAgent.Helper.Models;

public abstract class ApprovalAuditEvent
{
    public DateTime AuditTime { get; set; }
    public string CorrelationId { get; set; }
    public string OperationId { get; set; }
    public string ApprovalDocumentId { get; set; }
}

public class ApprovalCreationRequestAuditEvent: ApprovalAuditEvent
{
    public List<string> ReleaseApproversAllowed { get; set; } = new List<string>();
    public string Title { get; set; }
    public string RequestDescription { get; set; }
    public string Submitter { get; set; }
    public string ServiceTreeGuid { get; set; }
}

public class ApprovalActionAuditEvent : ApprovalAuditEvent
{
    public string Id { get; set; }
    public string Subject { get; set; }
    public string Principal { get; set; }
    public string Action { get; set; }
    public string Comments { get; set; }
    public DateTime EventTime { get; set; }
    public string Topic { get; set; }
    public string EventType { get; set; }
}
