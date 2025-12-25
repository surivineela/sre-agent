// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Common.ApiModels;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Service for loading managed identity configuration from the agent's MSI config map and certificates.
/// </summary>
public class ManagedIdentityConfigService : IManagedIdentityConfigService
{
    private readonly ILogger<ManagedIdentityConfigService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    // MSI config and cert paths (mounted from k8s configmap/secret)
    private const string MsiConfigPath = "/var/msi/config/config.json";
    private const string MsiCertsPath = "/var/msi/certs";
    private const string SystemManagedIdentityName = "system";

    public ManagedIdentityConfigService(
        ILogger<ManagedIdentityConfigService> logger,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public async Task<ManagedIdentityInfo?> GetManagedIdentityInfoAsync(string? identityResourceId)
    {
        // In development, skip MSI loading
        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInternalInformation("Development environment - skipping MSI config loading.");
            return null;
        }

        // Check if MSI config exists
        if (!File.Exists(MsiConfigPath))
        {
            _logger.LogInternalWarning($"MSI config file not found at {MsiConfigPath}. Skipping managed identity bootstrap.");
            return null;
        }

        try
        {
            // Load and parse the MSI config map
            var configJson = await File.ReadAllTextAsync(MsiConfigPath);
            var configMap = JsonSerializer.Deserialize<ManagedIdentityConfigMap>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (configMap == null)
            {
                _logger.LogInternalWarning("Failed to parse MSI config map. Skipping managed identity bootstrap.");
                return null;
            }

            // Determine which identity to use based on identityResourceId
            ManagedIdentityConfig? targetIdentity = null;
            string? pfxName = null;

            if (string.IsNullOrEmpty(identityResourceId) || SystemManagedIdentityName.Equals(identityResourceId, StringComparison.OrdinalIgnoreCase))
            {
                // Use system-assigned identity
                targetIdentity = configMap.GetSystemAssignedIdentity();
                pfxName = configMap.PfxName;
                _logger.LogInternalInformation("Using system-assigned managed identity for bootstrap.");
            }
            else
            {
                // Find user-assigned identity matching the configured identity resource ID
                targetIdentity = configMap.ExplicitIdentities?.FirstOrDefault(i =>
                    string.Equals(i.ResourceId, identityResourceId, StringComparison.OrdinalIgnoreCase));

                if (targetIdentity == null)
                {
                    _logger.LogInternalWarning($"Configured identity '{identityResourceId}' not found in MSI config. Falling back to system-assigned.");
                    targetIdentity = configMap.GetSystemAssignedIdentity();
                    pfxName = configMap.PfxName;
                }
                else
                {
                    pfxName = targetIdentity.PfxName;
                    _logger.LogInternalInformation($"Using user-assigned managed identity '{identityResourceId}' for bootstrap.");
                }
            }

            if (targetIdentity == null || string.IsNullOrEmpty(targetIdentity.ClientId))
            {
                _logger.LogInternalWarning("No valid managed identity found in config. Skipping managed identity bootstrap.");
                return null;
            }

            // Load the PFX certificate bytes from /var/msi/certs/{pfxName}
            byte[]? pfxBytes = null;
            if (!string.IsNullOrEmpty(pfxName))
            {
                var certPath = Path.Combine(MsiCertsPath, $"{pfxName}.p12");
                if (File.Exists(certPath))
                {
                    pfxBytes = await File.ReadAllBytesAsync(certPath);
                    _logger.LogInternalInformation($"Loaded PFX certificate from {certPath} ({pfxBytes.Length} bytes).");
                }
                else
                {
                    _logger.LogInternalWarning($"PFX certificate not found at {certPath}. Bootstrap may fail.");
                }
            }

            return new ManagedIdentityInfo
            {
                Type = targetIdentity.Type ?? "SystemAssigned",
                PfxBytes = pfxBytes ?? Array.Empty<byte>(),
                ClientId = targetIdentity.ClientId ?? string.Empty,
                PrincipalId = targetIdentity.PrincipalId ?? string.Empty,
                TenantId = targetIdentity.TenantId ?? configMap.TenantId ?? string.Empty,
                AuthenticationEndpoint = targetIdentity.AuthenticationEndpoint ?? configMap.AuthenticationEndpoint ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error loading managed identity info from MSI config.");
            return null;
        }
    }
}

/// <summary>
/// Represents identity information parsed from the agent's MSI config map.
/// </summary>
public class ManagedIdentityConfig
{
    /// <summary>
    /// The type of identity (SystemAssigned or UserAssigned).
    /// </summary>
    [JsonPropertyName("TYPE")]
    public string? Type { get; set; }

