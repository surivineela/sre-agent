// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.DataModels;

// Extended Thread model for Cosmos DB
public record ThreadDocument(
    string Id,
    string Title,
    string MessageId, // Reference to the start message
    string LastMessageId, // Reference to the last message
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp,
    ThreadSource Source = ThreadSource.Conversation,
    DateTime EvaluatedTimestamp = default,
    DateTime TrajectoryGeneratedTimestamp = default,
    IncidentSource? IncidentSource = null,
    ThreadType? ThreadType = ThreadType.Prod,
    FeatureConfig? FeatureConfig = null,
    IEnumerable<AgentTaskShort>? AgentTasks = null
) : ICosmosDocument
{
    public string DocumentType => "Thread";
    public string PartitionKey => Id; // Use Thread Id as partition key
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string IncidentId { get; set; } = string.Empty; // Incident Id associated with the thread if the source of the thread is incident
    public string IncidentStatus { get; set; } = string.Empty;
    public DateTime? LastReadTime { get; set; } = null; // The last time the thread was read
    public string? AgentMode { get; set; } = null; // Agent mode configuration for the thread

    // Conversion to/from domain model
    public static ThreadDocument FromDomainModel(Thread thread) =>
        new ThreadDocument(
            thread.Id.ToString(),
            thread.Title,
            thread.StartMessage?.Id.ToString() ?? string.Empty,
            thread.LastMessage?.Id.ToString() ?? string.Empty,
            thread.CreatedTimestamp,
            thread.ModifiedTimestamp,
            thread.Source,
            thread.EvaluatedTimestamp,
            thread.TrajectoryGeneratedTimestamp,
            IncidentSource: thread.IncidentSource,
            ThreadType: thread.Type,
            FeatureConfig: thread.FeatureConfig?.ToDocument(),
            AgentTasks: thread.AgentTasks
        )
        {
            IncidentId = thread.Status?.IncidentStatus?.IncidentId ?? string.Empty,
            IncidentStatus = thread.Status?.IncidentStatus?.Status ?? string.Empty,
            LastReadTime = thread.LastReadTime,
            AgentMode = thread.AgentMode
        };

    public Thread ToDomainModel(Message startMessage, Message? lastMessage) =>
        new Thread(
            Id: Guid.Parse(Id),
            Title: Title,
            StartMessage: startMessage,
            LastMessage: lastMessage,
            CreatedTimestamp: CreatedTimestamp,
            ModifiedTimestamp: ModifiedTimestamp,
            FeatureConfig: FeatureConfig?.ToModel() ?? FeatureConfigModel.Default,
            Source: Source,
            IncidentSource: IncidentSource,
            Type: ThreadType,
            AgentTasks: AgentTasks
        )
        {
            LastReadTime = LastReadTime,
            EvaluatedTimestamp = EvaluatedTimestamp,
            TrajectoryGeneratedTimestamp = TrajectoryGeneratedTimestamp,
            AgentMode = AgentMode,
            Status = new Status()
            {
                IncidentStatus = new IncidentStatus
                {
                    IncidentId = IncidentId,
                    Status = IncidentStatus
                }
            }
        };
}
