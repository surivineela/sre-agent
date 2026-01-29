// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Data.DataModels;

/// <summary>
/// Singleton Cosmos DB document tracking the current ADC base snapshot.
/// Used for auto-provisioning new sandboxes and detecting base image changes.
/// Partition key: "adc-config", Id: "base-snapshot"
/// </summary>
public record AdcBaseSnapshotDocument(
    string Id,
    string SnapshotId,
    string DiskImageId,
    string BaseImage,
    string Name,
    AdcResources Resources,
    DateTime CreatedAt
) : ICosmosDocument
{
    public string DocumentType => "AdcBaseSnapshot";
    public string PartitionKey => DocumentType;

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public AdcBaseSnapshotInfo ToInfo() => new()
    {
        SnapshotId = SnapshotId,
        DiskImageId = DiskImageId,
        BaseImage = BaseImage,
        Name = Name,
        Resources = Resources,
        CreatedAt = CreatedAt
    };
}
