// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Serialization;
using NJ = Newtonsoft.Json;
using NJC = Newtonsoft.Json.Converters;
using SJ = System.Text.Json.Serialization;

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

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required AgentTaskType Type { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required AgentTaskStatus Status { get; set; }

    public required Guid ThreadId { get; set; }
    public DateTime? LastModified { get; set; }

    public Guid? DeepInvestigationApprovalId { get; set; }

    // Input data is not returned to the client, it is only used by the task handler
    [NJ.JsonIgnore]
    [SJ.JsonIgnore]
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

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required AgentTaskType Type { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
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

[NJ.JsonConverter(typeof(PolymorphicJsonConverter<AgentTaskProperties>))]
[SJ.JsonPolymorphic(TypeDiscriminatorPropertyName = Serialization.Constants.TypePropertyName)]
[SJ.JsonDerivedType(typeof(IncidentInvestigationTaskProperties))]
public abstract record AgentTaskProperties : PolymorphicBase, IPolymorphic
{
    static readonly Dictionary<string, Type> TypeToPropertiesClass = new()
    {
        { nameof(IncidentInvestigationTaskProperties), typeof(IncidentInvestigationTaskProperties) }
    };

    public static Type GetSubType(string type) => TypeToPropertiesClass[type];
}

[NJ.JsonConverter(typeof(PolymorphicJsonConverter<AgentTaskInputData>))]
[SJ.JsonPolymorphic(TypeDiscriminatorPropertyName = Serialization.Constants.TypePropertyName)]
[SJ.JsonDerivedType(typeof(IncidentInvestigationTaskInputData))]
public abstract record AgentTaskInputData : PolymorphicBase, IPolymorphic
{
    static readonly Dictionary<string, Type> TypeToInputDataClass = new()
    {
        { nameof(IncidentInvestigationTaskInputData), typeof(IncidentInvestigationTaskInputData) }
    };

    public static Type GetSubType(string type) => TypeToInputDataClass[type];
}

#endregion
