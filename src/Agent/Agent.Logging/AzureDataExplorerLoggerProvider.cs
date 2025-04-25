using System;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Kusto.Data;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Agent.Logging;
public class AzureDataExplorerLoggerProvider : ILoggerProvider
{
    private readonly IKustoIngestClient _internalKustoClient;
    private readonly string _internalDatabaseName;
    private readonly string _internalTableName;

    private readonly IKustoIngestClient? _externalKustoClient;
    private readonly string? _externalDatabaseName;
    private readonly string? _externalTableName;

    private readonly string _agentName;

    public AzureDataExplorerLoggerProvider(
        string agentName,
        IAuthenticationService authenticationService,
        KustoClusterConfiguration internalKustoClusterConfiguration,
        KustoClusterConfiguration? externalKustoClusterConfiguration)
    {
        if (!string.IsNullOrEmpty(internalKustoClusterConfiguration.ClusterUri))
        {
            var internalKustoConnectionStringBuilder = new KustoConnectionStringBuilder(internalKustoClusterConfiguration.ClusterUri)
                        .WithAadAzureTokenCredentialsAuthentication(authenticationService.GetKustoCredential());

            _internalKustoClient = KustoIngestFactory.CreateDirectIngestClient(internalKustoConnectionStringBuilder);
            _internalDatabaseName = internalKustoClusterConfiguration.DatabaseName;
            _internalTableName = internalKustoClusterConfiguration.TableName;

        }

        if (externalKustoClusterConfiguration != null)
        {
            var externalKustoConnectionStringBuilder = new KustoConnectionStringBuilder(externalKustoClusterConfiguration.ClusterUri).WithAadUserManagedIdentity(externalKustoClusterConfiguration.Identity);
            _externalKustoClient = KustoIngestFactory.CreateDirectIngestClient(externalKustoConnectionStringBuilder);
            _externalDatabaseName = externalKustoClusterConfiguration.DatabaseName;
            _externalTableName = externalKustoClusterConfiguration.TableName;
        }

        _agentName = agentName;
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (_externalKustoClient == null)
        {
            return new AzureDataExplorerLogger(
                agentName: _agentName,
                internalKustoClient: _internalKustoClient,
                internalDatabaseName: _internalDatabaseName,
                internalTableName: _internalTableName);
        }

        return new AzureDataExplorerLogger(
            agentName: _agentName,
            internalKustoClient: _internalKustoClient,
            internalDatabaseName: _internalDatabaseName,
            internalTableName: _internalTableName,
            externalKustoClient: _externalKustoClient,
            externalDatabaseName: _externalDatabaseName,
            externalTableName: _externalTableName);
    }

    public void Dispose()
    {
        _internalKustoClient.Dispose();

        if (_externalKustoClient != null)
        {
            _externalKustoClient.Dispose();
        }
    }
}
