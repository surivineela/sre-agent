// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure.ResourceManager.Network;

namespace Agent.Plugins.Mocks
{
    public class MockContainerImagePullFailurePlugin : IContainerImagePullFailurePlugin
    {
        public Task<string> GetImageReferenceFromResourceId(string resourceId)
        {
            return Task.FromResult("myregistry.azurecr.io/myapp:latest");
        }

        public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(string resourceId)
        {
            return Task.FromResult<IDictionary<string, IReadOnlyList<SecurityRuleData>>>(
                new Dictionary<string, IReadOnlyList<SecurityRuleData>>());
        }

        public Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId, string imageReference)
        {
            var result = new AcrAuthenticationStatus
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsAuthenticated = true
            };
            return Task.FromResult(result);
        }

        public Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId, string imageReference)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                RegistryType = RegistryType.DockerHub,
                IsSuccessful = true,
                RegistryAccessible = true
            };
            return Task.FromResult(result);
        }

        public Task<ImagePullingResult> CheckImagePulling(string resourceId)
        {
            var result = new ImagePullingResult
            {
                IsSuccessful = true
            };
            return Task.FromResult(result);
        }

        public Task<ImagePullingResult> IsAzureContainerRegistryImageAccessibleAsync(string imageReference)
        {
            var result = new ImagePullingResult
            {
                IsSuccessful = true
            };
            return Task.FromResult(result);
        }

        public Task<RollbackImageResult> RollbackToLastWorkingImage(string resourceId)
        {
            var result = new RollbackImageResult
            {
                ResourceId = resourceId,
                IsSuccessful = true,
                CurrentImage = "myregistry.azurecr.io/myapp:latest",
                RolledBackToImage = "myregistry.azurecr.io/myapp:stable",
                PreviousRevision = "rev-1"
            };
            return Task.FromResult(result);
        }

        public Task<ContainerUpdateResult> UpdateContainerImage(string resourceId, string newImageReference, string containerName = null)
        {
            var result = new ContainerUpdateResult
            {
                ResourceId = resourceId,
                IsSuccessful = true,
                NewImage = newImageReference,
                PreviousImage = "myregistry.azurecr.io/myapp:latest",
                ContainerName = containerName ?? "default",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            return Task.FromResult(result);
        }

        public Task<ImagePullResult> RetryImagePull(string imageReference, string resourceId = null, bool useResourceAuth = true)
        {
            var result = new ImagePullResult
            {
                ImageReference = imageReference,
                IsSuccessful = true,
                PullAttemptedAt = DateTimeOffset.UtcNow,
                RegistryType = RegistryType.AzureContainerRegistry,
                AuthenticationMethod = "Managed Identity",
                Details = "Successfully accessed image manifest",
                PullDurationSeconds = 1.5
            };
            return Task.FromResult(result);
        }
    }
}
