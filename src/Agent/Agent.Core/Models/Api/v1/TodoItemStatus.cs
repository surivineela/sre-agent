// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TodoItemStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum TodoPlanUpdateType
{
    Created,
    Updated,
    ItemStatusChanged,
    Completed,
    Cancelled
}
