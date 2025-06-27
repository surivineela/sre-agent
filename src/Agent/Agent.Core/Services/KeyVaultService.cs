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
}

public class KeyVaultServiceDisabled : IKeyVaultService
{
    public bool IsEnabled => false;

    public Task<string> ReadSecretAsync(string secretName)
    {
        return Task.FromResult(string.Empty);
    }
}

public class KeyVaultService : IKeyVaultService
{
    public bool IsEnabled { get; set; } = false;
    private readonly SecretClient _secretClient;
    private ILogger<KeyVaultService> _logger;

    public KeyVaultService(
        KeyVaultSettings keyVaultSettings,
        ILogger<KeyVaultService> logger,
        IHostEnvironment hostEnvironment)
    {
        if (string.IsNullOrEmpty(keyVaultSettings.VaultUri))
        {
            return;
        }

        IsEnabled = true;
        _logger = logger;

        if (hostEnvironment.IsDevelopment())
        {
            _secretClient = new SecretClient(new Uri(keyVaultSettings.VaultUri), new DefaultAzureCredential());

        }
        else
        {
            if (string.IsNullOrEmpty(keyVaultSettings.ManagedIdentityClientId))
            {
                throw new ArgumentException("ManagedIdentityClientId must be set in production environment for KeyVaultService.");
            }

            _secretClient = new SecretClient(new Uri(keyVaultSettings.VaultUri), new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                ManagedIdentityResourceId = new Azure.Core.ResourceIdentifier(keyVaultSettings.ManagedIdentityClientId)
            }));
        }
    }

    public async Task<string> ReadSecretAsync(string secretName)
    {
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
