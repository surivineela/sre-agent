using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace FirstPartyAgent.Core.Models;

/// <summary>
/// Represents a pre-processed emerging issue configuration
/// </summary>
public class EmergingIssueConfig
{
    /// <summary>
    /// Unique identifier for this emerging issue configuration
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The ICM incident ID associated with this emerging issue
    /// </summary>
    [JsonProperty("incidentId")]
    public string IncidentId { get; set; } = string.Empty;

    /// <summary>
    /// Title of the emerging issue
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The team that owns this emerging issue
    /// </summary>
    [JsonProperty("owningTeam")]
    public string OwningTeam { get; set; } = string.Empty;
    
    /// <summary>
    /// The pre-processed content/analysis of the emerging issue
    /// </summary>
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// When this emerging issue was created
    /// </summary>
    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When this emerging issue was last modified
    /// </summary>
    [JsonProperty("lastModifiedDate")]
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Properties for CosmosDB partitioning
    /// </summary>
    [JsonProperty("partitionKey")]
    public string PartitionKey => OwningTeam;
}
