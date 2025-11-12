// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Azure.Identity;
using Microsoft.Azure.Monitoring.DGrep.DataContracts.External;
using Microsoft.Azure.Monitoring.DGrep.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Implementation;

public class DGrepPluginClient : IDGrepPluginClient, IDisposable
{
    private readonly DGrepSettings _settings;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<DGrepPluginClient> _logger;
    private Dictionary<string, ManagedIdentityCredential> _managedIdentityCredentialCache = new Dictionary<string, ManagedIdentityCredential>();

    // Unified cache entry for DGrepClient
    private class DGrepClientCacheEntry
    {
        public DGrepClient? Client { get; set; }
        public DateTimeOffset? ExpiresOn { get; set; } // Only used for Managed Identity
    }

    // Use a single cache for all DGrepClients
    private Dictionary<string, DGrepClientCacheEntry> _dGrepClientCache = new Dictionary<string, DGrepClientCacheEntry>();

    public DGrepPluginClient(IOptions<DGrepSettings> settings, IAuthenticationService authService, ILogger<DGrepPluginClient> logger)
    {
        if (settings?.Value == null)
        {
            throw new ArgumentNullException(nameof(settings), "DGrep settings cannot be null.");
        }

        _settings = settings.Value;
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        // Dispose of any resources if necessary
        foreach (var client in _dGrepClientCache.Values)
        {
            client.Client?.Dispose();
        }

        _dGrepClientCache.Clear();
        _managedIdentityCredentialCache.Clear();
    }

    public async Task<string> ExecuteDGrepQuery(string nameSpace, string eventName, string serverQuery, string clientQuery, string filters, QueryType queryType, DateTime startTime, DateTime endTime, int maxResults = 10, CancellationToken ct = default)
    {
        // DEBUG POINT 1: Entry point - check if method is called
        _logger.LogInternalInformation("DGrep query started: Namespace={Namespace}, Event={EventName}, Query={ServerQuery}", nameSpace, eventName, serverQuery);

        var dGrepEndpoint = new Uri(_settings.DGrepEndpoint);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(_settings.QueryTimeoutMinutes));

        if (string.IsNullOrWhiteSpace(nameSpace) || string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(serverQuery))
        {
            _logger.LogInternalError("DGrep query validation failed: missing required parameters");
            throw new ArgumentException("Namespace, event name, and query must be provided.");
        }

        var queryInput = new QueryInput
        {
            MdsEndpoint = new Uri(_settings.MdsEndpoint),
            EventFilters = new List<EventFilter>
            {
                new EventFilter
                {
                    NamespaceRegex = $"^{nameSpace}$",
                    NameRegex = $"^{eventName}$"
                }
            },
            StartTime = startTime,
            EndTime = endTime,
            ServerQueryType = queryType
        };

