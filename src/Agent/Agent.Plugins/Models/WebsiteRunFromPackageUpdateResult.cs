using System;

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Result of a WEBSITE_RUN_FROM_PACKAGE update operation
    /// </summary>
    public class WebsiteRunFromPackageUpdateResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the update was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the resource ID that was updated
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path that was used for the update
        /// </summary>
        public string ZipFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message if the update failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details about the update
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when the update was performed
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
    }
}
