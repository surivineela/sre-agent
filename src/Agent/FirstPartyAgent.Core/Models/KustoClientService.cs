using System.Data;
using System.Security.Cryptography.X509Certificates;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Azure.Identity;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Agent.Core.Helpers;

public class KustoClientService
{
    private readonly ILogger<KustoClientService> _logger;
    private readonly KustoSettings _kustoSettings;
    private readonly Dictionary<string, ICslQueryProvider> _regionsToQueryProviders = new();
    private readonly Dictionary<string, KustoCluster> _regionsToClusters = new();

    public KustoClientService(KustoSettings kustoSettings, ILogger<KustoClientService> logger)
    {
        _logger = logger;
        _kustoSettings = kustoSettings;
        _regionsToClusters = _kustoSettings.Clusters.ToDictionary(c => c.Region, c => c);

        _logger.LogInformation($"Authentication type: {_kustoSettings.Auth.AuthenticationType}");
        foreach (string region in _regionsToClusters.Keys)
        {
            _regionsToQueryProviders[region] = GetNewClient(_regionsToClusters[region]);
        }
    }

    private ICslQueryProvider GetNewClient(KustoCluster cluster)
    {
        ICslQueryProvider? queryProvider = null;
        if (_kustoSettings.Auth.AuthenticationType == KustoAuthenticationType.ManagedIdentity)
        {
            queryProvider = Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(cluster.ClusterUri).WithAadSystemManagedIdentity());
        }
        else if (_kustoSettings.Auth.AuthenticationType == KustoAuthenticationType.UAMI)
        {
            queryProvider = Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(cluster.ClusterUri).WithAadUserManagedIdentity(_kustoSettings.Auth.ManagedIdentityClientId));
        }
        else if (_kustoSettings.Auth.AuthenticationType == KustoAuthenticationType.App)
        {
            var clientCertificate = new X509Certificate2(Convert.FromBase64String(_kustoSettings.Auth.ApplicationCertificate), string.Empty, X509KeyStorageFlags.EphemeralKeySet);
            var credential = new ClientCertificateCredential(
                        _kustoSettings.Auth.Authority,
                        _kustoSettings.Auth.ApplicationClientId,
                        clientCertificate,
                        new ClientCertificateCredentialOptions()
                        {
                            AuthorityHost = new Uri(_kustoSettings.Auth.AuthorityHost),
                            SendCertificateChain = true
                        });
            queryProvider = Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(cluster.ClusterUri).WithAadAzureTokenCredentialsAuthentication(credential));
        }
        else
        {
            queryProvider = Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(cluster.ClusterUri).WithAadUserPromptAuthentication());
        }

        return queryProvider;
    }

    public async Task<IDataReader> PerformQueryAsync(string query, string region)
    {
        ValidateRegion(region);

        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        _logger.LogInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");
        return await _regionsToQueryProviders[region].ExecuteQueryAsync(_regionsToClusters[region].Database, query, properties);
    }

    public async Task<IDataReader> PerformQueryAsync(KustoCluster cluster, string query)
    {
        var client = GetNewClient(cluster);
        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        _logger.LogInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");
        return await client.ExecuteQueryAsync(cluster.Database, query, properties);
    }

    public async Task<IEnumerable<T>> PerformQueryAsync<T>(string query, string region)
    {
        ValidateRegion(region);

        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        _logger.LogInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");
        var result = await _regionsToQueryProviders[region].ExecuteQueryAsync(_regionsToClusters[region].Database, query, properties);

        return result.ToEnumerable<T>();
    }

    private void ValidateRegion(string region)
    {
        if (!_regionsToQueryProviders.ContainsKey(region))
        {
            throw new ArgumentException($"Region {region} is not supported");
        }
    }
}
