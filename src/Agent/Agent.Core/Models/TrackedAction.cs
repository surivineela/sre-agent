// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models;

public class TrackedAction

    {
        public string ActionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string AgentId { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public ActionType Type { get; set; }
        public ActionStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<DiagnosticEvent> DiagnosticEvents { get; set; } = new();
        public RemediationContext? RemediationContext { get; set; }
    }

    public class DiagnosticEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, double> Metrics { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    public class RemediationContext
    {
        public string IssueType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public List<string> AffectedComponents { get; set; } = new();
        public Dictionary<string, string> RemediationSteps { get; set; } = new();
        public Dictionary<string, string> RollbackPlan { get; set; } = new();
        public List<string> RelatedIncidents { get; set; } = new();
        public Dictionary<string, string> EnvironmentContext { get; set; } = new();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ActionType
    {
        AppStateTracking,
        Investigation,
        Monitoring,
        Remediation,
        Validation,
        RollBack,
        Notification
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ActionStatus
    {
        Initiated,
        InProgress,
        Completed,
        Failed,
        RolledBack,
        RequiresApproval
    }
