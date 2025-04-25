// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public enum ActionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
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
