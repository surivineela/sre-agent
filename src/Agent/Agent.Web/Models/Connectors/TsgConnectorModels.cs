// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Core.Helpers;
using Agent.Data.DataModels;

namespace Agent.Web.Models.Connectors;

/// <summary>
/// Request model for creating/updating a TSG connector
/// </summary>
public record TsgConnectorRequest
{
    /// <summary>
    /// Name of the connector
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Azure DevOps repository or wiki URL
    /// </summary>
    [Required]
    public string DataSource { get; init; } = string.Empty;

    /// <summary>
    /// Personal Access Token for Azure DevOps authentication
    /// </summary>
    [Required]
    public string PersonalAccessToken { get; init; } = string.Empty;
}

/// <summary>
/// Response model for TSG connector operations
/// </summary>
public record TsgConnectorResponse
{
    /// <summary>
    /// Name of the connector
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Repository URL (Azure DevOps or GitHub)
    /// </summary>
    public string DataSource { get; init; } = string.Empty;

    /// <summary>
    /// Type of repository (AzureDevOps or GitHub)
    /// </summary>
    public RepoType RepoType { get; init; } = RepoType.AzureDevOps;

    /// <summary>
    /// Indicates if credentials (PAT) are stored for this connector
    /// </summary>
    public bool HasCredentials { get; init; }

    /// <summary>
    /// Current status of the connector
    /// </summary>
    public ConnectorStatus Status { get; init; } = ConnectorStatus.Healthy;

    /// <summary>
    /// When the connector was last validated
    /// </summary>
    public DateTime? LastValidated { get; init; }

    /// <summary>
    /// Optional error message if status is not healthy
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Clone status: NotStarted, Cloning, Syncing, Ready, Failed
    /// </summary>
    public CloneStatus CloneStatus { get; init; } = CloneStatus.NotStarted;

    /// <summary>
    /// When the repository was last successfully synced
    /// </summary>
    public DateTime? LastSuccessfulSync { get; init; }

    /// <summary>
    /// Local path where the repository is cloned
    /// </summary>
    public string? LocalPath { get; init; }

    /// <summary>
    /// Latest commit hash after clone/sync
    /// </summary>
    public string? LatestCommit { get; init; }
}

/// <summary>
/// Response model for connectivity test
/// </summary>
public record TsgConnectorTestResponse
{
    /// <summary>
    /// Whether the connectivity test was successful
    /// </summary>
    public bool IsSuccessful { get; init; }

    /// <summary>
    /// Error message if the test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional details about the test result
    /// </summary>
    public string? Details { get; init; }
}
