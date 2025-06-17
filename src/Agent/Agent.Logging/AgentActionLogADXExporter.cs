using System.Text;
using System.Text.Json;
using Kusto.Data.Common;
using Kusto.Ingest;
using Kusto.Data;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace Agent.Logging;

/// <summary>
/// Exporter for sending agent action log records to Azure Data Explorer (Kusto) WatchTower table.
/// </summary>
public class AgentActionLogADXExporter : IAgentActionLogExporter
{
    private readonly IKustoIngestClient _kustoClient;
    private readonly string _databaseName;
    private readonly string _tableName;
    private readonly LogBuffer? _logBuffer;
    private readonly bool _useBatchProcessing;
    private readonly ILogger<AgentActionLogADXExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentActionLogADXExporter"/> class.
    /// </summary>
    /// <param name="kustoClient">The Kusto ingestion client.</param>
    /// <param name="databaseName">Name of the database to ingest data into.</param>
    /// <param name="tableName">Name of the table to ingest data into.</param>
    /// <param name="logger">Logger for the exporter.</param>
    /// <param name="useBatchProcessing">Whether to use batch processing (defaults to false).</param>
    public AgentActionLogADXExporter(
        string clusteruri,
        string databaseName,
        string tableName,
        string firstPartyAppCertificatePath,
        string firstPartyAppClientId,
        string firstPartyAppTenantId,
        ILogger<AgentActionLogADXExporter> logger)
    {
        Console.WriteLine("AgentActionLogADXExporter initialized with parameters: " +
            $"ClusterUri: {clusteruri}, " +
            $"DatabaseName: {databaseName}, " +
            $"TableName: {tableName}, " +
            $"FirstPartyAppCertificatePath: {firstPartyAppCertificatePath}, " +
            $"FirstPartyAppClientId: {firstPartyAppClientId}, " +
            $"FirstPartyAppTenantId: {firstPartyAppTenantId}");

        if (!string.IsNullOrEmpty(firstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(firstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(firstPartyAppTenantId))
        {
            var certPem = File.ReadAllText($"{firstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{firstPartyAppCertificatePath}/tls.key");
            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(clusteruri)
                        .WithAadApplicationCertificateAuthentication(
                            applicationClientId: firstPartyAppClientId,
                            certificate,
                            authority: firstPartyAppTenantId,
                            sendX5c: true);

            _kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);
            Console.WriteLine("AgentActionLogADXExporter use Kusto ingestion with application certificate authentication.");
        }
        else
        {
            if (string.IsNullOrEmpty(clusteruri))
            {
                throw new ArgumentException("ClusterUri must be specified");
            }
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(clusteruri).WithAadAzCliAuthentication();
            _kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);
            Console.WriteLine("AgentActionLogADXExporter use ingestion with Azure CLI authentication.");
        }

        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exports a single agent action log record to Azure Data Explorer.
    /// </summary>
    /// <param name="logRecord">The agent action log record to export.</param>
    public void Export(AgentActionLogRecord logRecord)
    {
        try
        {
            // Direct processing mode
            var actionData = ConvertLogRecordToKustoData(logRecord);
            Console.WriteLine("AgentActionLogADXExporter Ingesting single agent action log record directly to Kusto.");
            IngestToCluster(_kustoClient, _databaseName, _tableName, actionData);
            Console.WriteLine("AgentActionLogADXExporter Single agent action log record ingestion completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AgentActionLogADXExporter Error exporting agent action log record to Azure Data Explorer: {ex.Message}");
        }
    }

    private void IngestToCluster(IKustoIngestClient client, string databaseName, string tableName, object logData)
    {
        var ingestionProperties = new KustoIngestionProperties(databaseName, tableName)
        {
            Format = DataSourceFormat.json
        };

        var jsonData = JsonSerializer.Serialize(logData);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
        Console.WriteLine($"AgentActionLogADXExporterex Ingesting single record to Kusto: {jsonData}");
        try
        {
            client.IngestFromStreamAsync(stream, ingestionProperties).Wait();
            Console.WriteLine("AgentActionLogADXExporterex Single record ingestion to Kusto completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AgentActionLogADXExporterex Error occurred during single record ingestion to Kusto: {ex.Message}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
            throw; // Rethrow to allow caller to handle
        }
    }

    /// <summary>
    /// Finalizes the exporter and flushes any remaining logs.
    /// </summary>
    public void Shutdown()
    {

    }

    /// <summary>
    /// Converts an AgentActionLogRecord to a structured data object suitable for Kusto ingestion.
    /// </summary>
    private object ConvertLogRecordToKustoData(AgentActionLogRecord logRecord)
    {
        var kustoData = new Dictionary<string, object>
        {
            ["Action"] = logRecord.Action,
            ["Parameter"] = logRecord.Parameter,
            ["Status"] = logRecord.Status,
            ["Duration"] = logRecord.Duration,
            ["Timestamp"] = logRecord.Timestamp,
        };

        return kustoData;
    }

}
