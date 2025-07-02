using System;
using System.Collections.Generic;

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Result of a blob container verification check
    /// </summary>
    public class BlobContainerVerificationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the verification was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether files were found in the container
        /// </summary>
        public bool FilesFound { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the specific target file was found
        /// </summary>
        public bool TargetFileFound { get; set; }

        /// <summary>
        /// Gets or sets the container URI that was verified
        /// </summary>
        public string VerifiedContainerUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific target file path that was being verified
        /// </summary>
        public string TargetFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage account name
        /// </summary>
        public string StorageAccountName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the container name
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message if verification failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of blob files found in the container
        /// </summary>
        public List<StorageBlobItem> Files { get; set; } = new List<StorageBlobItem>();

        /// <summary>
        /// Gets or sets additional details about the verification
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when the verification was performed
        /// </summary>
        public DateTime VerificationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the next marker for pagination (if available)
        /// </summary>
        public string NextMarker { get; set; } = string.Empty;
    }
}
