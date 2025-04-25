// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Agent.Plugins.Definitions
{
    public interface IContainerImagePullFailurePlugin
    {
        /// <summary>
        /// Gets the container image reference from a resource ID
        /// </summary>
        /// <param name="resourceId">The resource ID of a Container App or Linux Web App</param>
        /// <returns>The container image reference if found, null otherwise</returns>
        Task<string> GetImageReferenceFromResourceId(string resourceId);

        /// <summary>
        /// Gets network security rules that might affect container image pulling
        /// </summary>
        /// <param name="resourceId">The resource ID to check NSG rules for</param>
        /// <returns>Dictionary of NSG names and their security rules</returns>
        Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(string resourceId);

        /// <summary>
        /// Checks if the resource has proper authentication configured for Azure Container Registry
        /// </summary>
        /// <param name="resourceId">The resource ID to check ACR authentication for</param>
        /// <param name="imageReference">The image reference to check authentication for (e.g. myregistry.azurecr.io/myapp:v2)</param>
        /// <returns>Authentication status and any error details</returns>
        Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId, string imageReference);

        /// <summary>
        /// Verifies connectivity and authentication to an external (non-ACR) registry
        /// </summary>
        /// <param name="resourceId">The resource ID to verify external registry for</param>
        /// <param name="imageReference">The image reference to check authentication for (e.g. myregistry.azurecr.io/myapp:v2)</param>
        /// <returns>Verification result with detailed status</returns>
        Task<ExternalRegistryVerificationResult> VerifyExternalRegistryAsync(string resourceId, string imageReference);

        /// <summary>
        /// Checks if there are any current or recent image pulling issues
        /// </summary>
        /// <param name="resourceId">The resource ID to check image pulling status for</param>
        /// <returns>Image pulling status with any error details</returns>
        Task<ImagePullingResult> CheckImagePulling(string resourceId);

        /// <summary>
        /// Checks if the ACR image is accessible
        /// </summary>
        /// <param name="imageReference">The image reference to check if it is accessible (e.g. myregistry.azurecr.io/myapp:v2)</param>
        /// <returns>Result indicating if the image is accessible</returns>
        Task<ImagePullingResult> IsAzureContainerRegistryImageAccessibleAsync(string imageReference);

        /// <summary>
        /// Rolls back a Container App or Web App to the last known working image
        /// </summary>
        /// <param name="resourceId">The resource ID of the Container App or Web App</param>
        /// <returns>Result of the rollback operation</returns>
        Task<RollbackImageResult> RollbackToLastWorkingImage(string resourceId);

        /// <summary>
        /// Updates the container image reference for a Container App or Web App
        /// </summary>
        /// <param name="resourceId">The resource ID of the Container App or Web App</param>
        /// <param name="newImageReference">The new image reference to use</param>
        /// <param name="containerName">Optional container name for multi-container apps</param>
        /// <returns>Result of the update operation</returns>
        Task<ContainerUpdateResult> UpdateContainerImage(string resourceId, string newImageReference, string containerName = null);

        /// <summary>
        /// Attempts to pull an image to verify accessibility and authentication
        /// </summary>
        /// <param name="imageReference">The image reference to try pulling</param>
        /// <param name="resourceId">Optional resource ID to use its authentication context</param>
        /// <param name="useResourceAuth">Whether to use the resource's authentication configuration</param>
        /// <returns>Result of the image pull attempt</returns>
        Task<ImagePullResult> RetryImagePull(string imageReference, string resourceId = null, bool useResourceAuth = true);
    }
}