    /// <summary>
    /// The name of the PFX certificate in the secret.
    /// </summary>
    [JsonPropertyName("PFX_NAME")]
    public string? PfxName { get; set; }

    /// <summary>
    /// The client ID of the managed identity.
    /// </summary>
    [JsonPropertyName("CLIENT_ID")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The principal ID of the managed identity.
    /// </summary>
    [JsonPropertyName("PRINCIPAL_ID")]
    public string? PrincipalId { get; set; }

    /// <summary>
    /// The tenant ID.
    /// </summary>
    [JsonPropertyName("TENANT_ID")]
    public string? TenantId { get; set; }

    /// <summary>
    /// The Arm resource ID.
    /// </summary>
    [JsonPropertyName("RESOURCE_ID")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// The regional authentication endpoint.
    /// </summary>
    [JsonPropertyName("AUTHENTICATION_ENDPOINT")]
    public string? AuthenticationEndpoint { get; set; }

    /// <summary>
    /// Indicates if this is the system-assigned identity.
    /// </summary>
    public bool IsSystemAssigned => string.IsNullOrEmpty(Type) ||
        string.Equals(Type, "SystemAssigned", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Represents the full MSI configuration from the config map.
/// </summary>
public class ManagedIdentityConfigMap
{
    /// <summary>
    /// The name of the PFX certificate for the system-assigned identity.
    /// </summary>
    [JsonPropertyName("PFX_NAME")]
    public string? PfxName { get; set; }

    /// <summary>
    /// The client ID of the system-assigned managed identity.
    /// </summary>
    [JsonPropertyName("CLIENT_ID")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The principal ID of the system-assigned managed identity.
    /// </summary>
    [JsonPropertyName("PRINCIPAL_ID")]
    public string? PrincipalId { get; set; }

    /// <summary>
    /// The tenant ID.
    /// </summary>
    [JsonPropertyName("TENANT_ID")]
    public string? TenantId { get; set; }

    /// <summary>
    /// The regional authentication endpoint.
    /// </summary>
    [JsonPropertyName("AUTHENTICATION_ENDPOINT")]
    public string? AuthenticationEndpoint { get; set; }

    /// <summary>
    /// List of explicit (user-assigned) identities.
    /// </summary>
    [JsonPropertyName("EXPLICIT_IDENTITIES")]
    public List<ManagedIdentityConfig>? ExplicitIdentities { get; set; }

    /// <summary>
    /// Gets the system-assigned identity info from the root level properties.
    /// </summary>
    public ManagedIdentityConfig GetSystemAssignedIdentity()
    {
        return new ManagedIdentityConfig
        {
            Type = "SystemAssigned",
            PfxName = PfxName,
            ClientId = ClientId,
            PrincipalId = PrincipalId,
            TenantId = TenantId,
            AuthenticationEndpoint = AuthenticationEndpoint,
        };
    }

    /// <summary>
    /// Gets all identities (system-assigned + user-assigned).
    /// </summary>
    public List<ManagedIdentityConfig> GetAllIdentities()
    {
        var identities = new List<ManagedIdentityConfig>
        {
            // Add system-assigned identity
            GetSystemAssignedIdentity()
        };

        // Add user-assigned identities
        if (ExplicitIdentities != null)
        {
            identities.AddRange(ExplicitIdentities);
        }

        return identities;
    }
}
