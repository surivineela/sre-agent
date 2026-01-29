// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for thread checkpoints (snapshots).
/// Partition key: DocumentType
/// </summary>
public record AdcThreadSnapshotDocument(
    string Id,
    string ThreadId,
    string SnapshotId,
    string Name,
    string OriginalBaseSnapshotId,
    DateTime CreatedAt
) : ICosmosDocument
{
    public string DocumentType => "AdcThreadSnapshot";
    public string PartitionKey => DocumentType;

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public AdcSnapshotInfo ToInfo() => new()
    {
        SnapshotId = SnapshotId,
        ThreadId = ThreadId,
        Name = Name,
        OriginalBaseSnapshotId = OriginalBaseSnapshotId,
        CreatedAt = CreatedAt
    };
}
