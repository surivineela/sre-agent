// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Helpers.ArmModels;

public class ArmListWrapper<T>
{
    [JsonPropertyName("value")]
    public required IReadOnlyCollection<ArmWrapper<T>> Value { get; init; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; init; }
}
