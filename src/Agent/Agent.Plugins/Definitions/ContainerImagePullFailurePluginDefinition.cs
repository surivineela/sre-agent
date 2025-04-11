// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
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

        [KernelFunction("get_image_reference")]
        [Description("Gets the container image reference from a resource ID")]
        public async Task<string> GetImageReferenceFromResourceId(
            [Description("The resource ID of a Container App or Linux Web App")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.GetImageReferenceFromResourceId(resourceId);
        }

        [KernelFunction("get_network_security_rules")]
        [Description("Gets the NSG rules that might affect container connectivity")]
        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(
            [Description("The resource ID of a Container App or Linux Web App")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.GetNetworkSecurityRulesForResource(resourceId);
        }

        [KernelFunction("check_acr_authentication")]
        [Description("Check if the Container App has proper authentication to Azure Container Registry. Validates ACR connectivity and permissions.")]
        public async Task<AcrAuthenticationStatus> CheckAcrAuthentication(
            [Description("Resource ID of the Container App to check")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.CheckAcrAuthentication(resourceId);
        }

        [KernelFunction("verify_external_registry")]
        [Description("Verify connectivity and authentication to an external container registry. Checks for rate limits, availability issues, or authentication failures.")]
        public async Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(
            [Description("Resource ID of the Container App to check")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.VerifyExternalRegistry(resourceId);
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

        [KernelFunction("is_acr_image_manifest_accessible")]
        [Description("Check if the image manifest in ACR is accessible. Validates ACR connectivity.")]
        public async Task<ImagePullingResult> IsACRImageManifestAccessibleAsync(
            [Description("Resource ID of the Container App to check")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.IsACRImageManifestAccessibleAsync(resourceId);
        }
    }
}
