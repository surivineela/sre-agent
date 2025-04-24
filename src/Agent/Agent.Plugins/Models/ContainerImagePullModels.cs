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

    public class ImagePullingResult
    {
        public bool IsSuccessful { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Result of a rollback operation to a previous working container image
    /// </summary>
    public class RollbackImageResult
    {
        /// <summary>
        /// Resource ID of the container app or web app
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Whether the rollback was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The current image reference before rollback
        /// </summary>
        public string CurrentImage { get; set; }

        /// <summary>
        /// The image reference that was rolled back to
        /// </summary>
        public string RolledBackToImage { get; set; }

        /// <summary>
        /// The name/ID of the previous revision or deployment that was used for rollback
        /// </summary>
        public string PreviousRevision { get; set; }

        /// <summary>
        /// Error message if rollback failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Potential solution to fix the rollback issue
        /// </summary>
        public string PotentialSolution { get; set; }
    }

    /// <summary>
    /// Result of updating a container image in a Container App or Web App
    /// </summary>
    public class ContainerUpdateResult
    {
        /// <summary>
        /// Resource ID of the container app or web app
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Whether the update was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The previous image reference before update
        /// </summary>
        public string PreviousImage { get; set; }

        /// <summary>
        /// The new image reference after update
        /// </summary>
        public string NewImage { get; set; }

        /// <summary>
        /// Name of the container that was updated (for multi-container apps)
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Error message if update failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Potential solution to fix the update issue
        /// </summary>
        public string PotentialSolution { get; set; }

        /// <summary>
        /// When the update was performed
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Result of attempting to pull a container image
    /// </summary>
    public class ImagePullResult
    {
        /// <summary>
        /// Whether the image pull was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The image reference that was attempted to pull
        /// </summary>
        public string ImageReference { get; set; }

        /// <summary>
        /// Registry type of the image
        /// </summary>
        public RegistryType RegistryType { get; set; }

        /// <summary>
        /// Authentication method used for the pull attempt
        /// </summary>
        public string AuthenticationMethod { get; set; }

        /// <summary>
        /// Detailed error message if pull failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Technical details about the pull operation
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Suggested fix if the pull failed
        /// </summary>
        public string SuggestedFix { get; set; }

        /// <summary>
        /// Time taken in seconds to attempt the pull
        /// </summary>
        public double PullDurationSeconds { get; set; }

        /// <summary>
        /// When the pull was attempted
        /// </summary>
        public DateTimeOffset PullAttemptedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Types of container registries
    /// </summary>
    public enum RegistryType
    {
        Other,
        AzureContainerRegistry,
        DockerHub,
        MicrosoftContainerRegistry,
        GoogleContainerRegistry,
        KubernetesRegistry,
        PrivateRegistry
    }
    /// <summary>
    /// Result of analyzing a container registry for image pull failures
    /// </summary>
    public class DiagnosticResult
    {
        public bool DnsResolved { get; set; }
        public List<string> IpAddresses { get; set; }
        public bool HttpsAccessible { get; set; }
        public bool IsRegistryApi { get; set; }
        public bool RequiresAuth { get; set; }
        public string AuthScheme { get; set; }
        public int StatusCode { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Result of checking network security rules for a container app
    /// </summary>
    public class ConnectivityTestResult
    {
        public bool IsConnected { get; set; }
        public int HttpStatusCode { get; set; }
        public string ErrorMessage { get; set; }
        public string PotentialSolution { get; set; }
    }

    /// <summary>
    /// Information about an image reference
    /// </summary>
    public class ImageReferenceInfo
    {
        public string Registry { get; set; }
        public string Repository { get; set; }
        public string Tag { get; set; }
        public RegistryType RegistryType { get; set; }
        public bool RequiresAuth { get; set; }
    }

    public class RegistryAuthConfig
    {
        public string RegistryServer { get; set; }
        public string Username { get; set; }
        public string PasswordReference { get; set; }
        public string IdentityReference { get; set; }
        public bool IsSystemManagedIdentity { get; set; }
    }

    public class NetworkSecurityAnalysis
    {
        public bool HasBlockingRules { get; set; }
        public List<string> BlockingRuleNames { get; set; }
        public List<string> RecommendedActions { get; set; }
        public Dictionary<string, List<string>> AffectedEndpoints { get; set; }

        public NetworkSecurityAnalysis()
        {
            BlockingRuleNames = new List<string>();
            RecommendedActions = new List<string>();
            AffectedEndpoints = new Dictionary<string, List<string>>();
        }
    }

    public class ContainerDiagnosticResult
    {
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public bool HasIssues { get; set; }
        public List<DiagnosticIssue> Issues { get; set; }
        public Dictionary<string, string> ResourceProperties { get; set; }
        public List<string> Recommendations { get; set; }

        public ContainerDiagnosticResult()
        {
            Issues = new List<DiagnosticIssue>();
            ResourceProperties = new Dictionary<string, string>();
            Recommendations = new List<string>();
        }
    }

    public class DiagnosticIssue
    {
        public string Category { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public List<string> PossibleSolutions { get; set; }
        public Dictionary<string, string> Context { get; set; }

        public DiagnosticIssue()
        {
            PossibleSolutions = new List<string>();
            Context = new Dictionary<string, string>();
        }
    }
}
