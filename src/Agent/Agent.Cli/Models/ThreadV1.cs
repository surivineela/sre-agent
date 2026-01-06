// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models;

/// <summary>
/// Top-level collection response for thread from the V1 API.
/// </summary>
public class ThreadCollectionV1
{
    [JsonPropertyName("value")]
    public List<ThreadV1> Value { get; set; } = [];
}

/// <summary>
/// Represents a thread in the SRE Agent system.
/// </summary>
public class ThreadV1
{
    /// <summary>
    /// The unique identifier of the thread.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The title of the thread.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The timestamp when the thread was created.
    /// </summary>
    [JsonPropertyName("createdTimestamp")]
    public DateTime CreatedTimestamp { get; set; }

    /// <summary>
    /// The timestamp when the thread was last modified.
    /// </summary>
    [JsonPropertyName("modifiedTimestamp")]
    public DateTime ModifiedTimestamp { get; set; }
}
