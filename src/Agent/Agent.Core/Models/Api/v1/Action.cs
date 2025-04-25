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

public record Action(
    Guid Id,
    string Title,
    string ToolName,
    DateTime TimeStamp,
    ActionStatus Status
);

