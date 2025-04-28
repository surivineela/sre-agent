// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
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
            string resourceId,
            [Description("Image reference to check authentication for (e.g. myregistry.azurecr.io/myapp:v2)")]
            string imageReference)
        {
            return await _containerImagePullFailurePlugin.CheckAcrAuthentication(resourceId, imageReference);
        }

        [KernelFunction("verify_external_registry")]
        [Description("Verify connectivity and authentication to an external container registry. Checks for rate limits, availability issues, or authentication failures.")]
        public async Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(
            [Description("Resource ID of the Container App to check")]
            string resourceId,
            [Description("Image reference to check authentication for (e.g. myregistry.azurecr.io/myapp:v2)")]
            string imageReference
            )
        {
            return await _containerImagePullFailurePlugin.VerifyExternalRegistryAsync(resourceId, imageReference);
        }

        // [KernelFunction("check_image_pulling")]
        // [Description("Checking the status of image pulling for given container resource")]
        // public async Task<ImagePullingResult> CheckImagePulling(
        //     [Description("Resource ID of the Container App to check")]
        //     string resourceId)
        // {
        //     return await _containerImagePullFailurePlugin.CheckImagePulling(resourceId);
        // }

        [KernelFunction("is_acr_image_manifest_accessible")]
        [Description("Check if the image in ACR is accessible. Validates ACR connectivity.")]
        public async Task<ImagePullingResult> IsAzureContainerRegistryImageAccessibleAsync(
            [Description("Image reference to check if it is accessible for (e.g. myregistry.azurecr.io/myapp:v2)")]
            string imageReference)
        {
            return await _containerImagePullFailurePlugin.IsAzureContainerRegistryImageAccessibleAsync(imageReference);
        }

        [KernelFunction("rollback_to_last_working_image")]
        [Description("Rolls back a Container App or Linux Web App to the last known working image. This is useful when a new image deployment causes pull failures or other issues.")]
        [RequiresApproval]
        public async Task<RollbackImageResult> RollbackToLastWorkingImage(
            [Description("Resource ID of the Container App or Linux Web App to roll back")]
            string resourceId)
        {
            return await _containerImagePullFailurePlugin.RollbackToLastWorkingImage(resourceId);
        }

        [KernelFunction("update_container_image")]
        [Description("Updates the container image for a Container App or Linux Web App. This enables changing to a new image version or completely different image.")]
        [RequiresApproval]
        public async Task<ContainerUpdateResult> UpdateContainerImage(
            [Description("Resource ID of the Container App or Linux Web App")]
            string resourceId,
            
            [Description("New image reference to use (e.g. myregistry.azurecr.io/myapp:v2)")]
            string newImageReference,
            
            [Description("Optional container name for multi-container apps. If not specified, the first container will be updated.")]
            string containerName = null)
        {
            return await _containerImagePullFailurePlugin.UpdateContainerImage(resourceId, newImageReference, containerName);
        }

        [KernelFunction("retry_image_pull")]
        [Description("Attempts to pull a container image to verify that it's accessible and properly authenticated. This can help troubleshoot image pull failures.")]
        [RequiresApproval]
        public async Task<ImagePullResult> RetryImagePull(
            [Description("The full image reference to try pulling (e.g. myregistry.azurecr.io/myapp:v2)")]
            string imageReference,
            
            [Description("Optional resource ID to use its authentication context for pulling the image")]
            string resourceId = null,
            
            [Description("Whether to use the resource's authentication configuration. Default is true.")]
            bool useResourceAuth = true)
        {
            return await _containerImagePullFailurePlugin.RetryImagePull(imageReference, resourceId, useResourceAuth);
        }
    }
}
