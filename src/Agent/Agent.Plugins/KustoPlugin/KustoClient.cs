// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Tools;
using Agent.Framework.Reasoning.Models;
using Azure.Identity;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using KustoData = Kusto.Data;

namespace Agent.Plugins.Kusto;

/// <summary>
/// Performs Kusto queries. This client caches connection objects and should be reused as much as possible.
/// </summary>
public class KustoClient
{
    private readonly ILogger<KustoClient> _logger;

    private readonly KustoConnector _kustoSettings;
    public KustoConnector KustoSettings => _kustoSettings;

    private readonly ConcurrentDictionary<string, ICslQueryProvider> _queryProviders = new ConcurrentDictionary<string, ICslQueryProvider>(StringComparer.OrdinalIgnoreCase);

    public KustoClient(ILogger<KustoClient> logger, KustoConnector kustoSettings)
    {
        _logger = logger;
        _kustoSettings = kustoSettings;

        _logger.LogInternalInformation($"Kusto Authentication type: {_kustoSettings.Auth.AuthenticationType}");
    }

    public async Task<IDataReader> PerformQueryAsync(string clusterUri, string database, string query, CancellationToken cancellationToken = default)
    {
        try
        {
            ICslQueryProvider queryProvider = GetQueryProvider(clusterUri);

            ClientRequestProperties properties = new ClientRequestProperties()
            {
                ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
            };

            _logger.LogExternalInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");

            return await queryProvider.ExecuteQueryAsync(database, query, properties, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"An error occurred while executing PerformQueryAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IDataReader> PerformQueryWithParametersAsync(string clusterUri, string database, string query, Dictionary<string, object> parameters)
    {
        ICslQueryProvider queryProvider = GetQueryProvider(clusterUri);

        ClientRequestProperties properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };

        ApplyParameters(properties, parameters);

        _logger.LogExternalInformation($"Executing query: {query}, request Id: {properties.ClientRequestId} with parameters");

        return await queryProvider.ExecuteQueryAsync(database, query, properties);
    }

    private ICslQueryProvider GetQueryProvider(string clusterUri)
    {
        return _queryProviders.GetOrAdd(clusterUri, (newUri => GetClient(newUri)));
    }

    private ClientCertificateCredential GetClientCertificateCredentials()
    {
        X509Certificate2 clientCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(_kustoSettings.Auth.ApplicationCertificate));

        return new ClientCertificateCredential(
                    _kustoSettings.Auth.Authority,
                    _kustoSettings.Auth.ApplicationClientId,
                    clientCertificate,
                    new ClientCertificateCredentialOptions()
                    {
                        AuthorityHost = new Uri(_kustoSettings.Auth.AuthorityHost),
                        SendCertificateChain = true
                    });
    }

    private DefaultAzureCredential GetUserManagedIdentityCredentials()
    {
        var defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(_kustoSettings.Auth.ManagedIdentityClientId))
        {
            defaultAzureCredentialOptions.ManagedIdentityClientId = _kustoSettings.Auth.ManagedIdentityClientId;
            _logger.LogInternalInformation($"Kusto using MI with ClientId: {_kustoSettings.Auth.ManagedIdentityClientId}");
        }
        else if (!string.IsNullOrEmpty(_kustoSettings.Auth.ManagedIdentityResourceId))
        {
            _logger.LogInternalInformation($"Kusto using MI with ResourceId: {_kustoSettings.Auth.ManagedIdentityResourceId}");
            defaultAzureCredentialOptions.ManagedIdentityResourceId = new Azure.Core.ResourceIdentifier(_kustoSettings.Auth.ManagedIdentityResourceId);
        }
        else
        {
            throw new InvalidOperationException("Either ManagedIdentityClientId or ManagedIdentityResourceId must be provided.");
        }
        return new DefaultAzureCredential(defaultAzureCredentialOptions);
    }

    private ICslQueryProvider GetClient(string clusterUri)
    {
        return _kustoSettings.Auth.AuthenticationType switch
        {
            ConnectorAuthType.ManagedIdentity => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadSystemManagedIdentity()),
            ConnectorAuthType.UAMI => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadAzureTokenCredentialsAuthentication(GetUserManagedIdentityCredentials())),
            ConnectorAuthType.App => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadAzureTokenCredentialsAuthentication(GetClientCertificateCredentials())),
            _ => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadUserPromptAuthentication())
        };
    }

    public async Task<KustoQueryResult> ExecuteClusterKustoQuery(
           string cluster,
           string database,
           string fullQuery)
    {
        cluster = cluster.Replace(".kusto.windows.net", "");
        cluster = cluster.Replace("https://", "");
        var reader = await PerformQueryAsync($"https://{cluster}.kusto.windows.net", database, fullQuery);
        return new KustoQueryResult(reader, fullQuery);
    }
    private static void ApplyParameters(ClientRequestProperties properties, Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (parameters == null || parameters.Count() < 1)
        {
            return;
        }

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
                    catch
                    {
                        // Fall back to string representation if serialization fails
                        properties.SetParameter(param.Key, param.Value.ToString() ?? string.Empty);
                    }
                    break;
            }
        }
    }
}
