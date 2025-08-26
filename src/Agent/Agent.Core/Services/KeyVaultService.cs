using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OperationalAgent.Core.Extensions;

namespace Agent.Core.Services;

public interface IKeyVaultService
{
    Task<string> ReadSecretAsync(string secretName);
    bool IsEnabled { get; }
    string KeyVaultUri { get; }
}

public class KeyVaultServiceDisabled : IKeyVaultService
{
    public bool IsEnabled => false;
    public string KeyVaultUri => string.Empty;

    public Task<string> ReadSecretAsync(string secretName)
    {
        return Task.FromResult(string.Empty);
    }
}

public class KeyVaultService : IKeyVaultService
{
    public bool IsEnabled { get; set; } = false;
    public string KeyVaultUri { get; set; } = string.Empty;
    private readonly SecretClient? _secretClient;
    private ILogger<KeyVaultService> _logger;

    public KeyVaultService(
        KeyVaultSettings keyVaultSettings,
        ILogger<KeyVaultService> logger,
        IAuthenticationService authService)
    {
        _logger = logger;

        if (string.IsNullOrEmpty(keyVaultSettings.VaultUri))
        {
            _secretClient = null;
            return;
        }

        IsEnabled = true;
        KeyVaultUri = keyVaultSettings.VaultUri;

        if (string.IsNullOrEmpty(keyVaultSettings.ManagedIdentityClientId))
        {
            throw new ArgumentException("ManagedIdentityClientId must be set in production environment for KeyVaultService.");
        }

        var cred = authService.Get1PAgentKeyVaultCredential(keyVaultSettings.ManagedIdentityClientId);
        _secretClient = new SecretClient(new Uri(keyVaultSettings.VaultUri), cred);
    }

    public async Task<string> ReadSecretAsync(string secretName)
    {
        if (_secretClient == null)
        {
            _logger.LogInternalError("KeyVaultService is not initialized. Cannot read secret.");
            throw new InvalidOperationException("KeyVaultService is not initialized. Cannot read secret.");
        }

        try
        {
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to read secret '{secretName}': {ex.Message}", ex);
            throw new Exception($"Failed to read secret '{secretName}': {ex.Message}", ex);
        }
    }
}
