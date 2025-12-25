using System.Security.Cryptography.X509Certificates;
using Agent.Common.ApiModels;

namespace Session.Identity.Services;

public interface IManagedIdentityService
{
    /// <summary>
    /// Stores a managed identity configuration.
    /// </summary>
    void StoreManagedIdentity(ManagedIdentityInfo managedIdentity);

    /// <summary>
    /// Gets the stored managed identity configuration.
    /// </summary>
    ManagedIdentityConfiguration? GetManagedIdentityConfiguration();

    /// <summary>
    /// Checks if a managed identity is configured.
    /// </summary>
    bool HasManagedIdentity { get; }
}

public class ManagedIdentityConfiguration
{
    public required ManagedIdentityInfo ManagedIdentity { get; set; }
    public required X509Certificate2 Certificate { get; set; }
}
