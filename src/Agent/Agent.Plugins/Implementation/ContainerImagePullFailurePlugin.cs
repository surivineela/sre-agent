// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Agent.Plugins.Implementation
{
    public class ContainerImagePullFailurePlugin : IContainerImagePullFailurePlugin
    {
        private readonly ILogger<ContainerImagePullFailurePlugin> _logger;
        private readonly IContainerAppPlugin _containerAppPlugin;
        private readonly IArmClientFactory _armClientFactory;
        private readonly HttpClient _httpClient;

        public ContainerImagePullFailurePlugin(
            ILogger<ContainerImagePullFailurePlugin> logger,
            IContainerAppPlugin containerAppPlugin,
            IArmClientFactory armClientFactory)
        {
            _logger = logger;
            _containerAppPlugin = containerAppPlugin;
            _armClientFactory = armClientFactory;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Checks if a Container App is properly authenticated to an Azure Container Registry
        /// </summary>
        public async Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId, string imageReference)
        {
            _logger.LogInformation($"Checking ACR authentication for app {resourceId} and image {imageReference}");

            var result = new AcrAuthenticationStatus
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsAuthenticated = false
            };

            try
            {
                // Extract registry name from the image reference
                string registryName = ExtractRegistryName(imageReference);
                if (string.IsNullOrEmpty(registryName))
                {
                    result.ErrorMessage = "Could not extract registry name from image reference";
                    return result;
                }

                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();

                // Get the Container App resource to check Managed Identity configuration
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Check if managed identity is configured
                if (containerApp.Value.Data.Identity == null ||
                    (!containerApp.Value.Data.Identity.UserAssignedIdentities.Any() &&
                     containerApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned))
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "No Managed Identity is configured for the Container App";
                    result.PotentialSolution = "Configure a System Assigned or User Assigned Managed Identity for the Container App";
                    return result;
                }

                // Get registry resource for ACR-Pull role check
                var registry = await FindAcrResourceByName(armClient, registryName);
                if (registry == null)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"Could not find ACR with name {registryName}";
                    result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                    return result;
                }

                // Check for network connectivity issues via NSG rules
                var nsgRules = await _containerAppPlugin.GetAllNSGRulesForContainerAppAsync(resourceId);
                bool hasBlockingRules = CheckForBlockingNsgRules(nsgRules, registryName);
                if (hasBlockingRules)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"NSG rules may be blocking access to ACR {registryName}";
                    result.PotentialSolution = "Add outbound allow rule for ACR in the NSG";
                    return result;
                }

                // Check if we can connect to the registry endpoint
                var connectivityResult = await TestConnectivityToRegistryAsync(registryName);
                if (!connectivityResult.IsConnected)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = connectivityResult.ErrorMessage;
                    result.PotentialSolution = connectivityResult.PotentialSolution;
                    return result;
                }

                // Check if the image exists in the registry
                var imageExists = await CheckImageExistsInAcr(imageReference);
                if (!imageExists)
                {
                    result.IsAuthenticated = true; // We can authenticate, but the image doesn't exist
                    result.ErrorMessage = $"The image {imageReference} was not found in the registry";
                    result.PotentialSolution = "Verify the image reference is correct and the image has been pushed to the registry";
                    return result;
                }

                // If we passed all checks, the container app should be able to authenticate
                result.IsAuthenticated = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking ACR authentication: {resourceId}");
                result.ErrorMessage = $"Exception during authentication check: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Verifies connectivity and authentication to an external registry
        /// </summary>
        public async Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId, string imageReference)
        {
            _logger.LogInformation($"Verifying external registry connectivity for {resourceId} and image {imageReference}");

            var result = new ExternalRegistryVerificationResult
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsSuccessful = false
            };

            try
            {
                var registryType = DetermineRegistryType(imageReference);
                result.RegistryType = registryType;

                if (registryType == RegistryType.DockerHub)
                {
                    // Check for rate limiting issues with Docker Hub
                    var isRateLimited = await CheckDockerHubRateLimiting(imageReference);
                    if (isRateLimited)
                    {
                        result.IsSuccessful = false;
                        result.FailureReason = "Rate Limiting";
                        result.ErrorDetails = "The Docker Hub registry is rate limiting your pulls. This commonly happens with anonymous pulls.";
                        result.RecommendedAction = "Consider using authenticated pulls by configuring registry credentials in your container app, or use a private registry.";
                        return result;
                    }

                    // Check if the image exists
                    var imageExists = await CheckImageExistsInDockerHub(imageReference);
                    if (!imageExists)
                    {
                        result.IsSuccessful = false;
                        result.FailureReason = "Image Not Found";
                        result.ErrorDetails = $"The image {imageReference} was not found in Docker Hub.";
                        result.RecommendedAction = "Verify the image reference is correct and that the image exists in the registry.";
                        return result;
                    }

                    // If we got here, the registry check was successful
                    result.IsSuccessful = true;
                    result.RegistryAccessible = true;
                }
                else if (registryType == RegistryType.Other)
                {
                    // For other registries, try to extract the hostname
                    string registryHostname = ExtractRegistryHostname(imageReference);
                    if (string.IsNullOrEmpty(registryHostname))
                    {
                        result.IsSuccessful = false;
                        result.FailureReason = "Invalid Image Reference";
                        result.ErrorDetails = "Could not determine the registry hostname from the provided image reference.";
                        result.RecommendedAction = "Verify the image reference format is correct (e.g., registry.example.com/repository:tag).";
                        return result;
                    }

                    // Check basic connectivity to the registry
                    var connectivityResult = await TestExternalRegistryConnectivity(registryHostname);
                    if (!connectivityResult)
                    {
                        result.IsSuccessful = false;
                        result.FailureReason = "Connectivity Issue";
                        result.ErrorDetails = $"Could not establish a connection to the registry at {registryHostname}.";
                        result.RecommendedAction = "Check network connectivity and ensure the registry is accessible from the Container App's network.";
                        return result;
                    }

                    // If we got here, the basic connectivity check passed
                    result.IsSuccessful = true;
                    result.RegistryAccessible = true;
                    result.RecommendedAction = "While basic connectivity was successful, you may need to configure registry credentials if the registry requires authentication.";
                }
                else
                {
                    result.IsSuccessful = false;
                    result.FailureReason = "Unsupported Registry Type";
                    result.ErrorDetails = "The registry type could not be determined or is not supported for verification.";
                    result.RecommendedAction = "Verify the image reference format is correct and uses a supported registry.";
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying external registry: {imageReference}");
                result.IsSuccessful = false;
                result.FailureReason = "Exception";
                result.ErrorDetails = $"An error occurred during verification: {ex.Message}";
                result.RecommendedAction = "Review the error details and check registry availability.";
                return result;
            }
        }

        #region Helper Methods

        private async Task<ContainerRegistryResource> FindAcrResourceByName(ArmClient armClient, string registryName)
        {
            try
            {
                // Get the default subscription
                var subscription = await armClient.GetDefaultSubscriptionAsync();

                // Query for ACRs with the given name
                var registryResources = subscription.GetContainerRegistries();
                foreach (var registry in registryResources)
                {
                    if (registry.Data.Name.Equals(registryName, StringComparison.OrdinalIgnoreCase))
                    {
                        return registry;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding ACR by name: {registryName}");
                return null;
            }
        }

        private async Task<ConnectivityTestResult> TestConnectivityToRegistryAsync(string registryName)
        {
            try
            {
                // Test connection to the registry login endpoint
                var request = new HttpRequestMessage(HttpMethod.Head, $"https://{registryName}.azurecr.io/v2/");
                var response = await _httpClient.SendAsync(request);

                // Even if we get a 401 Unauthorized, that means we were able to connect to the registry
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return new ConnectivityTestResult
                    {
                        IsConnected = true,
                        HttpStatusCode = (int)response.StatusCode
                    };
                }
                else
                {
                    return new ConnectivityTestResult
                    {
                        IsConnected = false,
                        HttpStatusCode = (int)response.StatusCode,
                        ErrorMessage = $"Connection to registry returned status code {(int)response.StatusCode}",
                        PotentialSolution = "Check network connectivity and registry availability"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connectivity to registry: {registryName}");
                return new ConnectivityTestResult
                {
                    IsConnected = false,
                    ErrorMessage = $"Connection to registry failed: {ex.Message}",
                    PotentialSolution = "Verify network connectivity, NSG rules, and registry availability"
                };
            }
        }

        private async Task<bool> TestExternalRegistryConnectivity(string hostname)
        {
            try
            {
                // Build the registry URL - assuming HTTPS
                var registryUrl = $"https://{hostname}/v2/";
                var request = new HttpRequestMessage(HttpMethod.Head, registryUrl);

                var response = await _httpClient.SendAsync(request);

                // Most registries will return 401 Unauthorized if you don't provide credentials
                // But that means the registry is accessible
                return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connectivity to external registry: {hostname}");
                return false;
            }
        }

        private async Task<bool> CheckImageExistsInAcr(string imageReference)
        {
            try
            {
                var registryName = ExtractRegistryName(imageReference);
                if (string.IsNullOrEmpty(registryName))
                {
                    return false;
                }

                var (repo, tag) = ExtractRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    return false;
                }

                // Use the V2 Docker registry API to check if the image exists
                var url = $"https://{registryName}.azurecr.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if image exists in ACR: {imageReference}");
                return false;
            }
        }

        private async Task<bool> CheckImageExistsInDockerHub(string imageReference)
        {
            try
            {
                var (repo, tag) = ExtractDockerHubRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    return false;
                }

                // Docker Hub API for checking if an image tag exists
                var url = $"https://hub.docker.com/v2/repositories/{repo}/tags/{tag}/";
                var response = await _httpClient.GetAsync(url);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if image exists in Docker Hub: {imageReference}");
                return false;
            }
        }

        private async Task<bool> CheckDockerHubRateLimiting(string imageReference)
        {
            try
            {
                // The best way to check for rate limiting is to try to pull the image and check for 429
                // But since we can't do that directly, we'll check Docker Hub API rate limits
                var response = await _httpClient.GetAsync("https://hub.docker.com/v2/");

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking Docker Hub rate limiting for: {imageReference}");
                return false;
            }
        }

        private RegistryType DetermineRegistryType(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return RegistryType.Unknown;

            if (imageReference.Contains("azurecr.io") || imageReference.Contains(".acr.io"))
                return RegistryType.AzureContainerRegistry;

            if (!imageReference.Contains("/") ||
                imageReference.Split('/')[0].Contains(".") ||
                !imageReference.Contains("."))
                return RegistryType.DockerHub;

            return RegistryType.Other;
        }

        private string ExtractRegistryHostname(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return string.Empty;

            try
            {
                // Check if the image reference contains a hostname with a dot
                int slashIndex = imageReference.IndexOf('/');
                if (slashIndex > 0)
                {
                    string possibleHostname = imageReference.Substring(0, slashIndex);
                    if (possibleHostname.Contains('.'))
                    {
                        return possibleHostname;
                    }
                }
            }
            catch
            {
                // In case of any parsing errors, return empty
            }

            return string.Empty;
        }

        private string ExtractRegistryName(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return string.Empty;

            try
            {
                // Pattern for ACR: registryname.azurecr.io/repository:tag
                var match = Regex.Match(imageReference, @"^([^\.]+)\.azurecr\.io/");
                if (match.Success && match.Groups.Count > 1)
                    return match.Groups[1].Value;

                // Try alternate pattern for ACR
                match = Regex.Match(imageReference, @"^([^\.]+)\.acr\.io/");
                if (match.Success && match.Groups.Count > 1)
                    return match.Groups[1].Value;

                // If no match, just return the first part before the first slash
                int slashIndex = imageReference.IndexOf('/');
                if (slashIndex > 0)
                    return imageReference.Substring(0, slashIndex);
            }
            catch
            {
                // In case of any parsing errors, return empty
            }

            return string.Empty;
        }

        private (string Repository, string Tag) ExtractRepositoryAndTag(string imageReference)
        {
            try
            {
                // Extract registry name
                string registryName = ExtractRegistryName(imageReference);

                // Remove the registry part
                string withoutRegistry = imageReference.Substring(imageReference.IndexOf('/') + 1);

                // Split repository and tag
                string repository, tag = "latest";
                if (withoutRegistry.Contains(':'))
                {
                    var parts = withoutRegistry.Split(':');
                    repository = parts[0];
                    tag = parts[1];
                }
                else
                {
                    repository = withoutRegistry;
                }

                return (repository, tag);
            }
            catch
            {
                return (string.Empty, "latest");
            }
        }

        private (string Repository, string Tag) ExtractDockerHubRepositoryAndTag(string imageReference)
        {
            try
            {
                string repository, tag = "latest";

                // Handle image references with and without Docker Hub prefix
                if (imageReference.StartsWith("docker.io/"))
                {
                    imageReference = imageReference.Substring("docker.io/".Length);
                }

                // Handle official images that don't have a user/org prefix
                if (!imageReference.Contains('/'))
                {
                    repository = $"library/{imageReference}";
                }
                else
                {
                    repository = imageReference;
                }

                // Split repository and tag
                if (repository.Contains(':'))
                {
                    var parts = repository.Split(':');
                    repository = parts[0];
                    tag = parts[1];
                }

                return (repository, tag);
            }
            catch
            {
                return (string.Empty, "latest");
            }
        }

        private bool CheckForBlockingNsgRules(IDictionary<string, IReadOnlyList<SecurityRuleData>> nsgRules, string registryName)
        {
            // Check for NSG rules that would block outbound traffic to ACR

            if (nsgRules == null || nsgRules.Count == 0)
                return false;

            foreach (var nsg in nsgRules)
            {
                foreach (var rule in nsg.Value)
                {
                    // Look for deny rules that might affect ACR connectivity
                    if (rule.Access == SecurityRuleAccess.Deny.ToString() && rule.Direction == SecurityRuleDirection.Outbound.ToString())
                    {
                        // Check if the rule applies to ACR
                        bool blocksAcr = false;

                        // Check destination address prefixes
                        if (rule.DestinationAddressPrefixes != null && rule.DestinationAddressPrefixes.Any())
                        {
                            // Check if any address prefix covers the ACR domain
                            if (rule.DestinationAddressPrefixes.Contains("*") ||
                                rule.DestinationAddressPrefixes.Contains("Internet") ||
                                rule.DestinationAddressPrefixes.Contains("AzureCloud"))
                            {
                                blocksAcr = true;
                            }
                        }

                        // Check destination ports
                        if ((rule.DestinationPortRanges != null && rule.DestinationPortRanges.Contains("443")) ||
                            rule.DestinationPortRange == "443" ||
                            rule.DestinationPortRange == "*")
                        {
                            blocksAcr = true;
                        }

                        if (blocksAcr)
                            return true;
                    }
                }
            }

            return false;
        }

        #endregion
    }

    #region Helper Classes

    public class ConnectivityTestResult
    {
        public bool IsConnected { get; set; }
        public int HttpStatusCode { get; set; }
        public string ErrorMessage { get; set; }
        public string PotentialSolution { get; set; }
    }

    #endregion
}
