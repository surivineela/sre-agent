using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace FirstPartyAgent.Core.Extensions;

public static class KeyVaultConfigurationExtension
{
    public static string GetPlatformKeyVaultSettingFromEnvironment(string settingName)
    {
        string keyVaultPrefix = "AppSettings__Core__Azure__FirstParty__KeyVaultConfiguration_";
        return Environment.GetEnvironmentVariable($"{keyVaultPrefix}{settingName}") ?? string.Empty;
    }

    public static IConfigurationBuilder LoadKeyVaultAppSettings(this IConfigurationBuilder configBuilder, bool isDevelopment = true)
    {
        string kvEndpointUri = GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri");
        string kvMsiResourceUri = GetPlatformKeyVaultSettingFromEnvironment("Identity");

        if (!isDevelopment && !string.IsNullOrWhiteSpace(kvEndpointUri))
        {
            if (!string.IsNullOrWhiteSpace(kvMsiResourceUri))
            {
                configBuilder.AddAzureKeyVault(new Uri(kvEndpointUri),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions() // CodeQL [SM05137] This is non-production code which is deprecated and not deployed.
                {
                    ManagedIdentityResourceId = new Azure.Core.ResourceIdentifier(kvMsiResourceUri)
                }));
            }
            else
            {
                configBuilder.AddAzureKeyVault(new Uri(kvEndpointUri), new DefaultAzureCredential()); // CodeQL [SM05137] This is non-production code which is deprecated and not deployed.
            }
        }
        return configBuilder;
    }
}
