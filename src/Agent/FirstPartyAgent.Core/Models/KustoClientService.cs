// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using System.Security.Cryptography.X509Certificates;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Azure.Identity;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Helpers;

public class KustoClientService
{
    private readonly ILogger<KustoClientService> _logger;
    private readonly KustoSettings _kustoSettings;
    private readonly Dictionary<string, ICslQueryProvider> _regionsToQueryProviders = new();
    private readonly Dictionary<string, KustoCluster> _regionsToClusters = new();

    public KustoClientService(ILogger<KustoClientService> logger, KustoSettings kustoSettings)
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

    public async Task<IDataReader> PerformQueryWithParametersAsync(string queryText, Dictionary<string, object> parameters, string region)
    {
        ValidateRegion(region);
        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };

        ApplyParameters(properties, parameters);

        _logger.LogInformation($"Executing query: {queryText}, request Id: {properties.ClientRequestId} with parameters");

        var result = await _regionsToQueryProviders[region].ExecuteQueryAsync(_regionsToClusters[region].Database, queryText, properties);
        return result;
    }

    public async Task<IDataReader> PerformQueryWithParametersAsync(string queryText, Dictionary<string, object> parameters, KustoCluster cluster)
    {
        var client = GetNewClient(cluster);
        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        ApplyParameters(properties, parameters);
        _logger.LogInformation($"Executing query: {queryText}, request Id: {properties.ClientRequestId} with parameters");
        var result = await client.ExecuteQueryAsync(cluster.Database, queryText, properties);
        return result;
    }

    private void ValidateRegion(string region)
    {
        if (!_regionsToQueryProviders.ContainsKey(region))
        {
            throw new ArgumentException($"Region {region} is not supported");
        }
    }

    private void ApplyParameters(ClientRequestProperties properties, Dictionary<string, object> parameters)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (parameters == null || parameters.Count() < 1) return;

        foreach (var param in parameters)
        {
            switch (param.Value)
            {
                case null:
                    properties.SetParameter(param.Key, string.Empty);
                    break;
                case string stringValue:
                    properties.SetParameter(param.Key, stringValue);
                    break;
                case int intValue:
                    properties.SetParameter(param.Key, intValue);
                    break;
                case long longValue:
                    properties.SetParameter(param.Key, longValue);
                    break;
                case double doubleValue:
                    properties.SetParameter(param.Key, doubleValue);
                    break;
                case bool boolValue:
                    properties.SetParameter(param.Key, boolValue);
                    break;
                case DateTime dateTimeValue:
                    properties.SetParameter(param.Key, dateTimeValue);
                    break;
                case TimeSpan timeSpanValue:
                    properties.SetParameter(param.Key, timeSpanValue);
                    break;
                case Guid guidValue:
                    properties.SetParameter(param.Key, guidValue);
                    break;
                case decimal decimalValue:
                    properties.SetParameter(param.Key, (double)decimalValue);
                    break;
                case float floatValue:
                    properties.SetParameter(param.Key, (double)floatValue);
                    break;
                case byte[] byteArrayValue:
                    properties.SetParameter(param.Key, Convert.ToBase64String(byteArrayValue));
                    break;
                case IEnumerable<object> enumerable:
                    // Convert collection to JSON string
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(enumerable);
                    properties.SetParameter(param.Key, jsonString);
                    break;
                default:
                    // For complex types, serialize to JSON
                    try
                    {
                        var complexJsonString = System.Text.Json.JsonSerializer.Serialize(param.Value);
                        properties.SetParameter(param.Key, complexJsonString);
                    }
                    catch (Exception ex)
                    {
                        // Fall back to string representation if serialization fails
                        properties.SetParameter(param.Key, param.Value.ToString() ?? string.Empty);
                    }
                    break;
            }
        }
    }
}

