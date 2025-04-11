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
        /// <returns>Authentication status and any error details</returns>
        Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId);

        /// <summary>
        /// Verifies connectivity and authentication to an external (non-ACR) registry
        /// </summary>
        /// <param name="resourceId">The resource ID to verify external registry for</param>
        /// <returns>Verification result with detailed status</returns>
        Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId);

        /// <summary>
        /// Checks if there are any current or recent image pulling issues
        /// </summary>
        /// <param name="resourceId">The resource ID to check image pulling status for</param>
        /// <returns>Image pulling status with any error details</returns>
        Task<ImagePullingResult> CheckImagePulling(string resourceId);

        /// <summary>
        /// Checks if the ACR image manifest is accessible
        /// </summary>
        /// <param name="resourceId">The resource ID to check ACR image manifest accessibility for</param>
        /// <returns>Result indicating if the manifest is accessible</returns>
        Task<ImagePullingResult> IsACRImageManifestAccessibleAsync(string resourceId);
    }
}
