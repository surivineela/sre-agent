// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models;

/// <summary>
/// Top-level collection response for thread messages from the V1 API.
/// </summary>
public class ThreadMessageCollectionV1
{
    [JsonPropertyName("value")]
    public List<ThreadMessageV1> Value { get; set; } = [];

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Represents a message in a thread from the V1 API.
/// </summary>
public class ThreadMessageV1
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timeStamp")]
    public DateTime TimeStamp { get; set; }

    [JsonPropertyName("author")]
    public ThreadMessageAuthorV1 Author { get; set; } = new();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("isComplete")]
    public bool IsComplete { get; set; }

    [JsonPropertyName("messageType")]
    public string? MessageType { get; set; }
}

/// <summary>
/// Represents the author of a message.
/// </summary>
public class ThreadMessageAuthorV1
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
