// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record LeaderLeaseDocument : ICosmosDocument
{
    public required string LeaseHolder { get; set; }
    public required DateTimeOffset LeaseExpiration { get; set; }

    public string DocumentType => Constants.LeaderLeaseName;
    public string PartitionKey => Constants.LeaderLeaseName; // there will be only one leader lease document
    public string Id => Constants.LeaderLeaseName;
    public static string ContainerName => AgentDataConfiguration.InstanceManagementContainerName;

    public LeaderLease ToDomainModel() =>
        new(
            LeaseHolder,
            LeaseExpiration
        );
}
