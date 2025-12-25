using System.Security.Cryptography.X509Certificates;
using Agent.Common.ApiModels;

namespace Session.Identity.Services;

public class ManagedIdentityService : IManagedIdentityService
{
    private readonly ILogger<ManagedIdentityService> _logger;
    private readonly object _lock = new();
    private ManagedIdentityConfiguration? _configuration;

    public ManagedIdentityService(ILogger<ManagedIdentityService> logger)
    {
        _logger = logger;
    }

    public bool HasManagedIdentity
    {
        get
        {
            lock (_lock)
            {
                return _configuration != null;
            }
        }
    }

    public void StoreManagedIdentity(ManagedIdentityInfo managedIdentity)
    {
        lock (_lock)
        {
            // Dispose of the old certificate if one exists
            _configuration?.Certificate.Dispose();

            var certificate = X509CertificateLoader.LoadPkcs12(
                managedIdentity.PfxBytes,
                string.Empty,
                X509KeyStorageFlags.EphemeralKeySet);

            _configuration = new ManagedIdentityConfiguration
            {
                ManagedIdentity = managedIdentity,
                Certificate = certificate
            };

            _logger.LogInformation(
                "Managed identity stored successfully. Type: {Type}, ClientId: {ClientId}, TenantId: {TenantId}, Subject: {Subject}, Thumbprint: {Thumbprint}",
                managedIdentity.Type ?? "SystemAssigned",
                managedIdentity.ClientId,
                managedIdentity.TenantId,
                certificate.Subject,
                certificate.Thumbprint);
        }
    }

    public ManagedIdentityConfiguration? GetManagedIdentityConfiguration()
    {
        lock (_lock)
        {
            return _configuration;
        }
    }
}
