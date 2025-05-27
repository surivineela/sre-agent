// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public enum ActionStatus
{
    Pending,
    PendingApproval,
    Approved,
    Rejected,
    InProgress,
    Completed,
    Failed
}

public enum ActionMode
{
    // Write actions will be taken after user approval
    Manual,
    // Write actions will be taken without user approval
    Agent,
    // No write actions will be taken by agent
    ReadOnly
}

public enum ActionSeverity
{
    Critical,
    Warning
}

public record Action(
    Guid Id,
    string Title,
    string ToolName,
    DateTime TimeStamp, // created timestamp
    ActionStatus Status,
    ActionSeverity Severity
);


public record actionStatusMetrics(
    int PendingActionsCount,
    int InProgressActionsCount,
    int CompletedActionsCount,
    int FailedActionsCount
);

public record actionSeverityMetrics(
    int CriticalActionsCount,
    int WarningActionsCount
);
