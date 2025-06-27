using System;

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Result of a zip file verification check
    /// </summary>
    public class ZipFileVerificationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the verification was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the path that was verified
        /// </summary>
        public string VerifiedPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message if verification failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details about the verification
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time when the verification was performed
        /// </summary>
        public DateTime VerificationTime { get; set; } = DateTime.UtcNow;
    }
}
