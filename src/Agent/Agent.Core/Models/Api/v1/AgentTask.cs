// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

public enum AgentTaskType
{
    IncidentInvestigation
}

public sealed record AgentTask
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public IEnumerable<AgentTaskStep>? Steps { get; set; }
    public AgentTaskProperties? Properties { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required AgentTaskType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required AgentTaskStatus Status { get; set; }

    public required Guid ThreadId { get; set; }
    public DateTime? LastModified { get; set; }

    public Guid? DeepInvestigationApprovalId { get; set; }

    // Input data is not returned to the client, it is only used by the task handler
    [JsonIgnore]
    public AgentTaskInputData? InputData { get; set; }

    public AgentTaskShort ToShortForm() => new()
    {
        Id = Id,
        Title = Title,
        Type = Type,
        Status = Status
    };
}

public sealed record AgentTaskShort
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required AgentTaskType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required AgentTaskStatus Status { get; set; }
}

public enum AgentTaskStatus
{
    InProgress,
    Complete,
    Failed,
    Cancelled,
    PendingUserApproval
}

public sealed record AgentTaskStep
{
    public required string Title { get; set; }
    public required string Summary { get; set; }
}

#region AgentTask polymorphic types

[JsonPolymorphic]
[JsonDerivedType(typeof(IncidentInvestigationTaskProperties), nameof(IncidentInvestigationTaskProperties))]
public abstract record AgentTaskProperties
{
}

[JsonPolymorphic]
[JsonDerivedType(typeof(IncidentInvestigationTaskInputData), nameof(IncidentInvestigationTaskInputData))]
public abstract record AgentTaskInputData
{
}

#endregion
