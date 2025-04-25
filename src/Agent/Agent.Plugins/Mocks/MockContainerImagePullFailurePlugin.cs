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
        private readonly Dictionary<string, (string ImageReference, string ErrorMessage, bool IsFixed)> _containerApps = new();

        public void SetupContainerAppWithImagePullFailure(string resourceId, string imageReference, string errorMessage)
        {
            _containerApps[resourceId] = (imageReference, errorMessage, false);
        }

        public bool IsContainerAppFixed(string resourceId)
        {
            return _containerApps.ContainsKey(resourceId) && _containerApps[resourceId].IsFixed;
        }

        public Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId, string imageReference)
        {
            if (_containerApps.TryGetValue(resourceId, out var appInfo))
            {
                return Task.FromResult(new AcrAuthenticationStatus
                {
                    IsAuthenticated = appInfo.IsFixed,
                    ErrorMessage = !appInfo.IsFixed
                        ? "Authentication is not configured correctly. The container app needs a managed identity with AcrPull role."
                        : string.Empty
                });
            }
            return Task.FromResult(new AcrAuthenticationStatus { IsAuthenticated = false, ErrorMessage = "Container app not found." });
        }

        public Task<ExternalRegistryVerificationResult> VerifyExternalRegistryAsync(string resourceId, string imageReference)
        {
            if (_containerApps.TryGetValue(resourceId, out var appInfo))
            {
                return Task.FromResult(new ExternalRegistryVerificationResult
                {
                    RegistryAccessible = appInfo.IsFixed,
                    FailureReason = !appInfo.IsFixed
                        ? "Unable to connect to external registry. Authentication is required."
                        : string.Empty
                });
            }
            return Task.FromResult(new ExternalRegistryVerificationResult { RegistryAccessible = false, ErrorDetails = "Container app not found." });
        }

        public Task<ImagePullingResult> CheckImagePulling(string resourceId)
        {
            if (_containerApps.TryGetValue(resourceId, out var appInfo))
            {
                return Task.FromResult(new ImagePullingResult
                {
                    IsSuccessful = appInfo.IsFixed,
                    FailureReason = !appInfo.IsFixed
                        ? $"Image pull failed with error: {appInfo.ErrorMessage}"
                        : string.Empty
                });
            }
            return Task.FromResult(new ImagePullingResult { IsSuccessful = false, FailureReason = "Container app not found." });
        }

        public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(string resourceId)
        {
            var rules = new Dictionary<string, IReadOnlyList<SecurityRuleData>>
        {
            { resourceId, new List<SecurityRuleData> { new SecurityRuleData { Description = "Allow outbound traffic to registry" } } }
        };
            return Task.FromResult((IDictionary<string, IReadOnlyList<SecurityRuleData>>)rules);
        }

        public Task<ImagePullingResult> IsAzureContainerRegistryImageAccessibleAsync(string imageReference)
        {
            var isAccessible = _containerApps.Values.Any(app => app.ImageReference == imageReference && app.IsFixed);
            return Task.FromResult(new ImagePullingResult
            {
                IsSuccessful = isAccessible,
                FailureReason = isAccessible ? string.Empty : "Registry is not accessible or authentication is required."
            });
        }

        public Task<RollbackImageResult> RollbackToLastWorkingImage(string resourceId)
        {
            if (_containerApps.ContainsKey(resourceId))
            {
                _containerApps[resourceId] = (_containerApps[resourceId].ImageReference, _containerApps[resourceId].ErrorMessage, true);
                return Task.FromResult(new RollbackImageResult
                {
                    IsSuccessful = true,
                });
            }
            return Task.FromResult(new RollbackImageResult { IsSuccessful = false, ErrorMessage = "Container app not found." });
        }

        public Task<ContainerUpdateResult> UpdateContainerImage(string resourceId, string newImageReference, string containerName = null)
        {
            if (_containerApps.ContainsKey(resourceId))
            {
                _containerApps[resourceId] = (newImageReference, _containerApps[resourceId].ErrorMessage, _containerApps[resourceId].IsFixed);
                return Task.FromResult(new ContainerUpdateResult
                {
                    IsSuccessful = true,
                });
            }
            return Task.FromResult(new ContainerUpdateResult { IsSuccessful = false, ErrorMessage = "Container app not found." });
        }

        public Task<ImagePullResult> RetryImagePull(string imageReference, string resourceId = null, bool useResourceAuth = true)
        {
            if (resourceId != null && _containerApps.ContainsKey(resourceId))
            {
                // Mark the container app as fixed when RetryImagePull is called
                _containerApps[resourceId] = (_containerApps[resourceId].ImageReference, _containerApps[resourceId].ErrorMessage, true);

                return Task.FromResult(new ImagePullResult
                {
                    IsSuccessful = true,
                });
            }
            return Task.FromResult(new ImagePullResult { IsSuccessful = false, ErrorMessage = "Container app not found." });
        }

        public Task<string> GetImageReferenceFromResourceId(string resourceId)
        {
            if (_containerApps.TryGetValue(resourceId, out var appInfo))
            {
                return Task.FromResult(appInfo.ImageReference);
            }
            return Task.FromResult<string>(null);
        }
    }
}
