// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Agent.Plugins.Implementation
{
    public class ContainerImagePullFailurePlugin : IContainerImagePullFailurePlugin
    {
        private readonly ILogger<ContainerImagePullFailurePlugin> _logger;
        private readonly IArmClientFactory _armClientFactory;
        private readonly HttpClient _httpClient;
        private readonly IAuthenticationService _authService;

        public ContainerImagePullFailurePlugin(
            ILogger<ContainerImagePullFailurePlugin> logger,
            IContainerAppPlugin containerAppPlugin,
            IArmClientFactory armClientFactory,
            IAuthenticationService authService)
        {
            _logger = logger;
            _armClientFactory = armClientFactory;
            _httpClient = new HttpClient();
            _authService = authService;
        }

        /// <summary>
        /// Gets image reference from a resource ID (Container App or Linux Web App)
        /// </summary>
        /// <param name="resourceId">The resource ID of a Container App or Linux Web App</param>
        /// <returns>Image reference string if found, null otherwise</returns>
        public async Task<string> GetImageReferenceFromResourceId(string resourceId)
        {
            _logger.LogInformation($"Getting image reference for resource: {resourceId}");
            var armClient = _armClientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            try
            {
                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    return await GetContainerAppImageReference(armClient, resourceIdentifier);
                }
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    return await GetLinuxWebAppImageReference(armClient, resourceIdentifier);
                }
                else
                {
                    _logger.LogWarning($"Resource type {resourceIdentifier.ResourceType} is not supported for getting image reference.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting image reference for resource {resourceId}");
                return null;
            }
        }

        // Extracted helper for Container App image reference
        private async Task<string> GetContainerAppImageReference(ArmClient armClient, ResourceIdentifier resourceId)
        {
            var containerAppResource = armClient.GetContainerAppResource(resourceId);
            var containerApp = await containerAppResource.GetAsync();
            string latestRevisionName = containerApp.Value.Data.LatestRevisionName;

            // If we have a latest revision name, get that revision specifically
            if (!string.IsNullOrEmpty(latestRevisionName))
            {
                string revisionResourceId = $"{resourceId}/revisions/{latestRevisionName}";
                try
                {
                    var revisionResource = armClient.GetContainerAppRevisionResource(new ResourceIdentifier(revisionResourceId));
                    var revision = await revisionResource.GetAsync();

                    // Get the container image from the revision
                    if (revision.Value.Data.Template?.Containers != null &&
                        revision.Value.Data.Template.Containers.Count > 0)
                    {
                        return revision.Value.Data.Template.Containers[0].Image;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Could not retrieve latest revision {latestRevisionName} for app {resourceId}, falling back to template");
                }
            }
            // Fall back to the template if available
            if (containerApp.Value.Data.Template?.Containers != null &&
                        containerApp.Value.Data.Template.Containers.Count > 0)
            {
                return containerApp.Value.Data.Template.Containers[0].Image;
            }

            return null;
        }

        // Extracted helper for Linux Web App image reference
        private async Task<string> GetLinuxWebAppImageReference(ArmClient armClient, ResourceIdentifier resourceId)
        {
            var webAppResource = armClient.GetWebSiteResource(resourceId);
            var webApp = await webAppResource.GetAsync();
            var siteConfig = webApp.Value.Data.SiteConfig;

            if (siteConfig?.LinuxFxVersion != null)
            {
                _logger.LogInformation($"Found LinuxFxVersion: {siteConfig.LinuxFxVersion}");

                if (siteConfig.LinuxFxVersion.StartsWith("DOCKER|", StringComparison.OrdinalIgnoreCase))
                {
                    return siteConfig.LinuxFxVersion.Substring("DOCKER|".Length);
                }
                // Add handling for other LinuxFxVersion formats if necessary (e.g., COMPOSE)
            }

            // Fallback: Ty siteContainers
            var containers = await webApp.Value.GetSiteContainers().ToListAsync();
            if (containers.Count > 0)
            {
                var containerImage = containers[0].Data.Image;
                if (!string.IsNullOrEmpty(containerImage))
                {
                    return containerImage;
                }
            }

            _logger.LogWarning($"Could not determine image reference from LinuxFxVersion for Web App {resourceId}");
            return null;
        }


        /// <summary>
        /// Gets network security group rules for a resource using direct ARM calls
        /// </summary>
        /// <param name="resourceId">The resource ID of a Container App or Linux Web App</param>
        /// <returns>Dictionary of NSG names and their security rules</returns>
        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNetworkSecurityRulesForResource(string resourceId)
        {
            _logger.LogInformation($"Getting network security rules for resource: {resourceId}");
            var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>();
            var armClient = _armClientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            try
            {
                ResourceIdentifier subnetId = null;

                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    subnetId = await GetContainerAppSubnetId(armClient, resourceIdentifier);
                }
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    subnetId = await GetWebAppSubnetId(armClient, resourceIdentifier);
                }
                else
                {
                    _logger.LogWarning($"Resource type {resourceIdentifier.ResourceType} is not supported for getting NSG rules.");
                    return result;
                }

                if (subnetId != null)
                {
                    var subnetResource = armClient.GetSubnetResource(subnetId);
                    var subnetData = (await subnetResource.GetAsync()).Value.Data;

                    if (subnetData.NetworkSecurityGroup?.Id != null)
                    {
                        var nsgId = subnetData.NetworkSecurityGroup.Id;
                        var nsgResource = armClient.GetNetworkSecurityGroupResource(nsgId);
                        var nsgData = (await nsgResource.GetAsync()).Value.Data;
                        if (nsgData?.SecurityRules != null)
                        {
                            _logger.LogInformation($"Found NSG {nsgData.Name} with {nsgData.SecurityRules.Count} rules for subnet {subnetData.Name}");
                            result.Add(nsgData.Name, nsgData.SecurityRules.ToList());
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Subnet {subnetId.Name} is not associated with an NSG.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting network security rules for resource {resourceId}");
            }
            return result;
        }

        // Extracted helper for Container App subnet ID
        private async Task<ResourceIdentifier> GetContainerAppSubnetId(ArmClient armClient, ResourceIdentifier resourceId)
        {
            var containerAppResource = armClient.GetContainerAppResource(resourceId);
            var containerApp = await containerAppResource.GetAsync();
            var environmentId = containerApp.Value.Data.EnvironmentId;

            if (environmentId == null)
            {
                _logger.LogWarning($"Container App {resourceId} does not have a managed environment ID.");
                return null;
            }

            var environmentResource = armClient.GetContainerAppManagedEnvironmentResource(environmentId);
            var environment = await environmentResource.GetAsync();
            var vnetConfiguration = environment.Value.Data.VnetConfiguration;

            if (vnetConfiguration == null)
            {
                _logger.LogInformation($"Container App Environment {environmentId.Name} is not VNet integrated.");
                return null;
            }

            var infrastructureSubnetId = vnetConfiguration.InfrastructureSubnetId;
            if (string.IsNullOrEmpty(infrastructureSubnetId))
            {
                _logger.LogWarning($"Container App Environment {environmentId.Name} VNet configuration does not have an infrastructure subnet ID.");
                return null;
            }

            return new ResourceIdentifier(infrastructureSubnetId);
        }

        // Extracted helper for Web App subnet ID
        private async Task<ResourceIdentifier> GetWebAppSubnetId(ArmClient armClient, ResourceIdentifier resourceId)
        {
            var webAppResource = armClient.GetWebSiteResource(resourceId);
            // First, try to get the subnet ID from the SiteConfig
            var webApp = await webAppResource.GetAsync();
            string subnetId = webApp.Value.Data.VirtualNetworkSubnetId?.ToString();

            if (!string.IsNullOrEmpty(subnetId))
            {
                return new ResourceIdentifier(subnetId);
            }

            // If not found, check the virtual network connections (use the property 'SubnetId' if available)
            var vnetConnections = webAppResource.GetSiteVirtualNetworkConnections();
            await foreach (var vnetInfo in vnetConnections)
            {
                if (vnetInfo?.Data is AppServiceVirtualNetworkData vnetData)
                {
                    if (!string.IsNullOrEmpty(vnetData.VnetResourceId))
                    {
                        return new ResourceIdentifier(vnetData.VnetResourceId);
                    }
                }
            }

            _logger.LogInformation($"Web App {resourceId} does not appear to be VNet integrated or subnet information is missing.");
            return null;
        }


        /// <summary>
        /// Checks if a Container App is properly authenticated to an Azure Container Registry
        /// </summary>
        public async Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId)
        {
           _logger.LogInformation($"Checking ACR authentication for app {resourceId}");

           string imageReference = await GetImageReferenceFromResourceId(resourceId);
           var result = new AcrAuthenticationStatus
           {
               ResourceId = resourceId,
               ImageReference = imageReference,
               IsAuthenticated = false // Default to false
           };

           if (string.IsNullOrEmpty(imageReference))
           {
               result.ErrorMessage = "Could not determine image reference from the resource";
               _logger.LogWarning(result.ErrorMessage);
               return result;
           }
            try
            {
               string registryName = ExtractRegistryName(imageReference);
               if (string.IsNullOrEmpty(registryName))
               {
                   result.ErrorMessage = "Could not extract registry name from image reference";
                   _logger.LogWarning(result.ErrorMessage);
                   return result;
               }

                // Only proceed if it's an ACR image
                if (!imageReference.Contains(".azurecr.io/", StringComparison.OrdinalIgnoreCase) &&
                    !imageReference.Contains(".acr.io/", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(result.ErrorMessage);
                    result.ErrorMessage = "Image is not from Azure Container Registry. Use verify_external_registry tool for non-ACR images.";
                    result.PotentialSolution = "For non-ACR images, configure registry credentials in the Container App settings.";
                    return result;
                }
               var armClient = _armClientFactory.GetArmClient();
               var resourceIdentifier = new ResourceIdentifier(resourceId);

                // Check if the image exists in the registry
                var imageExists = await CheckImageExistsInAcr(imageReference);
                if (!imageExists)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"The image {imageReference} was not found in the registry";
                    result.PotentialSolution = "Verify the image reference is correct and the image has been pushed to the registry";
                    _logger.LogWarning(result.ErrorMessage);
                }

               if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
               {
                   result = await CheckContainerAppAcrAuth(armClient, resourceId, registryName, imageReference);
               }
               else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
               {
                   result = await CheckWebAppAcrAuth(armClient, resourceId, registryName, imageReference);
               }
               else
               {
                   result.ErrorMessage = $"Resource type {resourceIdentifier.ResourceType} is not supported for ACR authentication check.";
                   _logger.LogWarning(result.ErrorMessage);
               }
                // Perform common checks only if authentication hasn't already failed definitively
                if (result.IsAuthenticated) // Check connectivity if auth seems okay so far
                {
                    // Check for network connectivity issues via NSG rules
                    var nsgRules = await GetNetworkSecurityRulesForResource(resourceId);
                    bool hasBlockingRules = CheckForBlockingNsgRules(nsgRules, registryName);
                    if (hasBlockingRules)
                    {
                        result.IsAuthenticated = false; // Downgrade status due to potential network block
                        result.ErrorMessage = $"NSG rules may be blocking access to ACR {registryName}";
                        result.PotentialSolution = "Add outbound allow rule for ACR in the NSG";
                        _logger.LogWarning(result.ErrorMessage);
                        return result;
                    }

                    // Check if we can connect to the registry endpoint
                    var connectivityResult = await TestConnectivityToRegistryAsync(registryName);
                    if (!connectivityResult.IsConnected)
                    {
                        result.IsAuthenticated = false; // Downgrade status due to connectivity failure
                        result.ErrorMessage = connectivityResult.ErrorMessage;
                        result.PotentialSolution = connectivityResult.PotentialSolution;
                        _logger.LogWarning($"Connectivity test failed: {result.ErrorMessage}");
                        return result;
                    }
                }
            }
           catch (Exception ex)
           {
               _logger.LogError(ex, $"Error checking ACR authentication for resource {resourceId}");
               result.IsAuthenticated = false;
               result.ErrorMessage = $"An unexpected error occurred: {ex.Message}";
           }
           return result;
        }

        // Method to handle Container App ACR authentication check
        private async Task<AcrAuthenticationStatus> CheckContainerAppAcrAuth(
            ArmClient armClient,
            string resourceId,
            string registryName,
            string imageReference)
        {
            var result = new AcrAuthenticationStatus
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsAuthenticated = false // Default to false
            };

            var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
            Response<ContainerAppResource> containerAppResponse = await containerAppResource.GetAsync();
            if (!containerAppResponse.HasValue)
            {
                result.ErrorMessage = "Could not retrieve Container App resource details.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }
            var containerApp = containerAppResponse.Value;

            var registryConfiguration = containerApp.Data.Configuration?.Registries;
            var identity = containerApp.Data.Identity;

            // Find specific registry config
            var registryConfig = registryConfiguration?.FirstOrDefault(r =>
                r.Server != null && r.Server.Contains(registryName, StringComparison.OrdinalIgnoreCase));

            if (registryConfig != null)
            {
                // Case 1: Explicit configuration for this registry exists
                return await CheckContainerAppExplicitRegistryAuth(armClient, registryConfig, identity, registryName, result);
            }
            else
            {
                // Case 2: No explicit configuration, check default managed identity
                return await CheckContainerAppDefaultManagedIdentityAuth(armClient, identity, registryName, result);
            }
        }

        // Helper for Container App Explicit Registry Auth Check
        private async Task<AcrAuthenticationStatus> CheckContainerAppExplicitRegistryAuth(
            ArmClient armClient,
            ContainerAppRegistryCredentials registryConfig,
            ManagedServiceIdentity identity,
            string registryName,
            AcrAuthenticationStatus result)
        {
            // Check if using managed identity for this registry
            if (!string.IsNullOrEmpty(registryConfig.Identity))
            {
                _logger.LogInformation($"Container App is configured to use managed identity '{registryConfig.Identity}' for ACR {registryName}");
                return await CheckContainerAppManagedIdentityRole(armClient, identity, registryConfig.Identity, registryName, result);
            }
            // Check if using username/password auth
            else if (!string.IsNullOrEmpty(registryConfig.Username) && !string.IsNullOrEmpty(registryConfig.PasswordSecretRef))
            {
                _logger.LogInformation($"Container App is configured to use username/password authentication for ACR {registryName}");
                result.IsAuthenticated = true; // Assume configured correctly, cannot verify password
                result.ErrorMessage = "Container App is using username/password authentication for ACR. Cannot verify if credentials are correct.";
                result.PotentialSolution = "For improved security, consider using managed identity authentication instead of username/password.";
                return result;
            }
            else
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "Container App has registry configuration for ACR, but no valid authentication method (Managed Identity or Username/Password) is specified.";
                result.PotentialSolution = "Configure either managed identity or username/password authentication for the registry in the Container App settings.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }
        }

        // Helper for Container App Managed Identity Role Check (used by explicit and default checks)
        private async Task<AcrAuthenticationStatus> CheckContainerAppManagedIdentityRole(
            ArmClient armClient,
            ManagedServiceIdentity identity,
            string identityName, // Can be "system", a user identity name/ID, or null for default check
            string registryName,
            AcrAuthenticationStatus result)
        {
            var registry = await FindAcrResourceByName(armClient, result.ResourceId, registryName);
            if (registry == null)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = $"Could not find ACR with name {registryName}";
                result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            bool isSystemIdentityCheck = identityName?.Equals("system", StringComparison.OrdinalIgnoreCase) ?? false;
            bool isUserIdentityCheck = !isSystemIdentityCheck && !string.IsNullOrEmpty(identityName);

            if (identity == null ||
                (isSystemIdentityCheck && identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned && identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned) ||
                (isUserIdentityCheck && identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned && identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned) ||
                (identity.ManagedServiceIdentityType == ManagedServiceIdentityType.None))
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = $"Managed Identity ('{(isSystemIdentityCheck ? "system" : identityName ?? "default")}') is specified for ACR auth, but the required identity type is not enabled on the Container App.";
                result.PotentialSolution = $"Enable the required Managed Identity type (System or User-Assigned '{identityName}') on the Container App.";
                 _logger.LogWarning(result.ErrorMessage);
               return result;
            }

            bool hasProperRoleAssignment = false;
            if (isSystemIdentityCheck)
            {
                 if (!identity.PrincipalId.HasValue)
                 {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "Container App is configured to use system-assigned identity for ACR, but system-assigned identity is not enabled";
                    result.PotentialSolution = "Enable system-assigned managed identity for the Container App";
                    _logger.LogWarning(result.ErrorMessage);
                     return result;
                 }
                 hasProperRoleAssignment = await CheckSpecificUserAssignedIdentityRoleAsync(armClient, identity.PrincipalId.Value, registry.Id);
            }
            else if (isUserIdentityCheck)
            {
                var userAssignedIdentity = identity.UserAssignedIdentities?.FirstOrDefault(kvp =>
                    kvp.Key.ToString().EndsWith($"/{identityName}", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.ToString().Equals(identityName, StringComparison.OrdinalIgnoreCase)); // Handle full ID or just name

                if (userAssignedIdentity.Equals(default(KeyValuePair<ResourceIdentifier, UserAssignedIdentity>)) ||
                    !userAssignedIdentity.Value.Value.PrincipalId.HasValue)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"User-assigned identity '{identityName}' is configured but not found or lacks a Principal ID on the Container App.";
                    result.PotentialSolution = $"Ensure the user-assigned identity '{identityName}' is correctly assigned to the Container App.";
                    _logger.LogWarning(result.ErrorMessage);
                    return result;
                }

                hasProperRoleAssignment = await CheckSpecificUserAssignedIdentityRoleAsync(
                    armClient,
                    userAssignedIdentity.Value.Value.PrincipalId.Value,
                    registry.Id);

            }
            else // Default check (no specific identity named)
            {
                 hasProperRoleAssignment = await CheckManagedIdentityRoleAssignmentAsync(armClient, identity, registry.Id, false); // Check all identities
            }


            if (!hasProperRoleAssignment)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = $"The configured Managed Identity ('{(isSystemIdentityCheck ? "system" : identityName ?? "any")}') does not have the required 'AcrPull' (or equivalent) role on the registry '{registryName}'.";
                result.PotentialSolution = $"Assign the 'AcrPull' role to the Managed Identity ('{(isSystemIdentityCheck ? "system" : identityName ?? "the appropriate one")}') on the ACR resource '{registryName}'.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // If role assignment is correct
            result.IsAuthenticated = true;
            _logger.LogInformation($"Managed Identity ('{(isSystemIdentityCheck ? "system" : identityName ?? "default")}') has appropriate role assignment on ACR {registryName}.");
            return result;
        }

         // Helper for Container App Default Managed Identity Auth Check
        private async Task<AcrAuthenticationStatus> CheckContainerAppDefaultManagedIdentityAuth(
            ArmClient armClient,
            ManagedServiceIdentity identity,
            string registryName,
            AcrAuthenticationStatus result)
        {
            _logger.LogInformation($"No explicit registry configuration found for ACR {registryName}. Checking for default managed identity configuration.");

            if (identity == null ||
                (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned &&
                 identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned &&
                 identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned))
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "No explicit registry configuration for ACR and no Managed Identity (System or User-Assigned) is configured for the Container App.";
                result.PotentialSolution = "Configure registry authentication (Managed Identity or Username/Password) in the Container App settings or enable a Managed Identity.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // Check if *any* configured MI has the required role
            return await CheckContainerAppManagedIdentityRole(armClient, identity, null, registryName, result); // Pass null identityName for default check
        }


        // Method to handle Web App ACR authentication check
        private async Task<AcrAuthenticationStatus> CheckWebAppAcrAuth(
            ArmClient armClient,
            string resourceId,
            string registryName,
            string imageReference)
        {
            var result = new AcrAuthenticationStatus
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsAuthenticated = false // Default to false
            };

            var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            Response<WebSiteResource> webAppResponse = await webAppResource.GetAsync();
             if (!webAppResponse.HasValue)
            {
                result.ErrorMessage = "Could not retrieve Web App resource details.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }
            var webApp = webAppResponse.Value;

            Response<AppServiceConfigurationDictionary> appSettingsResponse = await webAppResource.GetApplicationSettingsAsync();
             if (!appSettingsResponse.HasValue)
            {
                result.ErrorMessage = "Could not retrieve Web App application settings.";
                 _logger.LogWarning(result.ErrorMessage);
               return result;
            }
            var appSettings = appSettingsResponse.Value.Properties;

            // Case 1: Check for explicit username/password credentials in App Settings
            if (appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_URL", out string registryUrl) &&
                registryUrl?.Contains(registryName, StringComparison.OrdinalIgnoreCase) == true &&
                appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_USERNAME", out string username) && !string.IsNullOrEmpty(username) &&
                appSettings.ContainsKey("DOCKER_REGISTRY_SERVER_PASSWORD")) // Check if password key exists (value is hidden)
            {
                _logger.LogInformation($"Web App is configured to use username/password authentication via App Settings for ACR {registryName}");
                result.IsAuthenticated = true; // Assume configured correctly, cannot verify password
                result.ErrorMessage = "Web App is using username/password authentication via App Settings for ACR. Cannot verify if credentials are correct.";
                result.PotentialSolution = "For improved security and manageability, consider using managed identity authentication instead of storing credentials in App Settings.";
                return result;
            }

            // Case 2: Check for Managed Identity configuration if explicit credentials aren't fully set
            _logger.LogInformation($"No complete username/password configuration found in App Settings for {registryName}. Checking managed identity configuration.");

            var identity = webApp.Data.Identity;
            if (identity == null ||
                (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned &&
                 identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned &&
                 identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned))
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "No valid registry credentials found in App Settings and no Managed Identity (System or User-Assigned) is enabled for the Web App.";
                result.PotentialSolution = "Configure registry credentials in App Settings (DOCKER_REGISTRY_SERVER_...) or enable Managed Identity and grant it the 'AcrPull' role on the registry.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // Find the ACR resource
            var registry = await FindAcrResourceByName(armClient, resourceId, registryName);
            if (registry == null)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = $"Could not find ACR with name {registryName}";
                result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                 _logger.LogWarning(result.ErrorMessage);
               return result;
            }

            // Check if any enabled managed identity has the AcrPull role
            bool hasProperRoleAssignment = await CheckManagedIdentityRoleAssignmentAsync(armClient, identity, registry.Id, false); // Check all identities

            if (!hasProperRoleAssignment)
            {
                result.IsAuthenticated = false;
                result.ErrorMessage = "No configured Managed Identity (System or User-Assigned) has the required 'AcrPull' (or equivalent) role on the registry.";
                result.PotentialSolution = "Assign the 'AcrPull' role to either the System-Assigned or a User-Assigned Managed Identity on the ACR resource and ensure the identity is enabled on the Web App.";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // If role assignment is correct
            result.IsAuthenticated = true;
             _logger.LogInformation($"A Managed Identity configured on the Web App has appropriate role assignment on ACR {registryName}.");
            return result;
        }

        /// <summary>
        /// Verifies connectivity and authentication to an external registry
        /// </summary>
        public async Task<ExternalRegistryVerificationResult> VerifyExternalRegistry(string resourceId)
        {
            // Get the image reference from the resource ID
            string imageReference = await GetImageReferenceFromResourceId(resourceId);

            _logger.LogInformation($"Verifying external registry connectivity for {resourceId} and image {imageReference}");
            return await VerifyExternalRegistryAsync(imageReference, resourceId);
        }

        private async Task<ExternalRegistryVerificationResult> VerifyExternalRegistryAsync(string imageReference, string resourceId)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ImageReference = imageReference,
                ResourceId = resourceId,
                IsSuccessful = false
            };

            try
            {
                var registryType = DetermineRegistryType(imageReference);
                result.RegistryType = registryType;

                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    result.FailureReason = "Invalid Registry Reference";
                    result.ErrorDetails = "Could not determine registry hostname from image reference";
                    return result;
                }

                // Check basic connectivity first
                var connectivityResult = await TestExternalRegistryConnectivity(registryHostname);
                if (!connectivityResult)
                {
                    result.FailureReason = "Connectivity Issue";
                    result.ErrorDetails = $"Could not establish connection to registry at {registryHostname}";
                    result.RecommendedAction = "Check network connectivity and registry availability";
                    return result;
                }

                // Check for registry-specific issues
                switch (registryType)
                {
                    case RegistryType.DockerHub:
                        return await VerifyDockerHubRegistry(imageReference, resourceId);

                    case RegistryType.MicrosoftContainerRegistry:
                        return await VerifyMicrosoftContainerRegistry(imageReference, resourceId);

                    case RegistryType.GoogleContainerRegistry:
                        return await VerifyGoogleContainerRegistry(imageReference, resourceId);

                    case RegistryType.KubernetesRegistry:
                        // Kubernetes registry is generally public and doesn't require authentication
                        result.IsSuccessful = true;
                        result.RegistryAccessible = true;
                        return result;

                    case RegistryType.PrivateRegistry:
                        return await VerifyPrivateRegistry(imageReference, resourceId);

                    default:
                        result.FailureReason = "Unsupported Registry";
                        result.ErrorDetails = $"Registry type {registryType} is not fully supported for verification";
                        result.RecommendedAction = "Manual verification may be required";
                        return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying external registry for {imageReference}");
                result.FailureReason = "Verification Error";
                result.ErrorDetails = ex.Message;
                result.RecommendedAction = "Check registry configuration and try again";
                return result;
            }
        }

        private async Task<ExternalRegistryVerificationResult> VerifyDockerHubRegistry(string imageReference, string resourceId)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ImageReference = imageReference,
                ResourceId = resourceId,
                RegistryType = RegistryType.DockerHub,
                IsSuccessful = false
            };

            try
            {
                // Verify the image exists
                var (repo, tag) = ExtractDockerHubRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    result.FailureReason = "Invalid Reference";
                    result.ErrorDetails = "Could not parse repository information";
                    return result;
                }

                // Check if we can access the image manifest
                var token = await GetDockerOAuthTokenAsync(repo);
                var manifestUrl = $"https://registry-1.docker.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (response.Headers.Contains("X-RateLimit-Remaining"))
                    {
                        var rateLimitRemaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                        var rateLimitLimit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
                        var rateLimitReset = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();

                        _logger.LogInformation($"Rate Limit Remaining: {rateLimitRemaining}/{rateLimitLimit}, Reset Time: {rateLimitReset}");

                        // If remaining requests are 0, handle rate limiting  
                        if (int.TryParse(rateLimitRemaining, out int remaining) && remaining == 0)
                        {
                            _logger.LogWarning("Rate limit exceeded. Please wait until the limit resets.");
                            result.IsSuccessful = false;
                            result.RegistryAccessible = false;
                            result.FailureReason = "Rate Limited";
                            result.ErrorDetails = "Docker Hub is rate limiting image pulls";
                            result.RecommendedAction = "Configure authentication or wait for rate limit reset";
                            return result;
                        }
                    }

                    // Fallback to Retry-After logic if needed  
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfter = values.FirstOrDefault();
                        if (retryAfter != null && int.TryParse(retryAfter, out int seconds))
                        {
                            _logger.LogWarning($"Rate limit exceeded. Retry after {seconds} seconds.");
                            result.IsSuccessful = false;
                            result.RegistryAccessible = false;
                            result.FailureReason = "Rate Limited";
                            result.ErrorDetails = "Docker Hub is rate limiting image pulls";
                            result.RecommendedAction = $"Configure authentication or retry after {seconds} seconds.";
                            return result;
                        }
                    }
                }

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    result.IsSuccessful = true;
                    result.RegistryAccessible = true;
                    return result;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    result.FailureReason = "Image Not Found";
                    result.ErrorDetails = $"Image {repo}:{tag} not found in Docker Hub";
                    result.RecommendedAction = "Verify image name and tag are correct";
                    return result;
                }

                result.FailureReason = "Verification Failed";
                result.ErrorDetails = $"Unexpected response: {response.StatusCode}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Docker Hub registry for {imageReference}");
                result.FailureReason = "Verification Error";
                result.ErrorDetails = ex.Message;
                return result;
            }
        }

        private async Task<string> GetDockerOAuthTokenAsync(string repo)
        {
            var authUrl = "https://auth.docker.io/token";
            var authRequest = new HttpRequestMessage(HttpMethod.Get, $"{authUrl}?service=registry.docker.io&scope=repository:{repo}:pull");

            var authResponse = await _httpClient.SendAsync(authRequest);

            if (authResponse.IsSuccessStatusCode)
            {
                var authResponseBody = await authResponse.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<JsonElement>(authResponseBody).GetProperty("token").GetString();
                return token;
            }

            return null;
        }

        private async Task<ExternalRegistryVerificationResult> VerifyPrivateRegistry(string imageReference, string resourceId)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ImageReference = imageReference,
                ResourceId = resourceId,
                RegistryType = RegistryType.PrivateRegistry,
                IsSuccessful = false
            };

            try
            {
                // Extract registry details
                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    result.FailureReason = "Invalid Registry";
                    result.ErrorDetails = "Could not determine registry hostname";
                    return result;
                }

                // Check basic connectivity
                var isAccessible = await TestExternalRegistryConnectivity(registryHostname);
                if (!isAccessible)
                {
                    result.FailureReason = "Connectivity Issue";
                    result.ErrorDetails = $"Cannot connect to registry at {registryHostname}";
                    result.RecommendedAction = "Verify network connectivity and registry availability";
                    return result;
                }

                // For private registries, we can only verify basic connectivity
                // since authentication mechanisms vary
                result.IsSuccessful = true;
                result.RegistryAccessible = true;
                result.RecommendedAction = "Registry is accessible, but you may need to configure authentication";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying private registry for {imageReference}");
                result.FailureReason = "Verification Error";
                result.ErrorDetails = ex.Message;
                return result;
            }
        }

        public async Task<ImagePullingResult> CheckImagePulling(string resourceId)
        {
            _logger.LogInformation($"Checking Image pulling for resource: {resourceId}");
            try
            {
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);

                // Get image reference first to confirm we're dealing with a container resource
                var imageReference = await GetImageReferenceFromResourceId(resourceId);
                if (string.IsNullOrEmpty(imageReference))
                {
                    return new ImagePullingResult
                    {
                        IsSuccessful = false,
                        FailureReason = "Could not determine container image reference"
                    };
                }

                // Handle Container Apps
                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        new ResourceIdentifier($"/subscriptions/{resourceIdentifier.SubscriptionId}/resourceGroups/{resourceIdentifier.ResourceGroupName}"));

                    var containerApp = (await resourceGroup.GetContainerAppAsync(resourceIdentifier.Name)).Value;
                    if (containerApp == null)
                    {
                        return new ImagePullingResult
                        {
                            IsSuccessful = false,
                            FailureReason = "Container App not found"
                        };
                    }

                    var managedEnvResource = (await armClient.GetContainerAppManagedEnvironmentResource(
                        new ResourceIdentifier(containerApp.Data.EnvironmentId)).GetAsync()).Value;

                    // Check for image pull errors in the logs
                    var logAnalyticsCustomerId = managedEnvResource.Data.AppLogsConfiguration?.LogAnalyticsConfiguration?.CustomerId;
                    if (!string.IsNullOrEmpty(logAnalyticsCustomerId))
                    {
                        string query =
                         $@"
                        ContainerAppSystemLogs_CL 
                        | where ContainerAppName_s == '{containerApp.Id.Name}'
                        | where Reason_s == 'ContainerTerminated'
                        | where Log_s has_any ('ImagePullBackOff', 'ErrImagePull','ImagePullFailure')
                        | top 1 by TimeGenerated desc
                        | project Log_s
                        ";

                        var credential = _authService.GetArmOperationCredential();
                        var logsClient = new LogsQueryClient(credential);
                        var timespan = TimeSpan.FromMinutes(30);
                        var result = (await logsClient.QueryWorkspaceAsync(logAnalyticsCustomerId, query, new QueryTimeRange(timespan))).Value;
                        var imagePullingResult = ExtractPullingResultFromTable(result.Table, "Log_s");
                        return imagePullingResult;
                    }
                    return new ImagePullingResult
                    {
                        IsSuccessful = false,
                        FailureReason = "Log Analytics configuration not found",
                    };
                }
                // Handle Linux Web Apps
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var webApp = await webAppResource.GetAsync();

                    //if image reference is ACR, use a custom method
                    if (imageReference.Contains("azurecr.io", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ImagePullingResult
                        {
                            IsSuccessful = false,
                            FailureReason = "Image reference is ACR. Use IsAzureContainerRegistryImageAccessibleAsync tool to check image accessibility."
                        };
                    }

                    var zipFailures = await CheckContainerLogsZipAsync(webApp);
                    if (zipFailures.Any())
                    {
                        return new ImagePullingResult
                        {
                            IsSuccessful = false,
                            FailureReason = zipFailures.First(),
                        };
                    }

                    var instanceFailures = await CheckInstanceContainerLogsAsync(webApp);
                    if (instanceFailures.Any())
                    {
                        return new ImagePullingResult
                        {
                            IsSuccessful = false,
                            FailureReason = instanceFailures.First(),
                        };
                    }
                }

                // If we haven't found any pull failures, assume success
                _logger.LogInformation("No image pull failures detected in either log source.");
                return new ImagePullingResult { IsSuccessful = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking image pulling status for {resourceId}");
                return new ImagePullingResult
                {
                    IsSuccessful = false,
                    FailureReason = $"Error checking pull status: {ex.Message}"
                };
            }
        }

        public async Task<ImagePullingResult> IsAzureContainerRegistryImageAccessibleAsync(string resourceId)
        {
            try
            {
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                var imageReference = await GetImageReferenceFromResourceId(resourceId);
                if (string.IsNullOrEmpty(imageReference))
                {
                    return new ImagePullingResult
                    {
                        IsSuccessful = false,
                        FailureReason = "Could not determine container image reference"
                    };
                }
                var registryName = ExtractRegistryHostname(imageReference);
                var (repository, tag) = ExtractRepositoryAndTag(imageReference);
                string scope = $"https://{registryName}";

                var tokenRequestContext = new TokenRequestContext(new[] { $"{scope}/.default" });
                var credential = _authService.GetArmOperationCredential();
                var accessToken = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                string manifestUrl = $"https://{registryName}/v2/{repository}/manifests/{tag}";

                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return new ImagePullingResult
                    {
                        IsSuccessful = true,
                        FailureReason = "Image manifest is accessible"
                    };
                }

                return new ImagePullingResult
                {
                    IsSuccessful = false,
                    FailureReason = $"Failed to access image manifest: {response.StatusCode} - {response.ReasonPhrase}"
                };
            }
            catch (Exception ex)
            {
                return new ImagePullingResult
                {
                    IsSuccessful = false,
                    FailureReason = $"Error checking image manifest accessibility: {ex.Message}"
                };
            }
        }

        private async Task<List<string>> CheckContainerLogsZipAsync(WebSiteResource webApp)
        {
            var pullFailuresFromZip = new List<string>();

            try
            {
                var zipResponse = await webApp.GetContainerLogsZipAsync();

                if (zipResponse?.Value != null)
                {
                    using var zipStream = zipResponse.Value;
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                        {
                            using var logReader = new StreamReader(entry.Open());
                            string line;
                            while ((line = await logReader.ReadLineAsync()) != null)
                            {
                                if (line.Contains("Failed to pull image", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("Container pull image failed", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("ImagePullFailure", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("ImagePullBackOff", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("ErrImagePull", StringComparison.OrdinalIgnoreCase))
                                {
                                    pullFailuresFromZip.Add($"[ZIP Log] {line}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error reading ZIP logs: {ex.Message}");
            }

            return pullFailuresFromZip;
        }

        private async Task<List<string>> CheckInstanceContainerLogsAsync(WebSiteResource webApp)
        {
            var pullFailuresFromInstance = new List<string>();

            try
            {
                var instanceLogs = await webApp.GetWebSiteContainerLogsAsync();

                if (instanceLogs?.Value != null)
                {
                    using var reader = new StreamReader(instanceLogs.Value);
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("Failed to pull image", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Container pull image failed", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("ImagePullFailure", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("ImagePullBackOff", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("ErrImagePull", StringComparison.OrdinalIgnoreCase))
                        {
                            pullFailuresFromInstance.Add($"[Instance Log] {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to retrieve instance logs: {ex.Message}");
            }

            return pullFailuresFromInstance;
        }

        private ContainerLogAnalysisResult AnalyzeContainerLogs(IEnumerable<string> logs)
        {
            var result = new ContainerLogAnalysisResult
            {
                HasPullFailure = false
            };

            if (logs == null || !logs.Any())
            {
                result.ErrorMessage = "No logs available for analysis";
                return result;
            }

            // Common error patterns
            var errorPatterns = new Dictionary<string, (string Diagnosis, string Fix)>
            {
                { @"unauthorized|authentication required|access denied",
                    ("Authentication failure when pulling the image",
                     "Verify registry credentials or managed identity configuration") },

                { @"not found|404|no such image",
                    ("Image not found in the registry",
                     "Verify the image name and tag are correct") },

                { @"exceeded rate limit|rate limited|too many requests",
                    ("Registry rate limit exceeded",
                     "Use authenticated pulls or wait for rate limit reset") },

                { @"network timeout|connection refused|cannot connect",
                    ("Network connectivity issues",
                     "Check network configuration and NSG rules") },

                { @"insufficient memory|no space left|disk pressure",
                    ("Resource constraints preventing image pull",
                     "Check available resources and cleanup unused images") },

                { @"manifest unknown|manifest invalid|unsupported manifest",
                    ("Invalid or unsupported image manifest",
                     "Verify image architecture compatibility and manifest format") }
            };

            foreach (var log in logs)
            {
                foreach (var pattern in errorPatterns)
                {
                    if (Regex.IsMatch(log, pattern.Key, RegexOptions.IgnoreCase))
                    {
                        result.HasPullFailure = true;
                        result.ErrorMessage = log;
                        result.DetailedDiagnosis = pattern.Value.Diagnosis;
                        result.SuggestedFix = pattern.Value.Fix;
                        return result;
                    }
                }

                // Check for specific error codes
                if (log.Contains("ExitCode="))
                {
                    var exitCodeMatch = Regex.Match(log, @"ExitCode=(\d+)");
                    if (exitCodeMatch.Success)
                    {
                        string exitCode = exitCodeMatch.Groups[1].Value;
                        switch (exitCode)
                        {
                            case "125":
                                result.HasPullFailure = true;
                                result.DetailedDiagnosis = "Container runtime error during image pull";
                                result.SuggestedFix = "Check container runtime health and configuration";
                                break;
                            case "127":
                                result.HasPullFailure = true;
                                result.DetailedDiagnosis = "Command not found error, possible container runtime issue";
                                result.SuggestedFix = "Verify container runtime installation and configuration";
                                break;
                                // Add more exit codes as needed
                        }

                        if (result.HasPullFailure)
                        {
                            result.ErrorMessage = log;
                            return result;
                        }
                    }
                }
            }

            // Special case: Check for Back-off pattern
            var backoffLogs = logs.Where(l => l.Contains("Back-off pulling image")).ToList();
            if (backoffLogs.Any())
            {
                result.HasPullFailure = true;
                result.ErrorMessage = backoffLogs.First();
                result.DetailedDiagnosis = "Container runtime is backing off from pulling the image due to repeated failures";
                result.SuggestedFix = "Check previous error messages for root cause";
                return result;
            }

            return result;
        }

        private async Task<ContainerLogAnalysisResult> GetContainerLogAnalysis(string resourceId)
        {
            try
            {
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);

                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                    var containerApp = await containerAppResource.GetAsync();

                    var managedEnvResource = await armClient.GetContainerAppManagedEnvironmentResource(
                        new ResourceIdentifier(containerApp.Value.Data.EnvironmentId)).GetAsync();

                    var logAnalyticsCustomerId = managedEnvResource.Value.Data.AppLogsConfiguration?.LogAnalyticsConfiguration?.CustomerId;
                    if (!string.IsNullOrEmpty(logAnalyticsCustomerId))
                    {
                        string query = $@"
                            ContainerAppSystemLogs_CL 
                            | where ContainerAppName_s == '{containerApp.Value.Data.Name}' 
                            | where TimeGenerated > ago(1h)
                            | where Log_s has 'pull' or Log_s has 'image'
                            | project TimeGenerated, Log_s
                            | order by TimeGenerated desc
                        ";

                        var credential = _authService.GetArmOperationCredential();
                        var logsClient = new LogsQueryClient(credential);
                        var timeRange = new QueryTimeRange(TimeSpan.FromHours(1));

                        var queryResult = await logsClient.QueryWorkspaceAsync(logAnalyticsCustomerId, query, timeRange);
                        var logs = queryResult.Value.Table.Rows.Select(row => row[1].ToString()).ToList();

                        return AnalyzeContainerLogs(logs);
                    }
                }
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var logs = await webAppResource.GetWebSiteContainerLogsAsync();
                    // Update the AnalyzeContainerLogs method call to correctly handle the stream input
                    if (logs?.Value != null)
                    {
                        using (var reader = new StreamReader(logs.Value))
                        {
                            var logLines = new List<string>();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                logLines.Add(line);
                            }
                            return AnalyzeContainerLogs(logLines);
                        }
                    }
                }

                return new ContainerLogAnalysisResult
                {
                    HasPullFailure = false,
                    ErrorMessage = "No logs available for analysis"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing container logs for {resourceId}");
                return new ContainerLogAnalysisResult
                {
                    HasPullFailure = false,
                    ErrorMessage = $"Error analyzing logs: {ex.Message}"
                };
            }
        }

        private async Task<bool> CheckIsLinuxApp(ResourceIdentifier resourceIdentifier, ArmClient armClient)
        {
            var app = (await armClient.GetWebSiteResource(resourceIdentifier).GetAsync()).Value;
            string kind = app.Data.Kind.ToLower();
            return kind.IndexOf("linux") >= 0;
        }

        #region Helper Methods

        private async Task<ContainerRegistryResource> FindAcrResourceByName(ArmClient armClient, string appResourceId, string registryName)
        {
             _logger.LogInformation($"Attempting to find ACR '{registryName}' accessible from resource {appResourceId}");
            // Assuming appResourceId gives context for the subscription
            var appIdentifier = new ResourceIdentifier(appResourceId);
            var subscription = armClient.GetSubscriptionResource(appIdentifier);

            try
            {
                // Search within the same subscription first
                await foreach (var acr in subscription.GetContainerRegistriesAsync())
                {
                    // Simple name check - might need FQDN check depending on registryName format
                    if (acr.Data.Name.Equals(registryName, StringComparison.OrdinalIgnoreCase) ||
                        acr.Data.LoginServer?.StartsWith(registryName + ".", StringComparison.OrdinalIgnoreCase) == true)
                    {
                         _logger.LogInformation($"Found ACR '{acr.Data.Name}' in subscription {appIdentifier.SubscriptionId}");
                        return acr;
                    }
                }

                 _logger.LogWarning($"ACR '{registryName}' not found in subscription {appIdentifier.SubscriptionId}. Broader search might be needed if cross-subscription access is expected.");
                // Potentially extend to search across subscriptions if necessary and feasible
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding ACR with name {registryName} in subscription {appIdentifier.SubscriptionId}");
            }
            return null;
        }

        private async Task<ConnectivityTestResult> TestConnectivityToRegistryAsync(string registryName)
        {
            try
            {
                // Test V2 API endpoint
                var v2Endpoint = $"https://{registryName}.azurecr.io/v2/";
                var request = new HttpRequestMessage(HttpMethod.Get, v2Endpoint);

                // Add Azure-specific headers
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);

                // Check specific response codes
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        _logger.LogInformation($"Successfully connected to registry {registryName}");
                        return new ConnectivityTestResult
                        {
                            IsConnected = true,
                            HttpStatusCode = (int)response.StatusCode
                        };

                    case HttpStatusCode.Unauthorized:
                        // This is actually good - means we can reach the registry but need auth
                        _logger.LogInformation($"Registry {registryName} is reachable (requires authentication)");
                        return new ConnectivityTestResult
                        {
                            IsConnected = true,
                            HttpStatusCode = (int)response.StatusCode
                        };

                    case HttpStatusCode.NotFound:
                        _logger.LogWarning($"Registry {registryName} not found");
                        return new ConnectivityTestResult
                        {
                            IsConnected = false,
                            HttpStatusCode = (int)response.StatusCode,
                            ErrorMessage = "Registry not found"
                        };

                    default:
                        _logger.LogWarning($"Unexpected response from registry {registryName}: {response.StatusCode}");
                        return new ConnectivityTestResult
                        {
                            IsConnected = false,
                            HttpStatusCode = (int)response.StatusCode,
                            ErrorMessage = "Unexpected response from registry"
                        };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Network error connecting to registry {registryName}");
                return new ConnectivityTestResult
                {
                    IsConnected = false,
                    ErrorMessage = "Network error connecting to registry",
                    PotentialSolution = "Check network connectivity and DNS resolution"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error testing connectivity to registry {registryName}");
                return new ConnectivityTestResult
                {
                    IsConnected = false,
                    ErrorMessage = "Unexpected error occurred",
                    PotentialSolution = "Review logs for more details"
                };
            }
        }

        private async Task<bool> TestExternalRegistryConnectivity(string hostname)
        {
            try
            {
                // Try HTTPS first
                var httpsUrl = $"https://{hostname}/v2/";
                var request = new HttpRequestMessage(HttpMethod.Head, httpsUrl);

                try
                {
                    var httpResponse = await _httpClient.SendAsync(request);
                    if (httpResponse.IsSuccessStatusCode || httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInformation($"Successfully connected to registry {hostname} via HTTPS");
                        return true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, $"HTTPS connection failed to {hostname}");
                }

                return false;
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
                    _logger.LogWarning("Could not extract registry name from image reference");
                    return false;
                }

                var (repo, tag) = ExtractRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    _logger.LogWarning("Could not extract repository from image reference");
                    return false;
                }

                // Try to get the manifest for the image
                var manifestUrl = $"https://{registryName}.azurecr.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
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
                    _logger.LogWarning("Could not extract repository from Docker Hub image reference");
                    return false;
                }

                // Docker Hub API v2 endpoint
                var manifestUrl = $"https://registry-1.docker.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                // Try to get manifest
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // If unauthorized, try to get a token first
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();
                    if (authHeader != null && authHeader.Parameter.Contains("realm="))
                    {
                        // Extract token endpoint and service from WWW-Authenticate header
                        var realm = ExtractAuthValue(authHeader.Parameter, "realm");
                        var service = ExtractAuthValue(authHeader.Parameter, "service");
                        var scope = ExtractAuthValue(authHeader.Parameter, "scope");

                        // Get token
                        var tokenUrl = $"{realm}?service={service}&scope={scope}";
                        var tokenResponse = await _httpClient.GetAsync(tokenUrl);
                        if (tokenResponse.IsSuccessStatusCode)
                        {
                            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                            var token = System.Text.Json.JsonDocument.Parse(tokenContent)
                                .RootElement.GetProperty("token").GetString();

                            // Try manifest request again with token
                            request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                            request.Headers.Add("Authorization", $"Bearer {token}");
                            request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                            response = await _httpClient.SendAsync(request);
                            return response.IsSuccessStatusCode;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if image exists in Docker Hub: {imageReference}");
                return false;
            }
        }

        private string ExtractAuthValue(string authHeader, string key)
        {
            var match = Regex.Match(authHeader, $"{key}=\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private string ExtractRegistryHostname(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return string.Empty;

            try
            {
                // Split on first slash to get potential hostname
                var slashIndex = imageReference.IndexOf('/');
                if (slashIndex > 0)
                {
                    var possibleHostname = imageReference.Substring(0, slashIndex);

                    // If it contains a dot, it's likely a hostname
                    if (possibleHostname.Contains('.'))
                    {
                        return possibleHostname;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting registry hostname from {imageReference}");
                return string.Empty;
            }
        }

        private string ExtractRegistryName(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return string.Empty;

            try
            {
                // Check for ACR format
                var acrMatch = Regex.Match(imageReference, @"([^./]+)\.azurecr\.io/");
                if (acrMatch.Success && acrMatch.Groups.Count > 1)
                {
                    return acrMatch.Groups[1].Value;
                }

                // Check for ACR format with .acr.io
                var acrAltMatch = Regex.Match(imageReference, @"([^./]+)\.acr\.io/");
                if (acrAltMatch.Success && acrAltMatch.Groups.Count > 1)
                {
                    return acrAltMatch.Groups[1].Value;
                }

                // For other registries, get the hostname part
                var slashIndex = imageReference.IndexOf('/');
                if (slashIndex > 0)
                {
                    var hostnamePart = imageReference.Substring(0, slashIndex);
                    if (hostnamePart.Contains('.'))
                    {
                        return hostnamePart;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting registry name from {imageReference}");
                return string.Empty;
            }
        }

        private class ResourceLogAnalyzer
        {
            private readonly ILogger _logger;
            private readonly Dictionary<string, (string Pattern, string Message, string Solution)> _errorPatterns;

            public ResourceLogAnalyzer(ILogger logger)
            {
                _logger = logger;
                _errorPatterns = new Dictionary<string, (string Pattern, string Message, string Solution)>
                {
                    // Container App specific patterns
                    { "ContainerApp_NoImage", (
                        @"Failed to resolve image|image .* not found|repository .* not found",
                        "Container image not found in the registry",
                        "Verify the image name, tag, and registry path are correct"
                    )},
                    { "ContainerApp_Auth", (
                        @"unauthorized|authentication required|denied: access forbidden|access denied",
                        "Authentication failed when pulling the image",
                        "Check registry credentials or managed identity configuration"
                    )},
                    { "ContainerApp_Network", (
                        @"network timeout|connection refused|connection timed out|TLS handshake timeout",
                        "Network connectivity issues when pulling the image",
                        "Verify network configuration, NSG rules, and registry endpoint accessibility"
                    )},
                    // Linux Web App specific patterns
                    { "WebApp_StartupFailed", (
                        @"failed to start container|container .* failed to start",
                        "Container failed to start after pulling",
                        "Check container startup configuration and environment variables"
                    )},
                    { "WebApp_ImagePull", (
                        @"Error: ImagePullBackOff|Back-off pulling image",
                        "Container runtime is backing off from pulling the image",
                        "Check previous error messages for root cause and verify registry access"
                    )},
                    { "WebApp_Registry", (
                        @"cannot pull from registry|registry lookup failed",
                        "Failed to communicate with container registry",
                        "Verify registry URL and network connectivity"
                    )}
                };
            }

            public ContainerLogAnalysisResult AnalyzeContainerAppLogs(IEnumerable<(DateTime Timestamp, string Message)> logs)
            {
                var result = new ContainerLogAnalysisResult { HasPullFailure = false };

                if (logs?.Any() != true)
                {
                    result.ErrorMessage = "No logs available for analysis";
                    return result;
                }

                // Get the most recent logs first
                var orderedLogs = logs.OrderByDescending(l => l.Timestamp);

                foreach (var (timestamp, message) in orderedLogs)
                {
                    foreach (var errorPattern in _errorPatterns)
                    {
                        if (Regex.IsMatch(message, errorPattern.Value.Pattern, RegexOptions.IgnoreCase))
                        {
                            result.HasPullFailure = true;
                            result.ErrorMessage = message;
                            result.DetailedDiagnosis = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {errorPattern.Value.Message}";
                            result.SuggestedFix = errorPattern.Value.Solution;
                            return result;
                        }
                    }
                }

                // Check for cyclic failures
                if (HasCyclicFailures(orderedLogs))
                {
                    result.HasPullFailure = true;
                    result.ErrorMessage = "Detected repeated pull failures";
                    result.DetailedDiagnosis = "Container is experiencing cyclic pull failures";
                    result.SuggestedFix = "Review authentication configuration and network connectivity";
                    return result;
                }

                return result;
            }

            public ContainerLogAnalysisResult AnalyzeWebAppLogs(IEnumerable<string> logs)
            {
                var result = new ContainerLogAnalysisResult { HasPullFailure = false };

                if (logs?.Any() != true)
                {
                    result.ErrorMessage = "No logs available for analysis";
                    return result;
                }

                // Parse timestamp if available
                var parsedLogs = logs.Select(log =>
                {
                    var match = Regex.Match(log, @"^\[(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})\](.*)$");
                    if (match.Success)
                    {
                        return (
                            Timestamp: DateTime.Parse(match.Groups[1].Value),
                            Message: match.Groups[2].Value.Trim()
                        );
                    }
                    return (Timestamp: DateTime.MinValue, Message: log);
                })
                .OrderByDescending(l => l.Timestamp);

                foreach (var (timestamp, message) in parsedLogs)
                {
                    foreach (var errorPattern in _errorPatterns)
                    {
                        if (Regex.IsMatch(message, errorPattern.Value.Pattern, RegexOptions.IgnoreCase))
                        {
                            result.HasPullFailure = true;
                            result.ErrorMessage = message;
                            result.DetailedDiagnosis = timestamp != DateTime.MinValue
                                ? $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {errorPattern.Value.Message}"
                                : errorPattern.Value.Message;
                            result.SuggestedFix = errorPattern.Value.Solution;
                            return result;
                        }
                    }
                }

                // Check for environment-specific issues
                if (HasEnvironmentIssues(logs))
                {
                    result.HasPullFailure = true;
                    result.ErrorMessage = "Detected environment configuration issues";
                    result.DetailedDiagnosis = "Container environment variables or configuration may be incorrect";
                    result.SuggestedFix = "Review environment variables and app settings";
                    return result;
                }

                return result;
            }

            private bool HasEnvironmentIssues(IEnumerable<string> logs)
            {
                var envIssuePatterns = new[]
                {
                    @"invalid environment variable",
                    @"missing required environment variable",
                    @"configuration error",
                    @"invalid application setting",
                    @"environment variable .* not set",
                    @"required configuration .* missing"
                };

                return logs.Any(log =>
                    envIssuePatterns.Any(pattern =>
                        Regex.IsMatch(log, pattern, RegexOptions.IgnoreCase)));
            }

            private bool HasCyclicFailures(IEnumerable<(DateTime Timestamp, string Message)> logs)
            {
                const int failureThreshold = 3;
                const int timeWindowMinutes = 15;

                var recentFailures = logs
                    .Where(l => DateTime.UtcNow.Subtract(l.Timestamp).TotalMinutes <= timeWindowMinutes)
                    .Where(l => l.Message.Contains("failed to pull") ||
                               l.Message.Contains("ImagePullBackOff") ||
                               l.Message.Contains("ErrImagePull"))
                    .Take(failureThreshold + 1)
                    .ToList();

                return recentFailures.Count >= failureThreshold;
            }

            public bool IsCriticalError(string logMessage)
            {
                var criticalPatterns = new[]
                {
                    @"authentication failed",
                    @"access denied",
                    @"permission denied",
                    @"certificate error",
                    @"TLS handshake failure",
                    @"network is unreachable",
                    @"operation not permitted"
                };

                return criticalPatterns.Any(pattern =>
                    Regex.IsMatch(logMessage, pattern, RegexOptions.IgnoreCase));
            }

            public string GetErrorSeverity(string logMessage)
            {
                if (IsCriticalError(logMessage))
                    return "Critical";

                if (logMessage.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                    logMessage.Contains("retry", StringComparison.OrdinalIgnoreCase))
                    return "Warning";

                return "Info";
            }
        }

        private async Task<bool> CheckManagedIdentityRoleAssignmentAsync(
            ArmClient armClient,
            ManagedServiceIdentity identity,
            ResourceIdentifier registryId,
            bool checkOnlySystemAssigned)
        {
            try
            {
                var registryResource = armClient.GetContainerRegistryResource(registryId);
                var roleAssignments = await registryResource.GetRoleAssignments().GetAllAsync().ToListAsync();

                if (checkOnlySystemAssigned)
                {
                    return await CheckSystemAssignedIdentityRoleAsync(armClient, identity, roleAssignments);
                }

                return await CheckAllIdentitiesRoleAsync(armClient, identity, roleAssignments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking managed identity role assignments");
                return false;
            }
        }

        private async Task<bool> CheckSystemAssignedIdentityRoleAsync(
            ArmClient armClient,
            ManagedServiceIdentity identity,
            IEnumerable<RoleAssignmentResource> roleAssignments)
        {
            if (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned ||
                !identity.PrincipalId.HasValue)
            {
                return false;
            }

            foreach (var assignment in roleAssignments)
            {
                if (assignment.Data.PrincipalId == identity.PrincipalId.Value &&
                    IsRoleWithAcrPullPermissions(assignment.Data.RoleDefinitionId))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> CheckAllIdentitiesRoleAsync(
            ArmClient armClient,
            ManagedServiceIdentity identity,
            IEnumerable<RoleAssignmentResource> roleAssignments)
        {
            // Check system-assigned identity first
            if (await CheckSystemAssignedIdentityRoleAsync(armClient, identity, roleAssignments))
            {
                return true;
            }

            // Then check user-assigned identities
            if (identity.UserAssignedIdentities != null)
            {
                foreach (var userAssignedIdentity in identity.UserAssignedIdentities)
                {
                    if (userAssignedIdentity.Value.PrincipalId.HasValue)
                    {
                        foreach (var assignment in roleAssignments)
                        {
                            if (assignment.Data.PrincipalId == userAssignedIdentity.Value.PrincipalId.Value &&
                                IsRoleWithAcrPullPermissions(assignment.Data.RoleDefinitionId))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsRoleWithAcrPullPermissions(string roleDefinitionId)
        {
            var acrPullRoles = new[]
            {
                "7f951dda-4ed3-4680-a7ca-43fe172d538d", // AcrPull
                "b24988ac-6180-42a0-ab88-20f7382dd24c", // Contributor
                "8e3af657-a8ff-443c-a75c-2fe8c4bcb635"  // Owner
            };

            return acrPullRoles.Any(role => roleDefinitionId.EndsWith(role, StringComparison.OrdinalIgnoreCase));
        }

        private bool CheckForBlockingNsgRules(IDictionary<string, IReadOnlyList<SecurityRuleData>> nsgRules, string registryName)
        {
            if (nsgRules == null || nsgRules.Count == 0)
                return false;

            foreach (var nsg in nsgRules)
            {
                _logger.LogInformation($"Analyzing NSG rules for {nsg.Key}");
                var orderedRules = nsg.Value.OrderBy(r => r.Priority);

                foreach (var rule in orderedRules)
                {
                    // Only check outbound rules
                    if (rule.Direction == SecurityRuleDirection.Outbound.ToString())
                    {
                        bool affectsRegistry = false;
                        bool blocksTraffic = rule.Access == SecurityRuleAccess.Deny.ToString();

                        // Check destination addresses
                        if (rule.DestinationAddressPrefixes?.Any() == true)
                        {
                            affectsRegistry = rule.DestinationAddressPrefixes.Any(prefix =>
                                prefix == "*" ||
                                prefix == "Internet" ||
                                prefix == "AzureCloud" ||
                                (registryName != null && prefix.Contains("AzureContainerRegistry")));
                        }
                        else if (!string.IsNullOrEmpty(rule.DestinationAddressPrefix))
                        {
                            affectsRegistry =
                                rule.DestinationAddressPrefix == "*" ||
                                rule.DestinationAddressPrefix == "Internet" ||
                                rule.DestinationAddressPrefix == "AzureCloud" ||
                                (registryName != null && rule.DestinationAddressPrefix.Contains("AzureContainerRegistry"));
                        }

                        // Check if rule affects HTTPS port (443)
                        bool affectsHttpsPort = false;
                        if (rule.DestinationPortRanges?.Any() == true)
                        {
                            affectsHttpsPort = rule.DestinationPortRanges.Any(port =>
                                port == "*" || port == "443" || IsPortInRange(443, port));
                        }
                        else if (!string.IsNullOrEmpty(rule.DestinationPortRange))
                        {
                            affectsHttpsPort =
                                rule.DestinationPortRange == "*" ||
                                rule.DestinationPortRange == "443" ||
                                IsPortInRange(443, rule.DestinationPortRange);
                        }

                        if (affectsRegistry && affectsHttpsPort)
                        {
                            _logger.LogInformation(
                                $"Found rule {rule.Name} (Priority: {rule.Priority}) that {(blocksTraffic ? "blocks" : "allows")} " +
                                $"traffic to registry. Access: {rule.Access}");

                            if (blocksTraffic)
                            {
                                return true;
                            }
                            // If we find an allow rule, we can stop checking (unless there's a higher priority deny rule)
                            else if (rule.Access == SecurityRuleAccess.Allow.ToString())
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsPortInRange(int port, string range)
        {
            try
            {
                if (range.Contains("-"))
                {
                    var parts = range.Split('-');
                    if (int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                    {
                        return port >= start && port <= end;
                    }
                }
                else if (int.TryParse(range, out int exactPort))
                {
                    return port == exactPort;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing port range: {range}");
            }
            return false;
        }

        private async Task<bool> ValidateRegistryConnectivity(string registryName, bool isAcr = true)
        {
            try
            {
                var endpoint = isAcr ?
                    $"https://{registryName}.azurecr.io/v2/" :
                    $"https://{registryName}/v2/";

                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("Accept", "application/json");

                using var client = new HttpClient();
                var response = await client.SendAsync(request);

                // For registry API, both 200 OK and 401 Unauthorized are considered successful
                // (401 means we can reach the registry but need authentication)
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInformation($"Successfully validated connectivity to {endpoint}");
                    return true;
                }

                _logger.LogWarning($"Failed to validate connectivity to {endpoint}. Status code: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating registry connectivity for {registryName}");
                return false;
            }
        }

        private async Task<DiagnosticResult> PerformConnectivityDiagnostics(string registryName, bool isAcr = true)
        {
            var result = new DiagnosticResult();

            try
            {
                // Try DNS resolution first
                try
                {
                    var hostname = isAcr ? $"{registryName}.azurecr.io" : registryName;
                    var dnsResult = await System.Net.Dns.GetHostEntryAsync(hostname);
                    result.DnsResolved = true;
                    result.IpAddresses = dnsResult.AddressList.Select(ip => ip.ToString()).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DNS resolution failed");
                    result.DnsResolved = false;
                }

                // Check TLS/HTTPS connectivity
                var endpoint = isAcr ?
                    $"https://{registryName}.azurecr.io/v2/" :
                    $"https://{registryName}/v2/";

                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                using var client = new HttpClient();
                var response = await client.SendAsync(request);

                result.HttpsAccessible = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
                result.StatusCode = (int)response.StatusCode;

                // Check for specific headers
                if (response.Headers.Contains("Docker-Distribution-Api-Version"))
                {
                    result.IsRegistryApi = true;
                }

                if (response.Headers.WwwAuthenticate.Any())
                {
                    result.RequiresAuth = true;
                    result.AuthScheme = response.Headers.WwwAuthenticate.First().Scheme;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing connectivity diagnostics");
                result.Error = ex.Message;
                return result;
            }
        }

        private (string Repository, string Tag) ExtractDockerHubRepositoryAndTag(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
            {
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));
            }

            // Normalize the input by removing any double slashes  
            imageReference = imageReference.Replace("//", "/");

            var dockerImageRegex = new Regex(
                @"^(?:(?<registry>[^/]+(?:\.[^/]+)+(?:[:]\d+)?)/)?(?<repository>[^:]+)(?::(?<tag>[\w.-]+))?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var match = dockerImageRegex.Match(imageReference);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid Docker image reference format.", nameof(imageReference));
            }

            string repository = match.Groups["repository"].Value;
            string tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : "latest"; // Default tag is 'latest'  

            return (repository, tag);
        }

        private (string Repository, string Tag) ExtractRepositoryAndTag(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));

            // Split the image reference into repository and tag
            var parts = imageReference.Split(':');
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }

            // If no tag is specified, assume "latest"
            return (parts[0], "latest");
        }

        private async Task<ExternalRegistryVerificationResult> VerifyMicrosoftContainerRegistry(string imageReference, string resourceId)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ImageReference = imageReference,
                ResourceId = resourceId,
                RegistryType = RegistryType.MicrosoftContainerRegistry,
                IsSuccessful = false
            };

            try
            {
                // Extract registry details
                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    result.FailureReason = "Invalid Registry";
                    result.ErrorDetails = "Could not determine registry hostname";
                    return result;
                }

                // Check basic connectivity
                var isAccessible = await TestExternalRegistryConnectivity(registryHostname);
                if (!isAccessible)
                {
                    result.FailureReason = "Connectivity Issue";
                    result.ErrorDetails = $"Cannot connect to registry at {registryHostname}";
                    result.RecommendedAction = "Verify network connectivity and registry availability";
                    return result;
                }

                // For Microsoft Container Registry, we can only verify basic connectivity
                result.IsSuccessful = true;
                result.RegistryAccessible = true;
                result.RecommendedAction = "Registry is accessible, but you may need to configure authentication";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Microsoft Container Registry for {imageReference}");
                result.FailureReason = "Verification Error";
                result.ErrorDetails = ex.Message;
                return result;
            }
        }

        private async Task<ExternalRegistryVerificationResult> VerifyGoogleContainerRegistry(string imageReference, string resourceId)
        {
            var result = new ExternalRegistryVerificationResult
            {
                ImageReference = imageReference,
                ResourceId = resourceId,
                RegistryType = RegistryType.GoogleContainerRegistry,
                IsSuccessful = false
            };

            try
            {
                // Extract registry details
                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    result.FailureReason = "Invalid Registry";
                    result.ErrorDetails = "Could not determine registry hostname";
                    return result;
                }

                // Check basic connectivity
                var isAccessible = await TestExternalRegistryConnectivity(registryHostname);
                if (!isAccessible)
                {
                    result.FailureReason = "Connectivity Issue";
                    result.ErrorDetails = $"Cannot connect to registry at {registryHostname}";
                    result.RecommendedAction = "Verify network connectivity and registry availability";
                    return result;
                }

                // For Google Container Registry, we can only verify basic connectivity
                result.IsSuccessful = true;
                result.RegistryAccessible = true;
                result.RecommendedAction = "Registry is accessible, but you may need to configure authentication";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Google Container Registry for {imageReference}");
                result.FailureReason = "Verification Error";
                result.ErrorDetails = ex.Message;
                return result;
            }
        }
        // Add the missing method CheckSpecificUserAssignedIdentityRoleAsync to resolve the error CS0103.
        private async Task<bool> CheckSpecificUserAssignedIdentityRoleAsync(
            ArmClient armClient,
            Guid principalId,
            ResourceIdentifier registryId)
        {
            try
            {
                // Get the registry resource
                var registryResource = armClient.GetContainerRegistryResource(registryId);

                // Get all role assignments for the registry
                var roleAssignments = await registryResource.GetRoleAssignments().GetAllAsync().ToListAsync();

                // Check if the principalId has a role assignment with AcrPull permissions
                foreach (var assignment in roleAssignments)
                {
                    if (assignment.Data.PrincipalId == principalId &&
                        IsRoleWithAcrPullPermissions(assignment.Data.RoleDefinitionId))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user-assigned identity role assignments");
                return false;
            }
        }
        private RegistryType DetermineRegistryType(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
            {
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));
            }

            if (imageReference.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase) ||
                imageReference.Contains(".acr.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.AzureContainerRegistry;
            }
            else if (imageReference.Contains("docker.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.DockerHub;
            }
            else if (imageReference.Contains("gcr.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.GoogleContainerRegistry;
            }
            else if (imageReference.Contains("mcr.microsoft.com", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.MicrosoftContainerRegistry;
            }
            else if (imageReference.Contains("k8s.gcr.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.KubernetesRegistry;
            }
            else
            {
                return RegistryType.Other;
            }
        }

        private ImagePullingResult ExtractPullingResultFromTable(LogsTable table, string columnName)
        {
            var rows = table.Rows;
            if (rows.Count == 0 || rows[0].Count == 0)
            {
                return new ImagePullingResult()
                {
                    IsSuccessful = true,
                    FailureReason = ""
                };
            }
            var columns = table.Columns;
            int columnIndex = columns.ToList().FindIndex(c => c.Name == columnName);
            if (columnIndex < 0)
            {
                throw new Exception($"Column name: {columnName} not exist in the table");
            }
            string error = rows[0][columnIndex].ToString();
            return new ImagePullingResult()
            {
                FailureReason = error,
                IsSuccessful = false
            };
        }

        #endregion

        /// <summary>
        /// Rolls back a Container App or Web App to the last known working image
        /// </summary>
        /// <param name="resourceId">The resource ID of the Container App or Web App</param>
        /// <returns>Result of the rollback operation</returns>
        public async Task<RollbackImageResult> RollbackToLastWorkingImage(string resourceId)
        {
            _logger.LogInformation($"Rolling back to last known working image for resource: {resourceId}");
            
            var result = new RollbackImageResult
            {
                ResourceId = resourceId,
                IsSuccessful = false
            };

            try
            {
                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);

                // Check if this is a Container App
                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    return await RollbackContainerApp(armClient, resourceId);
                }
                // Check if this is a Web App
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    return await RollbackWebApp(armClient, resourceId);
                }
                else
                {
                    result.ErrorMessage = "Resource type not supported for rollback";
                    result.PotentialSolution = "Only Container Apps and Linux Web Apps are supported for image rollback";
                    _logger.LogWarning($"Unsupported resource type for rollback: {resourceId}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back resource {resourceId} to last working image");
                result.ErrorMessage = $"Error during rollback: {ex.Message}";
                result.PotentialSolution = "Check logs for more details and try again";
                return result;
            }
        }

        private async Task<RollbackImageResult> RollbackContainerApp(ArmClient armClient, string resourceId)
        {
            var result = new RollbackImageResult
            {
                ResourceId = resourceId,
                IsSuccessful = false
            };

            try
            {
                // Get the Container App resource
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Get all revisions for this Container App
                var revisions = await containerAppResource.GetContainerAppRevisions().ToListAsync();
                
                // Sort revisions by created time in descending order (newest first)
                revisions = revisions
                    .OrderByDescending(r => r.Data.CreatedOn)
                    .ToList();

                // We need at least 2 revisions to perform a rollback
                if (revisions.Count < 2)
                {
                    result.ErrorMessage = "Not enough revisions found for rollback";
                    result.PotentialSolution = "At least 2 revisions are needed for rollback";
                    return result;
                }

                // Get current active revision name
                string currentRevisionName = containerApp.Value.Data.LatestRevisionName;
                result.CurrentImage = await GetImageReferenceFromResourceId(resourceId);

                // Find the most recent inactive revision that is not the current one and is in a "Ready" state
                var targetRevision = revisions
                    .Where(r => r.Data.Name != currentRevisionName)
                    .Where(r => r.Data.ProvisioningState == ContainerAppRevisionProvisioningState.Provisioned)
                    .FirstOrDefault();

                if (targetRevision == null)
                {
                    result.ErrorMessage = "No suitable previous revision found for rollback";
                    result.PotentialSolution = "Deploy a new revision with a working image";
                    return result;
                }

                // Find the image reference in the target revision
                string? targetImageReference = null;
                if (targetRevision.Data.Template?.Containers != null && targetRevision.Data.Template.Containers.Count > 0)
                {
                    targetImageReference = targetRevision.Data.Template.Containers[0].Image;
                }

                if (string.IsNullOrEmpty(targetImageReference))
                {
                    result.ErrorMessage = "Could not determine image reference in previous revision";
                    result.PotentialSolution = "Deploy a new revision with a working image";
                    return result;
                }

                // Create a data object for the update
                ContainerAppData updateData = new ContainerAppData(containerApp.Value.Data.Location)
                {
                    Template = containerApp.Value.Data.Template
                };

                // Update image in the template containers
                if (updateData.Template?.Containers != null && updateData.Template.Containers.Count > 0)
                {
                    updateData.Template.Containers[0].Image = targetImageReference;
                }
                else
                {
                    result.ErrorMessage = "Current Container App template does not contain valid containers";
                    result.PotentialSolution = "Deploy a new revision manually with a working image";
                    return result;
                }

                // Update the Container App with the new template
                _logger.LogInformation($"Updating Container App {resourceId} with previous working image: {targetImageReference}");
                var updateOperation = await containerAppResource.UpdateAsync(
                   WaitUntil.Completed, // Specify the wait behavior (e.g., WaitUntil.Completed or WaitUntil.Started)
                   updateData,          // The ContainerAppData object to update
                   CancellationToken.None // Provide a CancellationToken (use CancellationToken.None if no cancellation is needed)
                );
                var updatedApp = updateOperation.Value;

                // Add information to result
                result.IsSuccessful = true;
                result.RolledBackToImage = targetImageReference;
                result.PreviousRevision = targetRevision.Data.Name;
                result.PotentialSolution = "Monitor the app to ensure it starts successfully";
                _logger.LogInformation($"Successfully rolled back Container App {resourceId} to image: {targetImageReference}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back Container App {resourceId}");
                result.ErrorMessage = $"Error during rollback: {ex.Message}";
                result.PotentialSolution = "Check permissions and try again, or deploy manually with a working image";
                return result;
            }
        }

        private async Task<RollbackImageResult> RollbackWebApp(ArmClient armClient, string resourceId)
        {
            var result = new RollbackImageResult
            {
                ResourceId = resourceId,
                IsSuccessful = false
            };

            try
            {
                // Get the Web App resource
                var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                var webApp = await webAppResource.GetAsync();

                // Check if this is a Linux Web App
                if (webApp.Value.Data.Kind == null || !webApp.Value.Data.Kind.ToLower().Contains("linux"))
                {
                    result.ErrorMessage = "Web App is not a Linux Web App";
                    result.PotentialSolution = "Only Linux Web Apps with container configurations can be rolled back";
                    return result;
                }

                // Get the current image reference
                result.CurrentImage = await GetImageReferenceFromResourceId(resourceId);
                if (string.IsNullOrEmpty(result.CurrentImage))
                {
                    result.ErrorMessage = "Could not determine current image reference";
                    result.PotentialSolution = "Verify that the Web App is using a container configuration";
                    return result;
                }

                // Get deployment history to find previous images
                var deploymentHistoryResponse = webAppResource.GetSiteDeployments();
                var deployments = await deploymentHistoryResponse.GetAllAsync().ToListAsync();

                // Sort deployments by time in descending order (newest first)
                deployments = deployments
                    .OrderByDescending(d => d.Data.StartOn)
                    .ToList();

                // We need at least 2 deployments to perform a rollback
                if (deployments.Count < 2)
                {
                    result.ErrorMessage = "Not enough deployment history found for rollback";
                    result.PotentialSolution = "At least 2 deployments are needed for rollback";
                    return result;
                }

                // Find the previous successful deployment
                var targetDeployment = deployments
                    .Skip(1) // Skip current deployment
                    .FirstOrDefault(d => d.Data.Status == 200);

                if (targetDeployment == null)
                {
                    result.ErrorMessage = "No suitable previous deployment found for rollback";
                    result.PotentialSolution = "Deploy a new version with a working image";
                    return result;
                }

                // Get the configuration from when the target deployment was active
                var configs = await webAppResource.GetAllConfigurationDataAsync().ToListAsync();
                //var previousConfigs = await configs.Value.GetAllAsync().ToListAsync();
                var targetConfig = configs
                    .Where(c => c != null && c.LinuxFxVersion != null && c.LinuxFxVersion != webApp.Value.Data.SiteConfig?.LinuxFxVersion)
                    .FirstOrDefault();

                if (targetConfig == null)
                {
                    // Check for backup configurations
                    var snapshots = await webAppResource.GetSnapshotsAsync().ToListAsync();
                    if (!snapshots.Any())
                    {
                        result.ErrorMessage = "No suitable previous container configuration found for rollback";
                        result.PotentialSolution = "Deploy a new version with a working image manually";
                        return result;
                    }

                    // Try to use the config snapshot:
                    var snapshotId = snapshots.First().Id;
                    var snapshotResponse = await webAppResource.GetSnapshotsAsync().FirstOrDefaultAsync(s => s.Id == snapshotId);

                    if (snapshotResponse == null)
                    {
                        throw new InvalidOperationException($"Snapshot with ID {snapshotId} not found.");
                    }
                    var snapshot = snapshotResponse;

                    if (snapshot.Kind == null ||
                        !snapshot.Kind.StartsWith("DOCKER|", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ErrorMessage = "No suitable previous container configuration found in snapshots";
                        result.PotentialSolution = "Deploy a new version with a working image manually";
                        return result;
                    }

                    // Extract image reference from snapshot
                    string targetImageReference = snapshot.Kind.Substring("DOCKER|".Length).Trim();
                    // Update the SiteConfigData to SiteConfigProperties conversion
                    var siteConfigUpdate = new SiteConfigProperties
                    {
                        LinuxFxVersion = snapshot.Kind
                    };

                    // Update the Web App with the previous image
                    _logger.LogInformation($"Rolling back Web App {resourceId} to previous image: {targetImageReference}");
                    await webAppResource.UpdateAsync(new SitePatchInfo { SiteConfig = siteConfigUpdate });

                    // Add information to result
                    result.IsSuccessful = true;
                    result.RolledBackToImage = targetImageReference;
                    result.PreviousRevision = targetDeployment.Data.Id.Name;
                    result.PotentialSolution = "Monitor the Web App to ensure it restarts successfully";
                    _logger.LogInformation($"Successfully rolled back Web App {resourceId} to image: {targetImageReference}");

                    return result;
                }
                else
                {
                    // Extract image reference from previous configuration
                    string targetImageReference = null;
                    if (targetConfig.LinuxFxVersion.StartsWith("DOCKER|", StringComparison.OrdinalIgnoreCase))
                    {
                        targetImageReference = targetConfig.LinuxFxVersion.Substring("DOCKER|".Length).Trim();
                    }

                    if (string.IsNullOrEmpty(targetImageReference))
                    {
                        result.ErrorMessage = "Could not determine previous image reference";
                        result.PotentialSolution = "Deploy a new version with a working image manually";
                        return result;
                    }

                    // Apply the rollback
                    var siteConfigUpdate = new SiteConfigProperties
                    {
                        LinuxFxVersion = targetConfig.LinuxFxVersion
                    };

                    // Update the Web App with the previous image
                    _logger.LogInformation($"Rolling back Web App {resourceId} to previous image: {targetImageReference}");
                    await webAppResource.UpdateAsync(new SitePatchInfo { SiteConfig = siteConfigUpdate });

                    // Restart the app to apply changes
                    await webAppResource.RestartAsync();

                    // Add information to result
                    result.IsSuccessful = true;
                    result.RolledBackToImage = targetImageReference;
                    result.PreviousRevision = targetDeployment.Data.Id.Name;
                    result.PotentialSolution = "Monitor the Web App to ensure it restarts successfully";
                    _logger.LogInformation($"Successfully rolled back Web App {resourceId} to image: {targetImageReference}");

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back Web App {resourceId}");
                result.ErrorMessage = $"Error during rollback: {ex.Message}";
                result.PotentialSolution = "Check permissions and try again, or deploy manually with a working image";
                return result;
            }
        }

        /// <summary>
        /// Updates the container image reference for a Container App or Web App
        /// </summary>
        /// <param name="resourceId">The resource ID of the Container App or Web App</param>
        /// <param name="newImageReference">The new image reference to use</param>
        /// <param name="containerName">Optional container name for multi-container apps</param>
        /// <returns>Result of the update operation</returns>
        public async Task<ContainerUpdateResult> UpdateContainerImage(string resourceId, string newImageReference, string containerName = null)
        {
            _logger.LogInformation($"Updating container image for resource: {resourceId} to {newImageReference}");
            
            var result = new ContainerUpdateResult
            {
                ResourceId = resourceId,
                IsSuccessful = false,
                NewImage = newImageReference
            };

            try
            {
                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);

                // Check if this is a Container App
                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    return await UpdateContainerAppImage(armClient, resourceId, newImageReference, containerName);
                }
                // Check if this is a Linux Web App
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    return await UpdateWebAppImage(armClient, resourceId, newImageReference);
                }
                else
                {
                    result.ErrorMessage = "Resource type not supported for container image update";
                    result.PotentialSolution = "Only Container Apps and Linux Web Apps are supported for image updates";
                    _logger.LogWarning($"Unsupported resource type for container image update: {resourceId}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating container image for resource {resourceId}");
                result.ErrorMessage = $"Error during update: {ex.Message}";
                result.PotentialSolution = "Check logs for more details and try again";
                return result;
            }
        }

        private async Task<ContainerUpdateResult> UpdateContainerAppImage(ArmClient armClient, string resourceId, string newImageReference, string containerName = null)
        {
            var result = new ContainerUpdateResult
            {
                ResourceId = resourceId,
                IsSuccessful = false,
                NewImage = newImageReference
            };

            try
            {
                // Get the Container App resource
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Store the current image for reference
                result.PreviousImage = await GetImageReferenceFromResourceId(resourceId);

                // Create a data object for the update
                ContainerAppData updateData = new ContainerAppData(containerApp.Value.Data.Location)
                {
                    Template = containerApp.Value.Data.Template
                };

                // Check if we have containers in the template
                if (updateData.Template?.Containers == null || updateData.Template.Containers.Count == 0)
                {
                    result.ErrorMessage = "No containers found in the Container App template";
                    result.PotentialSolution = "Verify the Container App configuration";
                    return result;
                }

                // Update specific container by name if provided, otherwise update the first container
                var containerToUpdate = string.IsNullOrEmpty(containerName)
                    ? updateData.Template.Containers[0]
                    : updateData.Template.Containers.FirstOrDefault(c => c.Name == containerName);

                if (containerToUpdate == null)
                {
                    result.ErrorMessage = $"Container with name '{containerName}' not found in the Container App";
                    result.PotentialSolution = "Verify the container name and try again";
                    return result;
                }

                // Save the container name for reference
                result.ContainerName = containerToUpdate.Name;

                // Update the image reference
                containerToUpdate.Image = newImageReference;

                // Update the Container App with the new template
                _logger.LogInformation($"Updating Container App {resourceId} with new image: {newImageReference}");
                var updateOperation = await containerAppResource.UpdateAsync(
                    WaitUntil.Completed, // Specify the wait behavior (e.g., WaitUntil.Completed or WaitUntil.Started)
                    updateData,          // The ContainerAppData object to update
                    CancellationToken.None // Provide a CancellationToken (use CancellationToken.None if no cancellation is needed)
                );
                var updatedApp = updateOperation.Value;

                // Add information to result
                result.IsSuccessful = true;
                result.UpdatedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation($"Successfully updated Container App {resourceId} to image: {newImageReference}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Container App {resourceId}");
                result.ErrorMessage = $"Error during update: {ex.Message}";
                result.PotentialSolution = "Check permissions and try again, or update manually with the correct image";
                return result;
            }
        }

        private async Task<ContainerUpdateResult> UpdateWebAppImage(ArmClient armClient, string resourceId, string newImageReference)
        {
            var result = new ContainerUpdateResult
            {
                ResourceId = resourceId,
                IsSuccessful = false,
                NewImage = newImageReference
            };

            try
            {
                // Get the Web App resource
                var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                var webApp = await webAppResource.GetAsync();

                // Check if this is a Linux Web App
                if (webApp.Value.Data.Kind == null || !webApp.Value.Data.Kind.ToLower().Contains("linux"))
                {
                    result.ErrorMessage = "Web App is not a Linux Web App";
                    result.PotentialSolution = "Only Linux Web Apps with container configurations can be updated";
                    return result;
                }

                // Get the current image reference
                result.PreviousImage = await GetImageReferenceFromResourceId(resourceId);
                if (string.IsNullOrEmpty(result.PreviousImage))
                {
                    result.ErrorMessage = "Could not determine current image reference";
                    result.PotentialSolution = "Verify that the Web App is using a container configuration";
                    return result;
                }

                // Format the Docker config string
                string linuxFxVersion = $"DOCKER|{newImageReference}";

                // Apply the update
                var siteConfigUpdate = new SiteConfigProperties
                {
                    LinuxFxVersion = linuxFxVersion
                };

                // Update the Web App with the new image
                _logger.LogInformation($"Updating Web App {resourceId} to use image: {newImageReference}");
                await webAppResource.UpdateAsync(new SitePatchInfo { SiteConfig = siteConfigUpdate });

                // Restart the app to apply changes
                await webAppResource.RestartAsync();

                // Add information to result
                result.IsSuccessful = true;
                result.ContainerName = "default"; // Web Apps typically have a single container
                result.UpdatedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation($"Successfully updated Web App {resourceId} to image: {newImageReference}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Web App {resourceId}");
                result.ErrorMessage = $"Error during update: {ex.Message}";
                result.PotentialSolution = "Check permissions and try again, or update manually with the correct image";
                return result;
            }
        }

        /// <summary>
        /// Attempts to pull an image to verify accessibility and authentication
        /// </summary>
        /// <param name="imageReference">The image reference to try pulling</param>
        /// <param name="resourceId">Optional resource ID to use its authentication context</param>
        /// <param name="useResourceAuth">Whether to use the resource's authentication configuration</param>
        /// <returns>Result of the image pull attempt</returns>
        public async Task<ImagePullResult> RetryImagePull(string imageReference, string resourceId = null, bool useResourceAuth = true)
        {
            _logger.LogInformation($"Attempting to pull image: {imageReference}. Using resource auth: {useResourceAuth}");
            var startTime = DateTimeOffset.UtcNow;
            
            var result = new ImagePullResult
            {
                ImageReference = imageReference,
                IsSuccessful = false,
                PullAttemptedAt = startTime,
                RegistryType = DetermineRegistryType(imageReference)
            };

            try
            {
                // Extract registry information
                string registryHost = ExtractRegistryHostname(imageReference);
                
                if (string.IsNullOrEmpty(registryHost))
                {
                    result.ErrorMessage = "Could not determine registry hostname from image reference";
                    result.SuggestedFix = "Make sure the image reference has a valid format (e.g. registry.com/repository:tag)";
                    return result;
                }
                
                // If using resource auth and resourceId is provided, get the resource's authentication configuration
                if (useResourceAuth && !string.IsNullOrEmpty(resourceId))
                {
                    return await PullImageWithResourceAuth(imageReference, resourceId, result);
                }
                else
                {
                    // Use default authentication
                    return await PullImageWithDefaultAuth(imageReference, result);
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTimeOffset.UtcNow;
                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                result.ErrorMessage = $"Error attempting to pull image: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check network connectivity and authentication configuration";
                _logger.LogError(ex, $"Error in RetryImagePull for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PullImageWithResourceAuth(
            string imageReference, 
            string resourceId, 
            ImagePullResult result)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                var armClient = _armClientFactory.GetArmClient();
                var resourceIdentifier = new ResourceIdentifier(resourceId);

                // Check if this is a Container App or Web App
                if (resourceIdentifier.ResourceType == ContainerAppResource.ResourceType)
                {
                    // Get the Container App resource
                    var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                    var containerApp = await containerAppResource.GetAsync();
                    
                    // Extract auth configs from the Container App
                    var registryConfiguration = containerApp.Value.Data.Configuration?.Registries;
                    string registryHost = ExtractRegistryHostname(imageReference);
                    
                    // Result metadata
                    result.AuthenticationMethod = "Container App Configuration";
                    
                    // If the registry is ACR
                    if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Details = "Using ACR authentication from Container App configuration";
                        
                        // Find the matching registry config
                        var matchingConfig = registryConfiguration?.FirstOrDefault(r => 
                            r.Server != null && r.Server.Contains(registryHost, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchingConfig != null)
                        {
                            // Use the identity if configured
                            if (!string.IsNullOrEmpty(matchingConfig.Identity))
                            {
                                var pullResult = await PullWithManagedIdentity(
                                    imageReference, 
                                    containerApp.Value.Data.Identity, 
                                    matchingConfig.Identity,
                                    result);
                                
                                var endTime = DateTimeOffset.UtcNow;
                                pullResult.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                                return pullResult;
                            }
                            // Otherwise try username/password
                            else if (!string.IsNullOrEmpty(matchingConfig.Username) && 
                                     !string.IsNullOrEmpty(matchingConfig.PasswordSecretRef))
                            {
                                // We can't actually access the secret, so simulate pull
                                _logger.LogInformation($"Container App using username/password auth for {registryHost}");
                                result.IsSuccessful = await CheckImageExistsInAcr(imageReference);
                                result.AuthenticationMethod = "Username/Password (simulated)";
                                result.Details = "Cannot perform actual pull with username/password; verifying image exists only";
                                
                                if (!result.IsSuccessful)
                                {
                                    result.ErrorMessage = "Image not found in registry";
                                    result.SuggestedFix = "Verify image exists";
                                }
                                
                                var endTime = DateTimeOffset.UtcNow;
                                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                                return result;
                            }
                            return await PullWithDefaultAcrAuth(imageReference, containerApp.Value.Data.Identity, result);
                        }
                        else
                        {
                            // No explicit config for this registry, try system identity
                            return await PullWithDefaultAcrAuth(imageReference, containerApp.Value.Data.Identity, result);
                        }
                    }
                    // For Docker Hub and other registries
                    else 
                    {
                        var matchingConfig = registryConfiguration?.FirstOrDefault(r => 
                            r.Server != null && r.Server.Contains(registryHost, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchingConfig != null)
                        {
                            _logger.LogInformation($"Container App has explicit config for registry {registryHost}");
                            
                            // For external registries, we can only simulate with check if image exists
                            bool imageExists = await CheckImageExistsInRegistry(imageReference, registryHost);
                            result.IsSuccessful = imageExists;
                            result.AuthenticationMethod = "External Registry Config (simulated)";
                            result.Details = "Cannot perform actual pull; verifying image exists only";
                            
                            if (!result.IsSuccessful)
                            {
                                result.ErrorMessage = "Image not found in registry or credentials are invalid";
                                result.SuggestedFix = "Verify image exists and credentials are correct";
                            }
                            
                            var endTime = DateTimeOffset.UtcNow;
                            result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                            return result;
                        }
                        else
                        {
                            // No registry config - try anonymous pull
                            return await PullWithAnonymousAuth(imageReference, result);
                        }
                    }
                }
                else if (resourceIdentifier.ResourceType == WebSiteResource.ResourceType && await CheckIsLinuxApp(resourceIdentifier, armClient))
                {
                    // Get the Web App resource
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var webApp = await webAppResource.GetAsync();
                    
                    // Get app settings for registry auth
                    var appSettingsResult = await webAppResource.GetApplicationSettingsAsync();
                    var appSettings = appSettingsResult.Value.Properties;
                    
                    // Result metadata
                    result.AuthenticationMethod = "Web App Configuration";
                    result.Details = "Using Web App configuration for pull authentication";
                    
                    string registryHost = ExtractRegistryHostname(imageReference);
                    
                    // Check if registry is ACR
                    if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if using username/password auth
                        if (appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_URL", out string registryUrl) && 
                            !string.IsNullOrEmpty(registryUrl) &&
                            appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_USERNAME", out _) &&
                            appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_PASSWORD", out _))
                        {
                            // Simulate pull with credentials check
                            _logger.LogInformation($"Web App using username/password auth for {registryHost}");
                            result.IsSuccessful = await CheckImageExistsInAcr(imageReference);
                            result.AuthenticationMethod = "Username/Password (simulated)";
                            result.Details = "Cannot perform actual pull with credentials; verifying image exists only";
                            
                            if (!result.IsSuccessful)
                            {
                                result.ErrorMessage = "Image not found in registry ";
                                result.SuggestedFix = "Verify image exists and credentials are correct";
                            }
                            
                            var endTime = DateTimeOffset.UtcNow;
                            result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                            return result;
                        }
                        else
                        {
                            // Try managed identity
                            return await PullWithDefaultAcrAuth(imageReference, webApp.Value.Data.Identity, result);
                        }
                    }
                    else
                    {
                        // For Docker Hub and other registries, check app settings
                        if (appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_URL", out string registryUrl) && 
                            !string.IsNullOrEmpty(registryUrl) &&
                            registryUrl.Contains(registryHost, StringComparison.OrdinalIgnoreCase))
                        {
                            // Simulate pull for external registry
                            _logger.LogInformation($"Web App has explicit config for registry {registryHost}");
                            
                            bool imageExists = await CheckImageExistsInRegistry(imageReference, registryHost);
                            result.IsSuccessful = imageExists;
                            result.AuthenticationMethod = "External Registry Config (simulated)";
                            result.Details = "Cannot perform actual pull; verifying image exists only";
                            
                            if (!result.IsSuccessful)
                            {
                                result.ErrorMessage = "Image not found in registry or credentials are invalid";
                                result.SuggestedFix = "Verify image exists and credentials are correct";
                            }
                            
                            var endTime = DateTimeOffset.UtcNow;
                            result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                            return result;
                        }
                        else
                        {
                            // No registry config - try anonymous pull
                            return await PullWithAnonymousAuth(imageReference, result);
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = "Unsupported resource type";
                    result.SuggestedFix = "Only Container Apps and Linux Web Apps are supported for authenticated pulls";
                    result.AuthenticationMethod = "None";
                    
                    var endTime = DateTimeOffset.UtcNow;
                    result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                    return result;
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTimeOffset.UtcNow;
                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                result.ErrorMessage = $"Error pulling image with resource auth: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check resource authentication configuration";
                _logger.LogError(ex, $"Error in PullImageWithResourceAuth for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PullWithManagedIdentity(
            string imageReference, 
            ManagedServiceIdentity identity, 
            string identityName,
            ImagePullResult result)
        {
            try
            {
                _logger.LogInformation($"Attempting to pull {imageReference} using managed identity: {identityName}");
                result.AuthenticationMethod = $"Managed Identity ({identityName})";
                
                // Get token credential for ACR
                string registryHost = ExtractRegistryHostname(imageReference);
                string registryName = ExtractRegistryName(imageReference);
                
                // Check if this is a system-assigned identity
                if (identityName == "system")
                {
                    if (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned && 
                        identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned)
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "System-assigned identity is not enabled";
                        result.SuggestedFix = "Enable system-assigned managed identity for the resource";
                        return result;
                    }
                    
                    // Use system-assigned identity to check if image exists
                    var credential = _authService.GetArmOperationCredential();
                    return await PerformImagePullWithToken(imageReference, credential, result);
                }
                else
                {
                    // This is a user-assigned identity
                    if (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned && 
                        identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned)
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "User-assigned identity is not enabled";
                        result.SuggestedFix = "Enable user-assigned managed identity for the resource";
                        return result;
                    }
                    
                    // Since we can't actually get the specific user-assigned identity credential,
                    // we'll use our access token and check if the image exists in ACR
                    if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                    {
                        bool imageExists = await CheckImageExistsInAcr(imageReference);
                        result.IsSuccessful = imageExists;
                        result.Details = "Simulating pull with user-assigned identity; verifying image existence";
                        
                        if (!imageExists)
                        {
                            result.ErrorMessage = "Image not found in ACR or identity lacks permissions";
                            result.SuggestedFix = "Verify image exists and identity has AcrPull role";
                        }
                        
                        return result;
                    }
                    else
                    {
                        result.IsSuccessful = false;
                        result.ErrorMessage = "User-assigned identities are only supported for ACR";
                        result.SuggestedFix = "Use username/password authentication for non-ACR registries";
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Error pulling with managed identity: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check managed identity configuration and permissions";
                _logger.LogError(ex, $"Error in PullWithManagedIdentity for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PullWithDefaultAcrAuth(
            string imageReference, 
            ManagedServiceIdentity identity,
            ImagePullResult result)
        {
            try
            {
                _logger.LogInformation($"Attempting to pull {imageReference} using default ACR auth");
                
                string registryHost = ExtractRegistryHostname(imageReference);
                
                if (!registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Default ACR auth only works with Azure Container Registry";
                    result.SuggestedFix = "Use explicit authentication for non-ACR registries";
                    return result;
                }
                
                // Check if managed identity is enabled
                if (identity == null || 
                    (identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned && 
                     identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned &&
                     identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "No managed identity is enabled for ACR authentication";
                    result.SuggestedFix = "Enable managed identity or configure explicit registry credentials";
                    return result;
                }
                
                // Try to pull using our ARM credential
                var credential = _authService.GetArmOperationCredential();
                result.AuthenticationMethod = "Default ACR Authentication";
                
                return await PerformImagePullWithToken(imageReference, credential, result);
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Error pulling with default ACR auth: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check ACR access and network connectivity";
                _logger.LogError(ex, $"Error in PullWithDefaultAcrAuth for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PullWithAnonymousAuth(string imageReference, ImagePullResult result)
        {
            try
            {
                _logger.LogInformation($"Attempting to pull {imageReference} anonymously");
                
                result.AuthenticationMethod = "Anonymous";
                string registryHost = ExtractRegistryHostname(imageReference);
                
                if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Anonymous pull not allowed for Azure Container Registry";
                    result.SuggestedFix = "Configure ACR authentication using managed identity or credentials";
                    return result;
                }
                
                // For Docker Hub and other registries
                bool imageExists = await CheckImageExistsInRegistry(imageReference, registryHost);
                result.IsSuccessful = imageExists;
                
                if (!imageExists)
                {
                    result.ErrorMessage = "Image not found or requires authentication";
                    result.SuggestedFix = "Verify image exists and configure registry authentication if needed";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = $"Error pulling anonymously: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check image reference and network connectivity";
                _logger.LogError(ex, $"Error in PullWithAnonymousAuth for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PullImageWithDefaultAuth(string imageReference, ImagePullResult result)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string registryHost = ExtractRegistryHostname(imageReference);
                
                // For ACR, try to use ARM credential
                if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                {
                    var credential = _authService.GetArmOperationCredential();
                    result.AuthenticationMethod = "ARM Credential";
                    
                    return await PerformImagePullWithToken(imageReference, credential, result);
                }
                // For other registries, try anonymous pull
                else
                {
                    return await PullWithAnonymousAuth(imageReference, result);
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTimeOffset.UtcNow;
                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                result.ErrorMessage = $"Error pulling with default auth: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check registry authentication requirements";
                _logger.LogError(ex, $"Error in PullImageWithDefaultAuth for {imageReference}");
                return result;
            }
        }

        private async Task<ImagePullResult> PerformImagePullWithToken(
            string imageReference, 
            TokenCredential credential,
            ImagePullResult result)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string registryHost = ExtractRegistryHostname(imageReference);
                string registryName = registryHost.Replace(".azurecr.io", "", StringComparison.OrdinalIgnoreCase);
                
                // Get token for ACR
                var tokenRequestContext = new TokenRequestContext(new[] { $"https://{registryHost}/.default" });
                var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                if (string.IsNullOrEmpty(token.Token))
                {
                    result.IsSuccessful = false;
                    result.ErrorMessage = "Failed to obtain token for registry access";
                    result.SuggestedFix = "Check managed identity permissions";
                    return result;
                }
                
                // Get the repository and tag
                var (repository, tag) = ExtractRepositoryAndTag(imageReference);
                
                // Check manifest using the token
                var manifestUrl = $"https://{registryHost}/v2/{repository}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Add("Authorization", $"Bearer {token.Token}");
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
                
                var response = await _httpClient.SendAsync(request);
                
                result.IsSuccessful = response.IsSuccessStatusCode;
                result.Details = $"HTTP Status: {(int)response.StatusCode} {response.StatusCode}";
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        result.ErrorMessage = "Image not found in registry";
                        result.SuggestedFix = "Verify image reference is correct and the image exists";
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        result.ErrorMessage = "Unauthorized access to registry";
                        result.SuggestedFix = "Verify identity has AcrPull role on the registry";
                    }
                    else
                    {
                        result.ErrorMessage = $"Registry returned error: {response.StatusCode}";
                        result.SuggestedFix = "Check registry configuration and network connectivity";
                    }
                }
                
                var endTime = DateTimeOffset.UtcNow;
                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                return result;
            }
            catch (Exception ex)
            {
                var endTime = DateTimeOffset.UtcNow;
                result.PullDurationSeconds = (endTime - startTime).TotalSeconds;
                result.IsSuccessful = false;
                result.ErrorMessage = $"Error during token-based pull: {ex.Message}";
                result.Details = ex.ToString();
                result.SuggestedFix = "Check token credentials and network connectivity";
                _logger.LogError(ex, $"Error in PerformImagePullWithToken for {imageReference}");
                return result;
            }
        }

        private async Task<bool> CheckImageExistsInRegistry(string imageReference, string registryHost)
        {
            try
            {
                // For Docker Hub
                if (registryHost.Contains("docker.io", StringComparison.OrdinalIgnoreCase) || 
                    string.IsNullOrEmpty(registryHost))
                {
                    return await CheckImageExistsInDockerHub(imageReference);
                }
                // For ACR
                else if (registryHost.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase))
                {
                    return await CheckImageExistsInAcr(imageReference);
                }
                // For other registries
                else
                {
                    // Extract repository and tag for non-standard registries
                    // Properly remove the registry hostname from the repository path
                    string repository = imageReference;
                    string tag = "latest";

                    // First remove the registry hostname from the repository path
                    if (repository.StartsWith(registryHost, StringComparison.OrdinalIgnoreCase))
                    {
                        repository = repository.Substring(registryHost.Length);
                    }
                    
                    // If it starts with a slash, remove it
                    if (repository.StartsWith("/"))
                    {
                        repository = repository.Substring(1);
                    }

                    // Extract the tag if present
                    int tagSeparatorIndex = repository.LastIndexOf(':');
                    if (tagSeparatorIndex > 0)
                    {
                        tag = repository.Substring(tagSeparatorIndex + 1);
                        repository = repository.Substring(0, tagSeparatorIndex);
                    }
                    
                    _logger.LogInformation($"Checking image existence in registry {registryHost}, repository: {repository}, tag: {tag}");
                    
                    // Try anonymous pull
                    var registryUrl = $"https://{registryHost}/v2/{repository}/manifests/{tag}";
                    var request = new HttpRequestMessage(HttpMethod.Head, registryUrl);
                    request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
                    
                    var response = await _httpClient.SendAsync(request);
                    
                    _logger.LogInformation($"Registry response: {(int)response.StatusCode} {response.StatusCode}, URL: {registryUrl}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // Try to get authentication details from WWW-Authenticate header
                        var authHeader = response.Headers.WwwAuthenticate.FirstOrDefault();
                        if (authHeader != null)
                        {
                            _logger.LogInformation($"Registry {registryHost} requires authentication: {authHeader.Scheme}");
                            
                            // For non-standard registries, we could try basic auth or bearer token in a future enhancement
                            // For now, just report that authentication is required
                        }
                        
                        return false;
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error checking if image exists in registry: {imageReference}");
                return false;
            }
        }
    }
}
