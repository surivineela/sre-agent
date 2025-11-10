// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Message model for Cosmos DB
public record MessageDocument(
    string Id,
    string ThreadId,
    DateTime TimeStamp,
    Author Author,
    string Text,
    bool IsImageContent = false,
    Posted? Posted = null,
    Approval? Approval = null,
    AzCliExecution? AzCliExecution = null,
    KubectlExecution? KubectlExecution = null,
    PsqlExecution? PsqlExecution = null,
    // e.g. If this message belongs to a PagerDuty incident thread and is a discussion(called note in PagerDuty),
    // it is the PagerDuty note id. PagerDuty note id is is not a guid
    string? IncidentDiscussionId = null,
    bool IsDailyReport = false,
    // Agent Task information associated with this message (for deep investigation notifications)
    AgentTaskInfo? AgentTaskInfo = null,
    // Memory search results from agent memory plugin
    MemorySearchResult? MemorySearchResult = null,
    // Todo Plan information associated with this message (for todo plan notifications)
    TodoInfo? TodoInfo = null,
    // Indicates if the message is complete (e.g., streaming is finished)
    bool IsComplete = true
) : ICosmosDocument
{
    public string DocumentType => "Message";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key to keep messages with their thread
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Conversion to/from domain model
    public static MessageDocument FromDomainModel(Message message, string threadId) =>
        new MessageDocument(
            message.Id.ToString(),
            threadId,
            message.TimeStamp,
            new Author(message.Author.Role, message.Author.UserId, message.Author.DisplayName),
            message.Text,
            message.IsImageContent,
            message.Posted,
            Approval: message.Approval ?? null,
            AzCliExecution: message.AzCliExecution ?? null,
            KubectlExecution: message.KubectlExecution ?? null,
            PsqlExecution: message.PsqlExecution ?? null,
            IncidentDiscussionId: message.IncidentDiscussionId,
            message.IsDailyReport,
            message.AgentTaskInfo ?? null,
            message.MemorySearchResult ?? null,
            message.TodoInfo ?? null,
            message.IsComplete
        );

    public Message ToDomainModel(Approval? approval = null, bool isDailyReport = false) =>
        new Message(
            Guid.Parse(Id),
            TimeStamp,
            Author,
            Text,
            IsImageContent,
            Posted,
            Approval,
            AzCliExecution,
            KubectlExecution,
            PsqlExecution,
            IncidentDiscussionId: IncidentDiscussionId,
            IsDailyReport,
            AgentTaskInfo,
            MemorySearchResult: MemorySearchResult,
            TodoInfo: TodoInfo,
            IsComplete: IsComplete
        );
}
