// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

public sealed record TodoPlan
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public required Guid ThreadId { get; set; }
    public required Guid TriggerMessageId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TodoPlanStatus Status { get; set; }

    public required IReadOnlyList<TodoItem> Items { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdated { get; set; }
}
