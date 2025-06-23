// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Agent.Logging;
using Azure.Identity;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using KustoData=Kusto.Data;

namespace Agent.Plugins.Kusto;

/// <summary>
/// Performs Kusto queries. This client caches connection objects and should be reused as much as possible.
/// </summary>
public class KustoClient
{
    private readonly ILogger<KustoClient> _logger;
    private readonly KustoAuthSettings _kustoAuthSettings;
    private readonly ConcurrentDictionary<string, ICslQueryProvider> _queryProviders = new ConcurrentDictionary<string, ICslQueryProvider>(StringComparer.OrdinalIgnoreCase);

    public KustoClient(ILogger<KustoClient> logger, KustoSettings kustoSettings)
    {
        _logger = logger;
        _kustoAuthSettings = kustoSettings.Auth;

        _logger.LogInternalInformation($"Authentication type: {_kustoAuthSettings.AuthenticationType}");
    }

    public async Task<IDataReader> PerformQueryAsync(string clusterUri, string database, string query)
    {
        ICslQueryProvider queryProvider = GetQueryProvider(clusterUri);

        ClientRequestProperties properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };

        _logger.LogExternalInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");

        return await queryProvider.ExecuteQueryAsync(database, query, properties);
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
        X509Certificate2 clientCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(_kustoAuthSettings.ApplicationCertificate));

        return new ClientCertificateCredential(
                    _kustoAuthSettings.Authority,
                    _kustoAuthSettings.ApplicationClientId,
                    clientCertificate,
                    new ClientCertificateCredentialOptions()
                    {
                        AuthorityHost = new Uri(_kustoAuthSettings.AuthorityHost),
                        SendCertificateChain = true
                    });
    }

    private DefaultAzureCredential GetUserManagedIdentityCredentials()
    {
        var defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(_kustoAuthSettings.ManagedIdentityClientId))
        {
            defaultAzureCredentialOptions.ManagedIdentityClientId = _kustoAuthSettings.ManagedIdentityClientId;
        } else if(!string.IsNullOrEmpty(_kustoAuthSettings.ManagedIdentityResourceId))
        {
            defaultAzureCredentialOptions.ManagedIdentityResourceId = new Azure.Core.ResourceIdentifier(_kustoAuthSettings.ManagedIdentityResourceId);
        } else
        {

            throw new InvalidOperationException("Either ManagedIdentityClientId or ManagedIdentityResourceId must be provided.");
        }
        return new DefaultAzureCredential(defaultAzureCredentialOptions);
    }

    private ICslQueryProvider GetClient(string clusterUri)
    {
        return _kustoAuthSettings.AuthenticationType switch
        {
            KustoAuthenticationType.ManagedIdentity => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadSystemManagedIdentity()),
            KustoAuthenticationType.UAMI => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadAzureTokenCredentialsAuthentication(GetUserManagedIdentityCredentials())),
            KustoAuthenticationType.App => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadAzureTokenCredentialsAuthentication(GetClientCertificateCredentials())),
            _ => KustoData.Net.Client.KustoClientFactory.CreateCslQueryProvider(new KustoData.KustoConnectionStringBuilder(clusterUri).WithAadUserPromptAuthentication())
        };
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

