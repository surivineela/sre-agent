using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Kusto.Data.Common;
using Kusto.Ingest;
using Kusto.Data;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Logging;

/// <summary>
/// Delegate for customizing log data columns.
/// </summary>
/// <param name="logRecord">The log record containing the telemetry data.</param>
/// <param name="logData">The log data dictionary to populate with custom columns.</param>
public delegate void PopulateLogColumnsDelegate(LogRecord logRecord, Dictionary<string, object> logData);

/// <summary>
/// Configuration options for the Azure Data Explorer log exporter.
/// </summary>
public class AzureDataExplorerLogExporterOptions
{
    /// <summary>
    /// Gets or sets the URI of the Azure Data Explorer cluster.
    /// </summary>
    public string ClusterUri { get; set; }

    /// <summary>
    /// Gets or sets the name of the database to export data to.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table to export data to.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets a function that can be used to populate custom columns in the log data.
    /// </summary>
    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }

    /// <summary>
    /// Gets or sets the path to the First Party App certificate.
    /// </summary>
    public string? FirstPartyAppCertificatePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the First Party App client ID.
    /// </summary>
    public string? FirstPartyAppClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the First Party App tenant ID.
    /// </summary>
    public string? FirstPartyAppTenantId { get; set; } = "";

    /// <summary>
    /// Gets or sets the managed identity client ID for user-assigned managed identity authentication.
    /// </summary>
    public string? Identity { get; set; } = "";

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDataExplorerLogExporterOptions"/> class.
    /// </summary>
    /// <param name="clusterUri">URI of the Azure Data Explorer cluster.</param>
    /// <param name="databaseName">Name of the database to export data to.</param>
    /// <param name="tableName">Name of the table to export data to.</param>
    /// <param name="populateColumns">Optional function to populate custom columns.</param>
    /// <param name="firstPartyAppCertificatePath">Optional path to the First Party App certificate.</param>
    /// <param name="firstPartyAppClientId">Optional First Party App client ID.</param>
    /// <param name="firstPartyAppTenantId">Optional First Party App tenant ID.</param>
    /// <param name="identity">Optional managed identity client ID for user-assigned managed identity authentication.</param>
    public AzureDataExplorerLogExporterOptions(
        string clusterUri,
        string databaseName,
        string tableName,
        PopulateLogColumnsDelegate? populateColumns = null,
        string? firstPartyAppCertificatePath = "",
        string? firstPartyAppClientId = "",
        string? firstPartyAppTenantId = "",
        string? identity = "")
    {
        ClusterUri = clusterUri;
        DatabaseName = databaseName;
        TableName = tableName;
        PopulateColumns = populateColumns;
        FirstPartyAppCertificatePath = firstPartyAppCertificatePath;
        FirstPartyAppClientId = firstPartyAppClientId;
        FirstPartyAppTenantId = firstPartyAppTenantId;
        Identity = identity;
    }
}

/// <summary>
/// Exporter for sending OpenTelemetry log data to Azure Data Explorer (Kusto).
/// </summary>
public class AzureDataExplorerLogExporter : BaseExporter<LogRecord>
{
    private IKustoIngestClient _kustoClient;
    private readonly string _databaseName;
    private readonly string _tableName;
    private readonly CommonColumn _commonColumns;

