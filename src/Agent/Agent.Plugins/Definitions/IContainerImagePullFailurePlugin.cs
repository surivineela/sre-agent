// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public interface IContainerImagePullFailurePlugin
    {
        /// <summary>
        /// Checks if a Container App is properly authenticated to an Azure Container Registry
        /// </summary>
        /// <param name="resourceId">Resource ID of the Container App to check</param>
        /// <param name="imageReference">Image reference to check authentication for</param>
        Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId, string imageReference);

        /// <summary>
        /// Verifies connectivity and authentication to an external registry
        /// </summary>
        /// <param name="resourceId">Resource ID of the Container App to check</param>
        /// <param name="imageReference">Image reference to check</param>
        Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId, string imageReference);
    }
}
