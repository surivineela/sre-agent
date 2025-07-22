// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using System.Security.Cryptography.X509Certificates;
using Agent.Core.Models;
using Azure.Identity;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Helpers;

public class KustoConfig
{
    public string ClusterUri { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public KustoAuthenticationType AuthType { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string AuthorityHost { get; set; } = string.Empty;
    public string ApplicationClientId { get; set; } = string.Empty;
    public string ApplicationCertificate { get; set; } = string.Empty;
    public string ManagedIdentityClientId { get; set; } = string.Empty;
}

public class KustoServiceClientFactory
{
    private readonly ILogger<KustoServiceClient> _logger;
    public KustoServiceClientFactory(ILogger<KustoServiceClient> logger)
    {
        _logger = logger;
    }
    public KustoServiceClient CreateKustoService(KustoConfig config)
    {
        return new KustoServiceClient(_logger, config);
    }
}

public class KustoServiceClient
{
    private readonly ILogger<KustoServiceClient> _logger;
    private readonly ICslQueryProvider _queryProvider;
    private readonly string _databaseName;

    public KustoServiceClient(ILogger<KustoServiceClient> logger, KustoConfig config)
    {
        _logger = logger;
        _databaseName = config.DatabaseName;

        _logger.LogInformation($"Authentication type: {config.AuthType}");
        if (config.AuthType == KustoAuthenticationType.ManagedIdentity)
        {
            _queryProvider = KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(config.ClusterUri).WithAadSystemManagedIdentity());
        }
        else if (config.AuthType == KustoAuthenticationType.UAMI)
        {
            _queryProvider = KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(config.ClusterUri).WithAadUserManagedIdentity(config.ManagedIdentityClientId));
        }
        else if (config.AuthType == KustoAuthenticationType.App)
        {
            var clientCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(config.ApplicationCertificate));
            var credential = new ClientCertificateCredential(
                        config.Authority,
                        config.ApplicationClientId,
                        clientCertificate,
                        new ClientCertificateCredentialOptions()
                        {
                            AuthorityHost = new Uri(config.AuthorityHost),
                            SendCertificateChain = true
                        });
            _queryProvider = KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(config.ClusterUri).WithAadAzureTokenCredentialsAuthentication(credential));
        }
        else
        {
            _queryProvider = KustoClientFactory.CreateCslQueryProvider(new KustoConnectionStringBuilder(config.ClusterUri).WithAadUserPromptAuthentication());
        }
    }

    public async Task<IDataReader> PerformQueryAsync(string query, DateTime? now_override = null)
    {
        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        if (now_override.HasValue)
        {
            properties.SetOption(ClientRequestProperties.OptionQueryNow, now_override.Value);
        }
        _logger.LogInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");
        return await _queryProvider.ExecuteQueryAsync(_databaseName, query, properties);
    }

    public async Task<IEnumerable<T>> PerformQueryAsync<T>(string query)
    {
        var properties = new ClientRequestProperties()
        {
            ClientRequestId = "Operational Agent;" + Guid.NewGuid().ToString()
        };
        _logger.LogInformation($"Executing query: {query}, request Id: {properties.ClientRequestId}");
        var result = await _queryProvider.ExecuteQueryAsync(_databaseName, query, properties);

        return result.ToEnumerable<T>();
    }
}

