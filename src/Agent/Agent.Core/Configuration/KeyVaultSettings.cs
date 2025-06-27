using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OperationalAgent.Core.Extensions;

namespace Agent.Core.Configuration;
public class KeyVaultSettings
{
    public string VaultUri
    {
        get
        {
            string envValue = KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri");
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }
            else
            {
                return _vaultUri;
            }
        }
        set
        {
            _vaultUri = value;
        }
    }
    private string _vaultUri = string.Empty;

    public string ManagedIdentityClientId
    {
        get
        {
            string envValue = KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("Identity");
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }
            else
            {
                return _managedIdentityClientId;
            }
        }
        set
        {
            _managedIdentityClientId = value;
        }
    }

    private string _managedIdentityClientId = string.Empty;
}
