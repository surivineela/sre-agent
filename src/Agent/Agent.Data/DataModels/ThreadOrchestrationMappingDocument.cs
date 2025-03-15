using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record ThreadOrchestrationMappingDocument(
    string Id,
    string ThreadId,
    string OrchestrationInstanceId,
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp
) : ICosmosDocument
{
    public string DocumentType => "ThreadOrchestrationMapping";
    public string PartitionKey => ThreadId; // Use thread ID as partition key

    public static ThreadOrchestrationMappingDocument FromDomainModel(ThreadOrchestrationMapping mapping) =>
        new ThreadOrchestrationMappingDocument(
            // This needs to change if we decide to associate multiple orchestrations with a thread
            $"mapping_{mapping.ThreadId}",
            mapping.ThreadId,
            mapping.OrchestrationInstanceId,
            mapping.CreatedTimestamp,
            mapping.ModifiedTimestamp
        );

    public ThreadOrchestrationMapping ToDomainModel() =>
    new ThreadOrchestrationMapping(
        Id,
        ThreadId,
        OrchestrationInstanceId,
        CreatedTimestamp,
        ModifiedTimestamp
    );
}