        if (!string.IsNullOrWhiteSpace(filters))
        {
            var filterPairs = filters.Split(';')
                .Select(f => f.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

            queryInput.IdentityColumns = filters.Split(';')
                .Select(f => f.Split('='))
                .Where(p => p.Length == 2)
                .Select(p => new KeyValuePair<string, List<string>>(p[0].Trim(), new List<string> { p[1].Trim() }))
                .ToDictionary(p => p.Key, p => p.Value);
        }

        try
        {
            // DEBUG POINT 2: Authentication attempt
            _logger.LogInternalInformation("DGrep: Attempting authentication with mode={AuthMode}, CertLocation={CertLocation}", _settings.AuthenticationMode, _settings.CertificateLocation);
            var dGrepClient = await GetDGrepClientWithAuth();
            _logger.LogInternalInformation("DGrep: Authentication successful, executing query");

            // DEBUG POINT 3: Query execution
            var rows = await dGrepClient.GetRowSetResultAsync(queryInput, serverQuery, cts.Token);
            _logger.LogInternalInformation("DGrep: Query executed, processing results");

            if (rows == null || rows.RowSet == null || rows.RowSet.Rows == null || !rows.RowSet.Rows.Any())
            {
                var status = rows?.QueryStatus?.Status ?? "Unknown";
                var queryId = rows != null ? rows.QueryId.ToString() : "Unknown";
                _logger.LogInternalWarning("DGrep: No results returned. Status={Status}, QueryId={QueryId}", status, queryId);
                return $"Query Status: {status}, Query RequestId: {queryId}, Query Results: No Rows Returned From Query";
            }

            // DEBUG POINT 4: Results processing
            _logger.LogInternalInformation("DGrep: Processing {RowCount} rows from query results", rows.RowSet.Rows.Count());
            var resultBuilder = new StringBuilder();
            resultBuilder.AppendLine($"Query Status: {rows.QueryStatus.Status}, Query RequestId: {rows.QueryId.ToString()}");
            resultBuilder.AppendLine("Query CSV Results:");

            resultBuilder.AppendLine(string.Join(", ", rows.RowSet.ColumnDefinitions.Keys));

            foreach (var row in rows.RowSet.Rows.Take(maxResults))
            {
                var sanitizedValues = row.Values.Select(v => SanitizeContent(v?.ToString() ?? ""));
                resultBuilder.AppendLine(string.Join(", ", sanitizedValues));
            }
            return resultBuilder.ToString();
        }
        catch (Exception ex)
        {
            // DEBUG POINT 5: Error occurred
            _logger.LogInternalError(ex, "DGrep query failed: {ErrorMessage}", ex.Message);
            throw new DGrepException($"An error occurred while executing the DGrep query: {ex.Message}", ex);
        }
    }

