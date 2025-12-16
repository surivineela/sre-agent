// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

/// <summary>
/// Configuration settings for tool output storage
/// </summary>
public class ToolOutputSettings
{
    /// <summary>
    /// Base directory path for storing tool outputs
    /// Default: System temp directory + "SREAgent/ToolOutputs"
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Number of days to retain tool output files before cleanup
    /// Default: 1 day
    /// </summary>
    public int RetentionDays { get; set; } = 1;

    /// <summary>
    /// Whether Azure Blob Storage is enabled for tool output storage
    /// If false, uses local file system storage
    /// </summary>
    public bool StorageAccountEnabled { get; set; } = false;

    /// <summary>
    /// Azure Storage Account name for blob storage
    /// Required when StorageAccountEnabled is true
    /// </summary>
    public string StorageAccountName { get; set; } = string.Empty;

    /// <summary>
    /// Azure Blob Storage domain suffix (e.g., blob.core.windows.net)
    /// </summary>
    public string BlobStorageDomainSuffix { get; set; } = "blob.core.windows.net";

    /// <summary>
    /// Azure Blob Storage container name for tool outputs
    /// </summary>
    public string BlobStorageContainerName { get; set; } = "tooloutput";

    /// <summary>
    /// Maximum number of characters allowed in tool output retrieval results
    /// Default: 65536 characters (64KB)
    /// </summary>
    public int MaxOutputChars { get; set; } = 65536;

    /// <summary>
    /// Enables the ToolOutputRetriever tool for accessing truncated tool outputs
    /// When enabled, agents will have access to the ToolOutputRetriever tool and related common prompt
    /// Default: false
    /// </summary>
    public bool EnablePartialOutput { get; set; } = false;

    /// <summary>
    /// Validates that required ToolOutputSettings configuration for Azure Blob Storage are present
    /// </summary>
    /// <returns>True if configuration is valid for Azure Blob Storage, false otherwise</returns>
    public bool IsStorageAccountValid()
    {
        // If storage account is not enabled, configuration is valid (will use local storage)
        if (!StorageAccountEnabled)
        {
            return false;
        }

        // If storage account is enabled, validate required Azure Blob Storage settings
        return !string.IsNullOrEmpty(StorageAccountName)
            && !string.IsNullOrEmpty(BlobStorageDomainSuffix)
            && !string.IsNullOrEmpty(BlobStorageContainerName);
    }
}
