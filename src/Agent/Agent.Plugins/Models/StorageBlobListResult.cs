namespace Agent.Plugins.Models
{
    /// <summary>
    /// Result of listing blobs in a storage container
    /// </summary>
    public class StorageBlobListResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the storage account name
        /// </summary>
        public string StorageAccountName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the container name
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of blobs in the container
        /// </summary>
        public List<StorageBlobItem> Blobs { get; set; } = new List<StorageBlobItem>();

        /// <summary>
        /// Gets or sets the error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URI of the container
        /// </summary>
        public string ContainerUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when the listing was performed
        /// </summary>
        public DateTime ListingTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the next marker for pagination (if available)
        /// </summary>
        public string NextMarker { get; set; } = string.Empty;
        public string Details { get; internal set; } = string.Empty;
    }
}
