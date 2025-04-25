// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Data.DataModels;

// Extended Action model for Cosmos DB
public record ActionDocument(
    string Id,
    string ThreadId,
    string Title,
    string ToolName,
    DateTime TimeStamp, // created timestamp
    ActionStatus Status,
    ActionSeverity Severity
) : ICosmosDocument
{
    public string DocumentType => "Action";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Conversion to/from domain model
    public static ActionDocument FromDomainModel(Action action, string threadId) =>
        new ActionDocument(
            action.Id.ToString(),
            threadId,
            action.Title,
            action.ToolName,
            action.TimeStamp,
            action.Status,
            action.Severity
    );

    public Action ToDomainModel() =>
        new Action(
            Guid.Parse(Id),
            Title,
            ToolName,
            TimeStamp,
            Status,
            Severity
        );
}
