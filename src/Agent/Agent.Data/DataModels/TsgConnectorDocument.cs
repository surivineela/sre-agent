// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Helpers;

namespace Agent.Data.DataModels;

/// <summary>
/// Status of the connector's connectivity
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorStatus
{
    Healthy,
    Unhealthy
}

/// <summary>
/// Status of the connector's local clone operation
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CloneStatus
{
    NotStarted,
    Cloning,
    Syncing,
    Ready,
    Failed,
    PendingCredentialUpdate
}

/// <summary>
/// Cosmos DB document for TSG connector with PAT authentication
/// </summary>
public class TsgConnectorDocument : ICosmosDocument
{
    public const string DocumentTypeName = "TsgConnector";

    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public string Id { get; set; } = string.Empty;

    public string DocumentType => DocumentTypeName;

    public string PartitionKey => DocumentTypeName;

    /// <summary>
    /// Connector name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Repository URL (Azure DevOps or GitHub)
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    /// <summary>
    /// Type of repository (AzureDevOps or GitHub). Set by controller based on URL.
    /// </summary>
    public RepoType RepoType { get; set; } = RepoType.AzureDevOps;

    /// <summary>
    /// Personal Access Token (stored with Cosmos DB encryption at rest)
    /// </summary>
    public string Pat { get; set; } = string.Empty;

    /// <summary>
    /// When the connector was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the connector was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When connectivity was last validated
    /// </summary>
    public DateTime? LastValidated { get; set; }

    /// <summary>
    /// Current status of the connector
    /// </summary>
    public ConnectorStatus Status { get; set; } = ConnectorStatus.Healthy;

    /// <summary>
    /// Error message if status is not healthy
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Clone status: NotStarted, Cloning, Syncing, Ready, Failed
    /// </summary>
    public CloneStatus CloneStatus { get; set; } = CloneStatus.NotStarted;

    /// <summary>
    /// When the clone/sync operation was last started
    /// </summary>
    public DateTime? CloneStartedAt { get; set; }

    /// <summary>
    /// When the clone/sync operation was last completed
    /// </summary>
    public DateTime? CloneCompletedAt { get; set; }

    /// <summary>
    /// When the repository was last successfully synced
    /// </summary>
    public DateTime? LastSuccessfulSync { get; set; }

    /// <summary>
    /// Local path where the repository is cloned (within sandbox codeRefs)
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// Latest commit hash after clone/sync
    /// </summary>
    public string? LatestCommit { get; set; }

    public static string GetId(string name) => $"{DocumentTypeName}_{name.ToLowerInvariant()}";

    public static string GetPartitionKey() => DocumentTypeName;
}
