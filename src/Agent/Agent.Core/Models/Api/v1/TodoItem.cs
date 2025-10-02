// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

public sealed record TodoItem
{
    public required string Content { get; set; }
    public required string ActiveForm { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TodoItemStatus Status { get; set; }

    public required int Order { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
