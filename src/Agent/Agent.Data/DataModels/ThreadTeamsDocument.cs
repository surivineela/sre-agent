// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Bot.Schema;

namespace Agent.Data.DataModels;

public record ThreadTeamsMappingDocument(
    string Id,
    string ThreadId,
    string ConversationId,
    string ChannelId,
    string ServiceUrl,
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp,
    ConversationReference? Reference = null
) : ICosmosDocument
{
    public string DocumentType => "ThreadTeamsMapping";
    public string PartitionKey => Id; // Use thread ID as partition key

    public static ThreadTeamsMappingDocument FromDomainModel(ThreadTeamsMapping mapping) =>
        new ThreadTeamsMappingDocument(
            $"teams_{mapping.ThreadId}",
            mapping.ThreadId,
            mapping.ConversationId,
            mapping.ChannelId,
            mapping.ServiceUrl,
            mapping.CreatedTimestamp,
            mapping.ModifiedTimestamp,
            mapping.Reference
        );

    public ThreadTeamsMapping ToDomainModel() =>
    new ThreadTeamsMapping(
        Id,
        ThreadId,
        ConversationId,
        ChannelId,
        ServiceUrl,
        CreatedTimestamp,
        ModifiedTimestamp,
        Reference
    );
}
