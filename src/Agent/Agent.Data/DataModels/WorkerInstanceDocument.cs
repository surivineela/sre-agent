// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record WorkerInstanceDocument(
    string Id,
    DateTimeOffset LastHeartbeat,
    int CurrentAgentCount,
    WorkerInstanceHealthState HealthState
) : ICosmosDocument
{
    public string DocumentType => "WorkerInstance";
    public string PartitionKey => Id;
    public static string ContainerName => AgentDataConfiguration.InstanceManagementContainerName;

    public WorkerInstance ToDomainModel() =>
        new()
        {
            Id = Id,
            LastHeartbeat = LastHeartbeat,
            CurrentAgentCount = CurrentAgentCount,
            HealthState = HealthState
        };

    public static WorkerInstanceDocument FromDomainModel(WorkerInstance instance) =>
        new(
            instance.Id,
            instance.LastHeartbeat,
            instance.CurrentAgentCount,
            instance.HealthState
        );
}