    /// <summary>
    /// Gets or sets a function that can be used to populate custom columns in the log data.
    /// </summary>
    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDataExplorerLogExporter"/> class using the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the exporter.</param>
    public AzureDataExplorerLogExporter(AzureDataExplorerLogExporterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initializing Azure Data Explorer log exporter with options: {options.ClusterUri}, {options.DatabaseName}, {options.TableName}, {options.FirstPartyAppClientId}, {options.FirstPartyAppTenantId}, {options.FirstPartyAppCertificatePath}");

        _databaseName = options.DatabaseName;
        _tableName = options.TableName;
        PopulateColumns = options.PopulateColumns;

        // Build common columns once during initialization
        try
        {
            _commonColumns = CommonColumn.Build();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Successfully loaded common columns: AgentName={_commonColumns.AgentName}, Region={_commonColumns.AgentLocation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WARNING: Failed to load common columns during initialization - {ex.Message}");
            // Create empty common columns as fallback
            _commonColumns = new CommonColumn { AgentName = "", AgentLocation = "", ContainerImage = "", ContainerGroupName = "" };
        }

        if (!string.IsNullOrEmpty(options.FirstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(options.FirstPartyAppClientId) &&
            !string.IsNullOrEmpty(options.FirstPartyAppTenantId))
        {
            var certPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.key");

            // Use a using statement to ensure the certificate is properly disposed
            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);

            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(options.ClusterUri)
                            .WithAadApplicationCertificateAuthentication(
                                applicationClientId: options.FirstPartyAppClientId,
                                certificate,
                                authority: options.FirstPartyAppTenantId,
                                sendX5c: true);

            _kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);
        }
        else if (!string.IsNullOrEmpty(options.Identity))
        {
            if (string.IsNullOrEmpty(options.ClusterUri))
            {
                throw new ArgumentException("ClusterUri must be specified");
            }
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(options.ClusterUri)
                .WithAadUserManagedIdentity(options.Identity);

            _kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);
        }
        else
        {
            if (string.IsNullOrEmpty(options.ClusterUri))
            {
                throw new ArgumentException("ClusterUri must be specified");
            }
            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(options.ClusterUri).WithAadAzCliAuthentication();

            _kustoClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnectionStringBuilder);
        }
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            using var scope = SuppressInstrumentationScope.Begin();

            var logDataList = new List<object>();

            foreach (var logRecord in batch)
            {
                var logData = ConvertLogRecordToLogData(logRecord);
                logDataList.Add(logData);
            }

            FlushBatch(logDataList);
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to export {batch.Count} log records - {ex.Message}");
            return ExportResult.Failure;
        }
    }

    /// <summary>
    /// Flushes the current batch of logs to Azure Data Explorer.
    /// </summary>
    /// <param name="logDataList">The list of log data objects to flush.</param>
    public void FlushBatch(List<object> logDataList)
    {
        if (logDataList == null || logDataList.Count == 0)
        {
            return;
        }

        try
        {
            IngestBatchToCluster(logDataList);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to flush batch of {logDataList.Count} log records - {ex.Message}");

            // Re-throw to preserve original behavior for caller
            throw;
        }
    }

    /// <summary>
    /// Converts a LogRecord to a structured log data object suitable for logging.
    /// </summary>
    private object ConvertLogRecordToLogData(LogRecord logRecord)
    {
        var logData = new Dictionary<string, object>
        {
            ["PreciseTimeStamp"] = logRecord.Timestamp,
            ["LogLevel"] = logRecord.LogLevel.ToString(),
            ["Message"] = logRecord.FormattedMessage ?? string.Empty,
            ["Exception"] = logRecord.Exception?.ToString() ?? string.Empty,
        };

        // Add common columns from cached values
        logData["AgentName"] = _commonColumns.AgentName;
        logData["Region"] = _commonColumns.AgentLocation;
        logData["ContainerImage"] = _commonColumns.ContainerImage;
        logData["ContainerGroupName"] = _commonColumns.ContainerGroupName;

        // Dynamically extract structured logging parameters from Attributes
        if (logRecord.Attributes != null)
        {
            foreach (var kvp in logRecord.Attributes)
            {
                // Skip the original message template key
                if (kvp.Key != "{OriginalFormat}")
                {
                    logData[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
            }
        }

        // Use the PopulateColumns plugin function if provided
        if (PopulateColumns != null)
        {
            PopulateColumns.Invoke(logRecord, logData);
        }

        return logData;
    }

    private void IngestBatchToCluster(IEnumerable<object> logDataBatch)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(logDataBatch);

            var ingestionProperties = new KustoIngestionProperties(_databaseName, _tableName)
            {
                Format = DataSourceFormat.multijson // Specify JSON format for batch
            };

            // Create a memory stream from the JSON data
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

            // Ensure the stream is not empty
            if (stream.Length == 0)
            {
                return;
            }

            stream.Position = 0; // Reset the position to the start of the stream

            // Log the ingestion attempt
            var recordCount = logDataBatch is ICollection<object> collection ? collection.Count : logDataBatch.Count();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Ingesting {recordCount} log records to Azure Data Explorer");

            // Ingest the batch into Kusto - this will be queued if using QueuedIngestClient
            _kustoClient.IngestFromStream(stream, ingestionProperties);

            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Successfully queued {recordCount} log records for ingestion");
        }
        catch (Exception ex)
        {
            var recordCount = logDataBatch is ICollection<object> collection ? collection.Count : logDataBatch.Count();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to ingest {recordCount} log records - {ex.Message}");

            // Re-throw the exception to preserve the original behavior for the caller
            throw;
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        _kustoClient.Dispose();
        return true;
    }
}

/// <summary>
/// Extension methods for configuring Azure Data Explorer log exporter.
/// </summary>
public static class AzureDataExplorerLogExporterExtensions
{
    /// <summary>
    /// Adds Azure Data Explorer log exporter to the OpenTelemetry logging pipeline.
    /// </summary>
    /// <param name="builder">The OpenTelemetry logging builder.</param>
    /// <param name="configure">Optional action to configure the exporter options.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerLogExporter(
        this LoggerProviderBuilder builder,
        Action<IServiceProvider, AzureDataExplorerLogExporterOptions>? configure = null)
    {
        return builder.AddProcessor(sp =>
        {
            // Create default options
            var options = new AzureDataExplorerLogExporterOptions(
                clusterUri: "",
                databaseName: "",
                tableName: "");

            // Apply configuration if provided
            configure?.Invoke(sp, options);

            // Create the exporter directly
            var exporter = new AzureDataExplorerLogExporter(options);

            // Use BatchLogRecordExportProcessor to wrap the exporter with batching functionality
            return new BatchLogRecordExportProcessor(exporter);
        });
    }

    /// <summary>
    /// Adds Azure Data Explorer log exporter to the OpenTelemetry logging pipeline with specific options.
    /// </summary>
    /// <param name="builder">The OpenTelemetry logging builder.</param>
    /// <param name="options">The exporter options.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerLogExporter(
        this LoggerProviderBuilder builder,
        AzureDataExplorerLogExporterOptions options)
    {
        return builder.AddProcessor(sp =>
        {
            var exporter = new AzureDataExplorerLogExporter(options);
            return new BatchLogRecordExportProcessor(exporter);
        });
    }

    /// <summary>
    /// Adds Azure Data Explorer log exporter to the OpenTelemetry logging pipeline with a factory function.
    /// </summary>
    /// <param name="builder">The OpenTelemetry logging builder.</param>
    /// <param name="exporterFactory">Factory function to create the exporter using service provider.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerLogExporter(
        this LoggerProviderBuilder builder,
        Func<IServiceProvider, AzureDataExplorerLogExporter> exporterFactory)
    {
        return builder.AddProcessor(sp =>
        {
            var exporter = exporterFactory(sp);
            return new BatchLogRecordExportProcessor(exporter);
        });
    }
}
