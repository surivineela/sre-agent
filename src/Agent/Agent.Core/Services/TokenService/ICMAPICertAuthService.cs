// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using System.Security.Cryptography.X509Certificates;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SREAgent.Incidents.IcM.Interface;

namespace Agent.Core.Services;
public class ICMAPICertAuthService : IICMCertAuthService
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<ICMAPICertAuthService> _logger;
    private ICMAPISettings _icmApiSettings;

    public ICMAPICertAuthService(IAuthenticationService authService, IOptionsMonitor<IncidentManagementSettings> monitor, ILogger<ICMAPICertAuthService> logger)
    {
        _authService = authService;
        _logger = logger;

        _icmApiSettings = monitor.CurrentValue.ICMAPI;
        monitor.OnChange(newConfig =>
        {
            _icmApiSettings = newConfig.ICMAPI;
        });
    }


    public X509Certificate2 GetClientCertificate()
    {
        // Try to load certificate from KeyVault first
        if (!string.IsNullOrWhiteSpace(_icmApiSettings.CertificateKeyVaultUri) && !string.IsNullOrWhiteSpace(_icmApiSettings.CertificateKeyVaultSecretName))
        {
            return LoadCertFromKeyVault();
        }
        // Fallback to local certificate store
        else if (!string.IsNullOrWhiteSpace(_icmApiSettings.CertificateSubjectName))
        {
            return LoadCertFromLocalStore();
        }
        else
        {
            throw new Exception("No certificate configuration found for ICMAPIClient. Please configure either CertificateKeyVaultUri with CertificateKeyVaultSecretName or CertificateSubjectName.");
        }
    }

    private X509Certificate2 LoadCertFromKeyVault()
    {
        _logger.LogInternalInformation("Trying to loaded certificate from KeyVault for ICMAPIClient.");
        string keyVaultUri = _icmApiSettings.CertificateKeyVaultUri;
        string certKvSecretName = _icmApiSettings.CertificateKeyVaultSecretName;
        string managedIdentityClientId = _icmApiSettings.ManagedIdentityClientId;

        var certificate = CertLoader.LoadCertFromKeyVault(_authService, keyVaultUri, certKvSecretName, managedIdentityClientId, null, _logger);
        _logger.LogInternalInformation("Successfully loaded certificate from KeyVault for ICMAPIClient.");
        return certificate;
    }

    private X509Certificate2 LoadCertFromLocalStore()
    {
        _logger.LogInternalInformation("Trying to loaded certificate from local store for ICMAPIClient.");
        // Open the "My" certificate store in the current user's context.
        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly);

            // Locate the certificate by matching the subject name.
            var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, _icmApiSettings.CertificateSubjectName, validOnly: false);
            if (certificates is null || certificates.Count == 0)
            {
                throw new Exception($"Certificate with subject matching '{_icmApiSettings.CertificateSubjectName}' not found.");
            }
            // Use the first matching certificate.
            _logger.LogInternalInformation("Successfully loaded certificate from local store for ICMAPIClient.");
            return certificates[0];
        }
    }

    public static bool IsCertAuthConfigured(ICMAPISettings icmApiSettings)
    {
        return !string.IsNullOrWhiteSpace(icmApiSettings.CertificateSubjectName) ||
        (!string.IsNullOrWhiteSpace(icmApiSettings.CertificateKeyVaultUri) && !string.IsNullOrWhiteSpace(icmApiSettings.CertificateKeyVaultSecretName));
    }
}
