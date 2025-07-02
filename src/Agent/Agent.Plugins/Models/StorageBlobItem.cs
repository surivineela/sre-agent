using System;

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Represents a blob item in Azure Storage
    /// </summary>
    public class StorageBlobItem
    {
        /// <summary>
        /// Gets or sets the name of the blob
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content length (size) of the blob in bytes
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Gets or sets the content type of the blob
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ETag of the blob
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the creation time of the blob
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the last modified time of the blob
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets or sets the MD5 hash of the blob content
        /// </summary>
        public string ContentMD5 { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of blob (Block, Page, etc.)
        /// </summary>
        public string BlobType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the lease status of the blob
        /// </summary>
        public string LeaseStatus { get; set; } = string.Empty;
    }
}
