using System.Security.Cryptography.X509Certificates;
using Kusto.Data;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;

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

    private AzureDataExplorerLogger _logger;

    public AzureDataExplorerLoggerProvider(
        string agentName,
        string internalKustoClusterUri,
        string internalKustoDatabaseName,
        string internalKustoTableName,
        string? externalKustoClusterUri,
        string? externalKustoDatabaseName,
        string? externalKustoTableName,
        string? externalKustoIdentityClientId,
        string kustoFirstPartyAppClientId,
        string kustoFirstPartyAppTenantId,
        string kustoFirstPartyAppCertificatePath)
    {
        if (!string.IsNullOrEmpty(internalKustoClusterUri))
        {
            var certPem = File.ReadAllText($"{kustoFirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{kustoFirstPartyAppCertificatePath}/tls.key");

            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);

            var internalKustoConnectionStringBuilder = new KustoConnectionStringBuilder(internalKustoClusterUri)
                        .WithAadApplicationCertificateAuthentication(applicationClientId: kustoFirstPartyAppClientId, certificate, authority: kustoFirstPartyAppTenantId, sendX5c: true);

            _internalKustoClient = KustoIngestFactory.CreateDirectIngestClient(internalKustoConnectionStringBuilder);
            _internalDatabaseName = internalKustoDatabaseName;
            _internalTableName = internalKustoTableName;

        }

        if (!string.IsNullOrEmpty(externalKustoClusterUri)
            && !string.IsNullOrEmpty(externalKustoDatabaseName)
            && !string.IsNullOrEmpty(externalKustoTableName)
            && !string.IsNullOrEmpty(externalKustoIdentityClientId))
        {
            var externalKustoConnectionStringBuilder = new KustoConnectionStringBuilder(externalKustoClusterUri)
                .WithAadUserManagedIdentity(externalKustoIdentityClientId);
            _externalKustoClient = KustoIngestFactory.CreateDirectIngestClient(externalKustoConnectionStringBuilder);
            _externalDatabaseName = externalKustoDatabaseName;
            _externalTableName = externalKustoTableName;
        }

        _agentName = agentName;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return GetLogger();
    }

    public AzureDataExplorerLogger GetLogger()
    {
        if (_logger != null)
        {
            return _logger;
        }

        if (_externalKustoClient == null)
        {
            _logger = new AzureDataExplorerLogger(
                agentName: _agentName,
                internalKustoClient: _internalKustoClient,
                internalDatabaseName: _internalDatabaseName,
                internalTableName: _internalTableName);
        }
        else
        {
            _logger = new AzureDataExplorerLogger(
                agentName: _agentName,
                internalKustoClient: _internalKustoClient,
                internalDatabaseName: _internalDatabaseName,
                internalTableName: _internalTableName,
                externalKustoClient: _externalKustoClient,
                externalDatabaseName: _externalDatabaseName,
                externalTableName: _externalTableName);
        }

        return _logger;
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
