// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Result of monitoring a container app for image pull failures
    /// </summary>
    public class ContainerAppMonitoringResult
    {
        /// <summary>
        /// Whether the monitoring operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if monitoring failed
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Name of the container app
        /// </summary>
        public string ContainerAppName { get; set; }
        
        /// <summary>
        /// Resource ID of the container app
        /// </summary>
        public string ResourceId { get; set; }
        
        /// <summary>
        /// Calculated availability percentage
        /// </summary>
        public double AvailabilityPercentage { get; set; }
        
        /// <summary>
        /// Generated chart data in base64 format
        /// </summary>
        public string ChartData { get; set; }
        
        /// <summary>
        /// Whether the availability is below the acceptable threshold
        /// </summary>
        public bool HasLowAvailability { get; set; }
        
        /// <summary>
        /// Results of container log inspection if there was low availability
        /// </summary>
        public LogInspectionResult LogInspectionResult { get; set; }
    }

    /// <summary>
    /// Result of inspecting container logs for image pull failures
    /// </summary>
    public class LogInspectionResult
    {
        /// <summary>
        /// Resource ID of the container app
        /// </summary>
        public string ResourceId { get; set; }
        
        /// <summary>
        /// Whether an image pull failure was detected
        /// </summary>
        public bool HasImagePullFailure { get; set; }
        
        /// <summary>
        /// Type of failure detected
        /// </summary>
        public string FailureType { get; set; }
        
        /// <summary>
        /// Details of the error
        /// </summary>
        public string ErrorDetails { get; set; }
        
        /// <summary>
        /// Image reference that failed to pull
        /// </summary>
        public string ImageReference { get; set; }
        
        /// <summary>
        /// Type of registry the image is from
        /// </summary>
        public RegistryType RegistryType { get; set; }
    }

    /// <summary>
    /// Status of ACR authentication check
    /// </summary>
    public class AcrAuthenticationStatus
    {
        /// <summary>
        /// Resource ID of the container app
        /// </summary>
        public string ResourceId { get; set; }
        
        /// <summary>
        /// Image reference being checked
        /// </summary>
        public string ImageReference { get; set; }
        
        /// <summary>
        /// Whether authentication is successful
        /// </summary>
        public bool IsAuthenticated { get; set; }
        
        /// <summary>
        /// Error message if authentication failed
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Potential solution to fix the authentication issue
        /// </summary>
        public string PotentialSolution { get; set; }
    }

    /// <summary>
    /// Result of verifying external registry connectivity and authentication
    /// </summary>
    public class ExternalRegistryVerificationResult
    {
        /// <summary>
        /// Resource ID of the container app
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Image reference being checked
        /// </summary>
        public string ImageReference { get; set; }

        /// <summary>
        /// Type of registry being verified
        /// </summary>
        public RegistryType RegistryType { get; set; }

        /// <summary>
        /// Whether verification was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Whether the registry is accessible
        /// </summary>
        public bool RegistryAccessible { get; set; }

        /// <summary>
        /// Reason for failure if verification failed
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Detailed error message
        /// </summary>
        public string ErrorDetails { get; set; }

        /// <summary>
        /// Recommended action to fix the issue
        /// </summary>
        public string RecommendedAction { get; set; }
    }

    /// <summary>
    /// Types of container registries
    /// </summary>
    public enum RegistryType
    {
        Unknown,
        AzureContainerRegistry,
        DockerHub,
        Other
    }
}