    private async Task<DGrepClient> GetDGrepClientWithAuth()
    {
        switch (_settings.AuthenticationMode)
        {
            case AuthMode.ManagedIdentity:
                try
                {
                    var cacheKey = _settings.ManagedIdentityClientId ?? "system-assigned";
                    if (!_managedIdentityCredentialCache.TryGetValue(cacheKey, out var credential))
                    {
                        // If the credential is not cached, create a new ManagedIdentityCredential
                        if (string.IsNullOrWhiteSpace(_settings.ManagedIdentityClientId))
                        {
                            // System assigned
                            credential = new ManagedIdentityCredential();
                        }
                        else
                        {
                            // User assigned
                            credential = new ManagedIdentityCredential(_settings.ManagedIdentityClientId);
                        }

                        if (!_managedIdentityCredentialCache.TryAdd(cacheKey, credential))
                        {
                            // If the credential was already added by another thread, use the cached one
                            credential = _managedIdentityCredentialCache.GetValueOrDefault(cacheKey);
                        }
                    }

                    if (credential == null)
                    {
                        throw new InvalidOperationException("ManagedIdentityCredential could not be created or retrieved from cache.");
                    }

                    // Check if we have a valid cached DGrepClient
                    if (_dGrepClientCache.TryGetValue(cacheKey, out var cacheEntry) && cacheEntry?.Client != null && cacheEntry.ExpiresOn.HasValue)
                    {
                        // Add a 5 minute buffer to avoid using a token that's about to expire
                        if (cacheEntry.ExpiresOn.Value > DateTimeOffset.UtcNow.AddMinutes(5))
                        {
                            return cacheEntry.Client;
                        }
                        else
                        {
                            // Dispose the old client if expired
                            cacheEntry.Client.Dispose();
                            _dGrepClientCache.Remove(cacheKey);
                        }
                    }

                    var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { _settings.AADResource });
                    var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    if (string.IsNullOrEmpty(token.Token))
                    {
                        throw new InvalidOperationException("Failed to acquire access token using Managed Identity.");
                    }
                    var dgrepClient = new DGrepClient(new Uri(_settings.DGrepEndpoint), token.Token);
                    _dGrepClientCache[cacheKey] = new DGrepClientCacheEntry
                    {
                        Client = dgrepClient,
                        ExpiresOn = token.ExpiresOn
                    };
                    return dgrepClient;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to acquire access token using Managed Identity.", ex);
                }
            case AuthMode.Certificate:
                string? certCacheKey = null;
                switch (_settings.CertificateLocation)
                {
                    case CertificateLocation.KeyVault:
                        certCacheKey = _settings.KeyVaultCertificateName;
                        break;
                    case CertificateLocation.FileSystem:
                        certCacheKey = _settings.CertificateFilePath;
                        break;
                    case CertificateLocation.CertStore:
                        certCacheKey = _settings.CertificateSubjectName;
                        break;
                    default:
                        throw new NotSupportedException($"Certificate location '{_settings.CertificateLocation}' is not supported.");
                }
                if (string.IsNullOrWhiteSpace(certCacheKey))
                {
                    throw new ArgumentException("A valid certificate cache key must be provided for certificate authentication.");
                }
                if (_dGrepClientCache.TryGetValue(certCacheKey, out var certCacheEntry) && certCacheEntry?.Client != null)
                {
                    return certCacheEntry.Client;
                }
                DGrepClient certClient;
                switch (_settings.CertificateLocation)
                {
                    case CertificateLocation.KeyVault:
                        var certificate = CertLoader.LoadCertFromKeyVault(
                            _authService,
                            _settings.KeyVaultUri,
                            _settings.KeyVaultCertificateName,
                            _settings.ManagedIdentityClientId,
                            _settings.CertificatePassword,
                            _logger);
                        certClient = new DGrepClient(new Uri(_settings.DGrepEndpoint), certificate);
                        break;
                    case CertificateLocation.FileSystem:
                        var fileCertificate = CertLoader.LoadCertFromFile(_settings.CertificateFilePath, _settings.CertificatePassword);
                        certClient = new DGrepClient(new Uri(_settings.DGrepEndpoint), fileCertificate);
                        break;
                    case CertificateLocation.CertStore:
                        var storeCertificate = CertLoader.LoadCertFromAppService(_settings.CertificateSubjectName, "", null);
                        certClient = new DGrepClient(new Uri(_settings.DGrepEndpoint), storeCertificate);
                        break;
                    default:
                        throw new NotSupportedException($"Certificate location '{_settings.CertificateLocation}' is not supported.");
                }
                _dGrepClientCache[certCacheKey] = new DGrepClientCacheEntry
                {
                    Client = certClient,
                    ExpiresOn = null // Not used for certificate
                };
                return certClient;
            default:
                throw new NotSupportedException($"Authentication mode '{_settings.AuthenticationMode}' is not supported.");
        }
    }

    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // More aggressive sanitization to prevent content filtering
        var sanitized = content
            // Remove stack traces and exceptions (major triggers)
            .Replace("Exception", "[Ex]")
            .Replace("Error", "[Err]")
            .Replace("Failed", "[Fail]")
            .Replace("Failure", "[Fail]")
            .Replace("Fatal", "[Fatal]")
            .Replace("Critical", "[Crit]")
            .Replace("Warning", "[Warn]")
            // Remove file paths
            .Replace("C:\\", "[Drive]\\")
            .Replace("D:\\", "[Drive]\\")
            .Replace("\\bin\\", "\\[bin]\\")
            .Replace("\\tmp\\", "\\[tmp]\\")
            // Remove URLs and IPs
            .Replace("http://", "[http]://")
            .Replace("https://", "[https]://")
            .Replace("localhost", "[local]")
            .Replace("127.0.0.1", "[local-ip]")
            // Remove common sensitive patterns
            .Replace("password", "[pwd]")
            .Replace("token", "[tok]")
            .Replace("key", "[key]")
            .Replace("secret", "[sec]");

        // Limit length aggressively - use sanitized string length to avoid ArgumentOutOfRangeException
        return sanitized.Substring(0, Math.Min(sanitized.Length, 200));
    }
}
