// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for per-thread ADC sandbox state.
/// Each thread owns exactly one sandbox at a time.
/// Partition key: DocumentType
/// </summary>
public record AdcThreadSandboxDocument(
    string Id,
    string ThreadId,
    string SandboxId,
    string Endpoint,
    string? SourceSnapshotId,
    AdcResources Resources,
    DateTime CreatedAt,
    DateTime LastAccessedAt
) : ICosmosDocument
{
    public string DocumentType => "AdcThreadSandbox";
    public string PartitionKey => DocumentType;

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public AdcSandboxInfo ToInfo() => new()
    {
        SandboxId = SandboxId,
        ThreadId = ThreadId,
        Endpoint = Endpoint,
        SnapshotId = SourceSnapshotId,
        Resources = Resources,
        CreatedAt = CreatedAt,
    };
}
