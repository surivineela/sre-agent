// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// TodoPlan model for Cosmos DB
public record TodoPlanDocument(
    string Id,
    string Title,
    string ThreadId,
    string TriggerMessageId,
    TodoPlanStatus Status,
    IEnumerable<TodoItemDocument> Items,
    DateTime CreatedAt,
    DateTime? LastUpdated = null
) : ICosmosDocument
{
    public string DocumentType => "TodoPlan";
    public string PartitionKey => ThreadId; // Thread Id as partition key to co-locate with thread data
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static TodoPlanDocument FromDomainModel(TodoPlan plan) =>
        new(
            plan.Id.ToString(),
            plan.Title,
            plan.ThreadId.ToString(),
            plan.TriggerMessageId.ToString(),
            plan.Status,
            plan.Items.Select(TodoItemDocument.FromDomainModel),
            plan.CreatedAt,
            plan.LastUpdated
        );

    public TodoPlan ToDomainModel() =>
        new()
        {
            Id = Guid.Parse(Id),
            Title = Title,
            ThreadId = Guid.Parse(ThreadId),
            TriggerMessageId = Guid.Parse(TriggerMessageId),
            Status = Status,
            Items = [.. Items.Select(item => item.ToDomainModel())],
            CreatedAt = CreatedAt,
            LastUpdated = LastUpdated
        };
}

public record TodoItemDocument(
    string Content,
    string ActiveForm,
    TodoItemStatus Status,
    int Order,
    DateTime? StartedAt = null,
    DateTime? CompletedAt = null
)
{
    // Conversion to/from domain model
    public static TodoItemDocument FromDomainModel(TodoItem item) =>
        new(
            item.Content,
            item.ActiveForm,
            item.Status,
            item.Order,
            item.StartedAt,
            item.CompletedAt
        );

    public TodoItem ToDomainModel() =>
        new()
        {
            Content = Content,
            ActiveForm = ActiveForm,
            Status = Status,
            Order = Order,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt
        };
}
