// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Plugin definition for verifying container registry connectivity and authentication
    /// </summary>
    public class ContainerImagePullFailurePluginDefinition
    {
        private readonly IContainerImagePullFailurePlugin _containerImagePullFailurePlugin;

        public ContainerImagePullFailurePluginDefinition(IContainerImagePullFailurePlugin containerImagePullFailurePlugin)
        {
            _containerImagePullFailurePlugin = containerImagePullFailurePlugin;
        }

        [KernelFunction("check_acr_authentication")]
        [Description("Check if the Container App has proper authentication to Azure Container Registry. Validates ACR connectivity and permissions.")]
        public async Task<AcrAuthenticationStatus> CheckAcrAuthentication(
            [Description("Resource ID of the Container App to check")]
            string resourceId,
            
            [Description("Image reference to check authentication for")]
            string imageReference)
        {
            return await _containerImagePullFailurePlugin.CheckAcrAuthentication(resourceId, imageReference);
        }

        [KernelFunction("verify_external_registry")]
        [Description("Verify connectivity and authentication to an external container registry. Checks for rate limits, availability issues, or authentication failures.")]
        public async Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(
            [Description("Resource ID of the Container App to check")]
            string resourceId,
            
            [Description("Image reference to check connectivity for")]
            string imageReference)
        {
            return await _containerImagePullFailurePlugin.VerifyExternalRegistry(resourceId, imageReference);
        }

        [KernelFunction("check_image_pulling")]
        [Description("Checking the status of image pulling for given container resource")]
        public async Task<ImagePullingResult> CheckImagePulling(
            [Description("Resource ID of the Container App to check")]
            string resourceId
        )
        {
            return await _containerImagePullFailurePlugin.CheckImagePulling(resourceId);
        }
    }
}
