// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Certificate authentication service for ICM that loads certificates from Azure Key Vault
/// using Data Connector configuration settings.
/// </summary>
public class IcmDataConnectorCertAuthService : IIcmCertAuthService
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<IcmDataConnectorCertAuthService> _logger;
    private readonly DataConnectorInstanceSettings _connectorSettings;

    public IcmDataConnectorCertAuthService(
        IAuthenticationService authService,
        DataConnectorInstanceSettings connectorSettings,
        ILogger<IcmDataConnectorCertAuthService> logger)
    {
        _authService = authService;
        _connectorSettings = connectorSettings;
        _logger = logger;
    }

    public X509Certificate2 GetClientCertificate()
    {
        if (string.IsNullOrWhiteSpace(_connectorSettings.KeyVaultUri))
        {
            throw new InvalidOperationException("KeyVaultUri is not configured for the ICM data connector.");
        }

        var (keyVaultUrl, certificateName, version) = ParseKeyVaultCertificateUri(_connectorSettings.KeyVaultUri);

        _logger.LogInternalInformation(
            "Loading ICM certificate from Key Vault via Data Connector. Vault: {VaultUrl}, Certificate: {CertName}, Version: {Version}, Identity: {Identity}",
            keyVaultUrl, certificateName, version ?? "latest", _connectorSettings.Identity);

        // Determine managed identity - "System" means system-assigned, otherwise it's user-assigned client ID
        string managedIdentityClientId = GetManagedIdentityClientId(_connectorSettings.Identity);

        var certificate = CertLoader.LoadCertFromKeyVault(
            _authService,
            keyVaultUrl,
            certificateName,
            managedIdentityClientId,
            certPassword: null,
            log: _logger,
            version: version);

        _logger.LogInternalInformation("Successfully loaded ICM certificate from Data Connector configuration.");
        return certificate;
    }

    /// <summary>
    /// Parses a Key Vault certificate URI to extract the vault URL, certificate name, and optional version.
    /// Supports formats:
    /// - Full URI: https://myvault.vault.azure.net/certificates/mycert/version
    /// - Full URI without version: https://myvault.vault.azure.net/certificates/mycert
    /// </summary>
    private static (string keyVaultUrl, string certificateName, string? version) ParseKeyVaultCertificateUri(string keyVaultUri)
    {
        // Pattern to match Key Vault certificate URIs
        // Format: https://{vault-name}.vault.azure.net/certificates/{cert-name}[/{version}]
        var pattern = @"^(https://[^/]+\.vault\.azure\.net)/certificates/([^/]+)(?:/([^/]+))?$";
        var match = Regex.Match(keyVaultUri, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var vaultUrl = match.Groups[1].Value;
            var certName = match.Groups[2].Value;
            var version = match.Groups[3].Success ? match.Groups[3].Value : null;
            return (vaultUrl, certName, version);
        }

        throw new InvalidOperationException(
            $"Invalid Key Vault certificate URI format: '{keyVaultUri}'. " +
            "Expected format: https://{{vault-name}}.vault.azure.net/certificates/{{cert-name}}[/{{version}}]");
    }

    /// <summary>
    /// Converts the Identity setting to a managed identity client ID.
    /// "System" or empty string means system-assigned managed identity (returns empty string).
    /// Otherwise, the value is treated as a user-assigned managed identity client ID.
    /// </summary>
    private static string GetManagedIdentityClientId(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) ||
            identity.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return identity;
    }

    /// <summary>
    /// Checks if certificate authentication is configured via a Data Connector with Source=Agent.
    /// </summary>
    public static bool IsCertAuthConfigured(DataConnectorInstanceSettings? connectorSettings)
    {
        return connectorSettings != null &&
               !string.IsNullOrWhiteSpace(connectorSettings.KeyVaultUri) &&
               connectorSettings.DataConnectorType.Equals("icm", StringComparison.OrdinalIgnoreCase) &&
               connectorSettings.Source == DataConnectorSource.Agent;
    }
}
