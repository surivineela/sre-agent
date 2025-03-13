using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Data.DataModels;

// Extended Action model for Cosmos DB
public record ActionDocument(
    string Id,
    string ThreadId,
    string Title,
    DateTime TimeStamp,
    ActionStatus Status
) : ICosmosDocument
{
    public string DocumentType => "Action";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static ActionDocument FromDomainModel(Action action, string threadId) =>
        new ActionDocument(
            action.Id.ToString(),
            threadId,
            action.Title,
            action.TimeStamp,
            action.Status
    );

    public Action ToDomainModel() =>
        new Action(
            Guid.Parse(Id),
            Title,
            TimeStamp,
            Status
        );
}
