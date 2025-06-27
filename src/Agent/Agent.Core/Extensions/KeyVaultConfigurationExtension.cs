using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.OperationalAgent.Core.Extensions;
public static class KeyVaultConfigurationExtension
{
    public static string GetPlatformKeyVaultSettingFromEnvironment(string settingName)
    {
        string keyVaultPrefix = "AppSettings__Core__Azure__FirstParty__KeyVaultConfiguration_";
        return Environment.GetEnvironmentVariable($"{keyVaultPrefix}{settingName}") ?? string.Empty;
    }
}
