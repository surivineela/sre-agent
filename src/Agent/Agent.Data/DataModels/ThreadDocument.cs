using Agent.Core.Models.Api.v1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.DataModels;

// Extended Thread model for Cosmos DB
public record ThreadDocument(
    string Id,
    string Title,
    string MessageId, // Reference to the start message
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp
) : ICosmosDocument
{
    public string DocumentType => "Thread";
    public string PartitionKey => Id; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static ThreadDocument FromDomainModel(Thread thread) =>
        new ThreadDocument(
            thread.Id.ToString(),
            thread.Title,
            thread.StartMessage.Id.ToString(),
            thread.CreatedTimestamp,
            thread.ModifiedTimestamp
        );

    public Thread ToDomainModel(Message startMessage) =>
        new Thread(
            Guid.Parse(Id),
            Title,
            startMessage,
            CreatedTimestamp,
            ModifiedTimestamp
        );
}
