using Agent.Core.Helpers;

namespace Agent.Core.Models;

/// <summary>
/// Represents a code repository configuration.
/// </summary>
public sealed record CodeRepo
{
    /// <summary>
    /// Gets the unique name of the repository.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the normalized URL of the repository (https, no .git suffix, no trailing slash).
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the type of repository (GitHub or AzureDevOps).
    /// </summary>
    public required RepoType Type { get; init; }

    /// <summary>
    /// Gets the timestamp when this repository was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when this repository was last modified.
    /// </summary>
    public DateTime? LastModified { get; init; }
}

/// <summary>
/// Represents the scanning status of a code repository.
/// </summary>
public enum ScanStatus
{
    /// <summary>
    /// Repository has not been scanned yet.
    /// </summary>
    NotScanned,

    /// <summary>
    /// Repository scan is currently in progress.
    /// </summary>
    Scanning,

    /// <summary>
    /// Repository has been successfully scanned.
    /// </summary>
    Scanned,

    /// <summary>
    /// Repository scan failed.
    /// </summary>
    Failed
}
