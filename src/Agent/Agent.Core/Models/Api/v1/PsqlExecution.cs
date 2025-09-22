// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents a PostgreSQL command execution
/// </summary>
public record PsqlExecution(
    Guid Id,
    string Command,
    string Description,
    AzCliExecutionStatus Status,
    string? OriginalFunctionCall = null,
    string? Output = null,
    string? Error = null,
    DateTime CreatedTimestamp = default,
    DateTime? StartedTimestamp = null,
    DateTime? CompletedTimestamp = null,
    Author? ExecutedBy = null,
    Guid? AgentContextId = null
);
