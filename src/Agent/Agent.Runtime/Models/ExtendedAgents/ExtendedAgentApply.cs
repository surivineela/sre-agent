// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Runtime.Models.ExtendedAgents;

public class ExtendedAgentApply
{
    [Required]
    public ExtendedAgentApplyStatus Status { get; set; } = ExtendedAgentApplyStatus.Accepted;

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required]
    public string OperationId { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ExtendedAgentApplyDetails? Details { get; set; }
}

public class ExtendedAgentApplyDetails
{
    public string? AgentName { get; set; }
    public int ToolsCount { get; set; }
    public int ConnectorsCount { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
}

public enum ExtendedAgentApplyStatus
{
    Accepted,
    Rejected,
    Pending
}
