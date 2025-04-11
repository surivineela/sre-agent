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
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using System.IO.Compression;
using System.Text.Json;
using YamlDotNet.Core.Tokens;
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
            
            try
            {
                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();

                // Check if this is a Container App
                if (resourceId.Contains("Microsoft.App/containerApps", StringComparison.OrdinalIgnoreCase))
                {
                    // Get Container App resource
                    var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                    var containerApp = await containerAppResource.GetAsync();

                    // Get the active revision directly through ARM API instead of using _containerAppPlugin
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
                }
                // Support for Linux Web Apps
                else if (resourceId.Contains("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the Web App resource
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var webApp = await webAppResource.GetAsync();
                    
                    // Check if this is a Linux Web App
                    if (webApp.Value.Data.Kind != null && webApp.Value.Data.Kind.ToLower().Contains("linux"))
                    {
                        // Get the site configuration which contains the container information
                        var siteConfig = webApp.Value.Data.SiteConfig;
                        if (siteConfig?.LinuxFxVersion != null)
                        {
                            _logger.LogInformation($"Found LinuxFxVersion: {siteConfig.LinuxFxVersion}");
                            
                            // For Docker container apps, the image reference will be in LinuxFxVersion
                            if (siteConfig.LinuxFxVersion.StartsWith("DOCKER|", StringComparison.OrdinalIgnoreCase))
                            {
                                return siteConfig.LinuxFxVersion.Substring("DOCKER|".Length).Trim();
                            }
                        }

                        // Fallback: Try siteContainers
                        var containers = await webApp.Value.GetSiteContainers().ToListAsync();
                        if (containers.Count > 0)
                        {
                            var containerImage = containers[0].Data.Image;
                            if (!string.IsNullOrEmpty(containerImage))
                            {
                                return containerImage;
                            }
                        }

                        _logger.LogWarning($"Could not determine container image from configuration for {resourceId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Resource {resourceId} is not a Linux Web App");
                    }
                }

                _logger.LogWarning($"Could not find image reference for resource: {resourceId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting image reference for resource: {resourceId}");
                return null;
            }
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

            try
            {
                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();
                
                // Check if this is a Container App
                if (resourceId.Contains("Microsoft.App/containerApps", StringComparison.OrdinalIgnoreCase))
                {
                    // Get Container App resource
                    var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                    var containerApp = await containerAppResource.GetAsync();
                    
                    // Get the environment resource ID from the Container App
                    var environmentId = containerApp.Value.Data.EnvironmentId;
                    if (environmentId == null)
                    {
                        _logger.LogWarning($"Container App {resourceId} does not have an environment ID");
                        return result;
                    }
                    
                    // Get the Container App Environment
                    var environmentResource = armClient.GetContainerAppManagedEnvironmentResource(new ResourceIdentifier(environmentId));
                    var environment = await environmentResource.GetAsync();
                    
                    // Check if the environment has a VNet configuration
                    var vnetConfiguration = environment.Value.Data.VnetConfiguration;
                    if (vnetConfiguration == null)
                    {
                        _logger.LogInformation($"Container App Environment {environmentId} does not have VNet integration");
                        return result;
                    }
                    
                    // Get the infrastructure subnet ID
                    var infrastructureSubnetId = vnetConfiguration.InfrastructureSubnetId?.ToString();
                    if (string.IsNullOrEmpty(infrastructureSubnetId))
                    {
                        _logger.LogWarning($"Container App Environment {environmentId} has VNet configuration but no infrastructure subnet ID");
                        return result;
                    }
                    
                    // Get the infrastructure subnet
                    var subnetResource = armClient.GetSubnetResource(new ResourceIdentifier(infrastructureSubnetId));
                    var subnet = await subnetResource.GetAsync();
                    
                    // Get NSGs associated with this subnet
                    var nsgId = subnet.Value.Data.NetworkSecurityGroup?.Id;
                    if (nsgId != null)
                    {
                        var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                        var nsg = await nsgResource.GetAsync();
                        
                        var securityRules = nsg.Value.Data.SecurityRules.ToList();
                        result.Add(nsg.Value.Data.Name, securityRules);
                        _logger.LogInformation($"Found NSG {nsg.Value.Data.Name} with {securityRules.Count} rules for subnet {subnet.Value.Data.Name}");
                    }
                }
                // Support for Linux Web Apps
                else if (resourceId.Contains("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the Web App resource
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var webApp = await webAppResource.GetAsync();
                    
                    // Check if this Web App has VNet integration
                    var vnetConnections = webAppResource.GetSiteVirtualNetworkConnections();
                    if (vnetConnections != null)
                    {
                        var vnetInfo = await vnetConnections.GetAllAsync().FirstOrDefaultAsync();
                        if (vnetInfo?.Data?.VnetResourceId != null)
                        {
                            // Get the subnet ID from the VNet integration
                            var subnetId = vnetInfo.Data.VnetResourceId;
                            if (subnetId != null)
                            {
                                // Get the subnet resource
                                var subnetResource = armClient.GetSubnetResource(new ResourceIdentifier(subnetId));
                                var subnet = await subnetResource.GetAsync();

                                // Get NSGs associated with this subnet
                                var nsgId = subnet.Value.Data.NetworkSecurityGroup?.Id;
                                if (nsgId != null)
                                {
                                    var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                                    var nsg = await nsgResource.GetAsync();

                                    var securityRules = nsg.Value.Data.SecurityRules.ToList();
                                    result.Add(nsg.Value.Data.Name, securityRules);
                                    _logger.LogInformation($"Found NSG {nsg.Value.Data.Name} with {securityRules.Count} rules for subnet {subnet.Value.Data.Name}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation($"Web App {resourceId} does not have VNet integration");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Web App {resourceId} does not have VNet integration");
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting network security rules for resource: {resourceId}");
                return result;
            }
        }

        /// <summary>
        /// Checks if a Container App is properly authenticated to an Azure Container Registry
        /// </summary>
        public async Task<AcrAuthenticationStatus> CheckAcrAuthentication(string resourceId)
        {
            _logger.LogInformation($"Checking ACR authentication for app {resourceId}");

            // Get the image reference from the resource ID
            string imageReference = await GetImageReferenceFromResourceId(resourceId);
            
            var result = new AcrAuthenticationStatus
            {
                ResourceId = resourceId,
                ImageReference = imageReference,
                IsAuthenticated = false
            };

            // If we couldn't get the image reference, return with an error
            if (string.IsNullOrEmpty(imageReference))
            {
                result.ErrorMessage = "Could not determine image reference from the resource";
                return result;
            }

            try
            {
                // Extract registry name from the image reference
                string registryName = ExtractRegistryName(imageReference);
                if (string.IsNullOrEmpty(registryName))
                {
                    result.ErrorMessage = "Could not extract registry name from image reference";
                    return result;
                }

                // Verify this is actually an Azure Container Registry
                if (!imageReference.Contains(".azurecr.io/", StringComparison.OrdinalIgnoreCase) && 
                    !imageReference.Contains(".acr.io/", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "Image is not from Azure Container Registry. Use verify_external_registry tool for non-ACR images.";
                    result.PotentialSolution = "For non-ACR images, configure registry credentials in the Container App settings.";
                    return result;
                }

                // Get the ARM client
                var armClient = _armClientFactory.GetArmClient();
                
                // Check if this is a Container App or a Web App
                if (resourceId.Contains("Microsoft.App/containerApps", StringComparison.OrdinalIgnoreCase))
                {
                    // Container App ACR Authentication Check
                    return await CheckContainerAppAcrAuth(armClient, resourceId, registryName, imageReference);
                }
                else if (resourceId.Contains("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase))
                {
                    // Web App ACR Authentication Check
                    return await CheckWebAppAcrAuth(armClient, resourceId, registryName, imageReference);
                }
                else
                {
                    result.ErrorMessage = "Resource type not supported for ACR authentication check";
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking ACR authentication: {resourceId}");
                result.ErrorMessage = $"Exception during authentication check: {ex.Message}";
                return result;
            }
        }

        // New method to handle Container App ACR authentication check
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
                IsAuthenticated = false
            };

            // Get the Container App resource to check registry authentication settings
            var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
            var containerApp = await containerAppResource.GetAsync();

            // Check how the Container App is configured to authenticate to ACR
            var registryConfiguration = containerApp.Value.Data.Configuration?.Registries;
            
            // If explicit registry credentials are configured for this ACR
            if (registryConfiguration != null && registryConfiguration.Any(r => 
                r.Server != null && r.Server.Contains(registryName, StringComparison.OrdinalIgnoreCase)))
            {
                var registryConfig = registryConfiguration.First(r => 
                    r.Server != null && r.Server.Contains(registryName, StringComparison.OrdinalIgnoreCase));
                
                // Check if using managed identity for this registry
                if (registryConfig.Identity != null)
                {
                    _logger.LogInformation($"Container App is configured to use managed identity for ACR {registryName}");
                    
                    // Now we need to check if the specified identity has proper ACR Pull permissions
                    
                    // Get registry resource for ACR-Pull role check
                    var registry = await FindAcrResourceByName(armClient, resourceId, registryName);
                    if (registry == null)
                    {
                        result.IsAuthenticated = false;
                        result.ErrorMessage = $"Could not find ACR with name {registryName}";
                        result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                        return result;
                    }
                    
                    // Get the identity that's being used for registry auth
                    var identityName = registryConfig.Identity;
                    
                    // Check if this is referring to system-assigned identity
                    if (identityName == "system")
                    {
                        // Verify system-assigned identity is enabled
                        if (containerApp.Value.Data.Identity == null || 
                            containerApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned ||
                            !containerApp.Value.Data.Identity.PrincipalId.HasValue)
                        {
                            result.IsAuthenticated = false;
                            result.ErrorMessage = "Container App is configured to use system-assigned identity for ACR, but system-assigned identity is not enabled";
                            result.PotentialSolution = "Enable system-assigned managed identity for the Container App";
                            return result;
                        }
                        
                        // Check if the system-assigned identity has proper role assignments
                        bool hasProperRoleAssignment = await CheckManagedIdentityRoleAssignmentAsync(
                            armClient,
                            containerApp.Value.Data.Identity,
                            registry.Id,
                            true);  // Check only system-assigned identity
                        
                        if (!hasProperRoleAssignment)
                        {
                            result.IsAuthenticated = false;
                            result.ErrorMessage = "The system-assigned managed identity does not have AcrPull role on the registry";
                            result.PotentialSolution = "Assign the AcrPull role to the system-assigned managed identity on the ACR resource";
                            return result;
                        }
                    }
                    else
                    {
                        // This is a user-assigned identity - check if it's configured and has permissions
                        if (containerApp.Value.Data.Identity == null ||
                            !containerApp.Value.Data.Identity.UserAssignedIdentities.Any())
                        {
                            result.IsAuthenticated = false;
                            result.ErrorMessage = $"Container App is configured to use user-assigned identity '{identityName}' for ACR, but no user-assigned identities are configured";
                            result.PotentialSolution = "Configure the user-assigned managed identity for the Container App";
                            return result;
                        }
                        
                        // Check if the specified identity exists in the Container App's user-assigned identities
                        var userAssignedIdentities = containerApp.Value.Data.Identity.UserAssignedIdentities;
                        
                        // The identity reference in registry config might be the full resource ID or just the name
                        bool identityFound = false;
                        foreach (var identity in userAssignedIdentities)
                        {
                            // Check if identity name matches (either full ID or just name part)
                            var identityKey = identity.Key.ToString();
                            if (identityKey.EndsWith($"/{identityName}", StringComparison.OrdinalIgnoreCase) ||
                                identityKey.Equals(identityName, StringComparison.OrdinalIgnoreCase))
                            {
                                identityFound = true;

                                // Check if this identity has proper ACR Pull role
                                if (identity.Value.PrincipalId.HasValue)
                                {
                                    var principalId = identity.Value.PrincipalId.Value;
                                    bool hasProperRoleAssignment = await CheckSpecificUserAssignedIdentityRoleAsync(
                                        armClient,
                                        principalId,
                                        registry.Id);

                                    if (!hasProperRoleAssignment)
                                    {
                                        result.IsAuthenticated = false;
                                        result.ErrorMessage = $"The user-assigned managed identity {identityName} does not have AcrPull role on the registry";
                                        result.PotentialSolution = "Assign the AcrPull role to the user-assigned managed identity on the ACR resource";
                                        return result;
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"User-assigned identity {identityName} does not have a valid PrincipalId.");
                                    result.IsAuthenticated = false;
                                    result.ErrorMessage = $"The user-assigned managed identity {identityName} does not have a valid PrincipalId.";
                                    result.PotentialSolution = "Ensure the user-assigned managed identity is properly configured.";
                                    return result;
                                }
                                
                                break;
                            }
                        }
                        
                        if (!identityFound)
                        {
                            result.IsAuthenticated = false;
                            result.ErrorMessage = $"Container App is configured to use user-assigned identity '{identityName}' for ACR, but this identity is not attached to the Container App";
                            result.PotentialSolution = "Configure the specified user-assigned managed identity for the Container App";
                            return result;
                        }
                    }
                }
                else if (registryConfig.Username != null && registryConfig.PasswordSecretRef != null)
                {
                    // App is using username/password auth for this registry
                    _logger.LogInformation($"Container App is configured to use username/password authentication for ACR {registryName}");
                    
                    // We can't verify the actual credentials as we don't have access to the password secret
                    // but we can note that username/password auth is being used
                    result.IsAuthenticated = true;  // Assume authentication is properly configured
                    result.ErrorMessage = "Container App is using username/password authentication for ACR. Cannot verify if credentials are correct.";
                    result.PotentialSolution = "For improved security, consider using managed identity authentication instead of username/password.";
                    return result;
                }
                else
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "Container App has registry configuration for ACR, but no authentication method is specified";
                    result.PotentialSolution = "Configure either managed identity or username/password authentication for the registry";
                    return result;
                }
            }
            else
            {
                // No explicit registry configuration for this ACR
                _logger.LogInformation($"No explicit registry configuration found for ACR {registryName}. Checking for default managed identity configuration.");
                
                // Check if the Container App has managed identity configured for default use
                if (containerApp.Value.Data.Identity == null ||
                    (!containerApp.Value.Data.Identity.UserAssignedIdentities.Any() &&
                     containerApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned))
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "No explicit registry configuration for ACR and no Managed Identity is configured for the Container App";
                    result.PotentialSolution = "Configure registry authentication in the Container App settings or enable Managed Identity";
                    return result;
                }

                // Get registry resource for ACR-Pull role check
                var registry = await FindAcrResourceByName(armClient, resourceId, registryName);
                if (registry == null)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"Could not find ACR with name {registryName}";
                    result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                    return result;
                }

                // Check if any managed identity has proper role assignments on the ACR
                bool hasProperRoleAssignment = await CheckManagedIdentityRoleAssignmentAsync(
                    armClient, 
                    containerApp.Value.Data.Identity, 
                    registry.Id,
                    false);  // Check all identities
                
                if (!hasProperRoleAssignment)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "No Managed Identity has AcrPull role on the registry";
                    result.PotentialSolution = "Assign the AcrPull role to a Managed Identity on the ACR resource";
                    return result;
                }
            }

            // Check for network connectivity issues via NSG rules
            var nsgRules = await GetNetworkSecurityRulesForResource(resourceId);
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

        // New method to handle Web App ACR authentication check
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
                IsAuthenticated = false
            };

            // Get the Web App resource
            var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
            var webApp = await webAppResource.GetAsync();

            // Check how the Web App is configured to authenticate to ACR
            
            // Get the app settings to check for registry credentials
            var appSettingsResult = await webAppResource.GetApplicationSettingsAsync();
            var appSettings = appSettingsResult.Value.Properties;

            // First, check if there are explicit registry credentials in app settings
            bool hasExplicitCredentials = false;
            if (appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_URL", out string registryUrl) && 
                !string.IsNullOrEmpty(registryUrl) && 
                registryUrl.Contains(registryName, StringComparison.OrdinalIgnoreCase))
            {
                hasExplicitCredentials = true;
                
                // Check if username/password auth is configured
                bool hasUsernamePassword = appSettings.TryGetValue("DOCKER_REGISTRY_SERVER_USERNAME", out string username) && 
                                        !string.IsNullOrEmpty(username) &&
                                        appSettings.ContainsKey("DOCKER_REGISTRY_SERVER_PASSWORD");
                
                if (hasUsernamePassword)
                {
                    _logger.LogInformation($"Web App is configured to use username/password authentication for ACR {registryName}");
                    
                    // We can't verify the actual password, but we can note that username/password auth is being used
                    result.IsAuthenticated = true;  // Assume authentication is properly configured
                    result.ErrorMessage = "Web App is using username/password authentication for ACR. Cannot verify if credentials are correct.";
                    result.PotentialSolution = "For improved security, consider using managed identity authentication instead of username/password.";
                    return result;
                }
            }

            // If no explicit credentials, or credentials are incomplete, check for managed identity
            if (!hasExplicitCredentials || 
                !appSettings.ContainsKey("DOCKER_REGISTRY_SERVER_USERNAME") || 
                !appSettings.ContainsKey("DOCKER_REGISTRY_SERVER_PASSWORD"))
            {
                _logger.LogInformation($"Checking managed identity configuration for Web App {resourceId}");
                
                // Check if managed identity is enabled
                if (webApp.Value.Data.Identity == null ||
                    (webApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssigned &&
                     webApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.SystemAssignedUserAssigned &&
                     webApp.Value.Data.Identity.ManagedServiceIdentityType != ManagedServiceIdentityType.UserAssigned))
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "No valid registry credentials found and no managed identity is enabled for the Web App";
                    result.PotentialSolution = "Configure registry credentials in app settings or enable Managed Identity";
                    return result;
                }
                
                // Get registry resource for ACR-Pull role check
                var registry = await FindAcrResourceByName(armClient, resourceId, registryName);
                if (registry == null)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = $"Could not find ACR with name {registryName}";
                    result.PotentialSolution = "Verify the registry exists and is accessible from your subscription";
                    return result;
                }
                
                // Check for system-assigned identity role assignment
                bool hasProperRoleAssignment = false;

                // Check system-assigned if enabled
                if (webApp.Value.Data.Identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssigned ||
                    webApp.Value.Data.Identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssignedUserAssigned)
                {
                    if (webApp.Value.Data.Identity.PrincipalId.HasValue)
                    {
                        hasProperRoleAssignment = await CheckSpecificUserAssignedIdentityRoleAsync(
                            armClient,
                            webApp.Value.Data.Identity.PrincipalId.Value,
                            registry.Id);
                    }
                }
                
                // If system-assigned doesn't have permission, check user-assigned identities
                if (!hasProperRoleAssignment && 
                    (webApp.Value.Data.Identity.ManagedServiceIdentityType == ManagedServiceIdentityType.UserAssigned ||
                     webApp.Value.Data.Identity.ManagedServiceIdentityType == ManagedServiceIdentityType.SystemAssignedUserAssigned))
                {
                    if (webApp.Value.Data.Identity.UserAssignedIdentities != null)
                    {
                        foreach (var identity in webApp.Value.Data.Identity.UserAssignedIdentities)
                        {
                            if (identity.Value.PrincipalId.HasValue)
                            {
                                bool identityHasRole = await CheckSpecificUserAssignedIdentityRoleAsync(
                                    armClient,
                                    identity.Value.PrincipalId.Value,
                                    registry.Id);
                                
                                if (identityHasRole)
                                {
                                    hasProperRoleAssignment = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (!hasProperRoleAssignment)
                {
                    result.IsAuthenticated = false;
                    result.ErrorMessage = "No managed identity has AcrPull role on the registry";
                    result.PotentialSolution = "Assign the AcrPull role to a managed identity on the ACR resource";
                    return result;
                }
            }
            
            // Check for network connectivity issues via NSG rules
            var nsgRules = await GetNetworkSecurityRulesForResource(resourceId);
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

            // If we passed all checks, the web app should be able to authenticate
            result.IsAuthenticated = true;
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
                if (resourceId.Contains("Microsoft.App/containerApps", StringComparison.OrdinalIgnoreCase))
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
                        | project TimeGenerated, Log_s, ContainerAppName_s
                        | summarize by Log_s
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
                else if (resourceId.Contains("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase))
                {
                    var webAppResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
                    var webApp = await webAppResource.GetAsync();

                    //if image reference is ACR, use a custom method
                    if (imageReference.Contains("azurecr.io", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ImagePullingResult
                        {
                            IsSuccessful = false,
                            FailureReason = "Image reference is ACR. Use IsACRImageManifestAccessibleAsync tool to check image accessibility."
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

        public async Task<ImagePullingResult> IsACRImageManifestAccessibleAsync(string resourceId)
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
                
                if (resourceId.Contains("Microsoft.App/containerApps"))
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
                else if (resourceId.Contains("Microsoft.Web/sites"))
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

        #region Helper Methods

        private async Task<ContainerRegistryResource> FindAcrResourceByName(ArmClient armClient, string appResourceId, string registryName)
        {
            try
            {
                var containerAppId = new ResourceIdentifier(appResourceId);
                var subscriptionId = containerAppId.SubscriptionId;

                var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                var registries = subscription.GetContainerRegistries();

                // Query for ACRs with the given name
                await foreach (var registry in registries.ToAsyncEnumerable())
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
    }
}
