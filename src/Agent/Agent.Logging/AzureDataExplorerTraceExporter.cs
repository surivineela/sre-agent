using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// Delegate for customizing trace data columns.
/// </summary>
/// <param name="activity">The activity containing the telemetry data.</param>
/// <param name="trace">The trace data dictionary to populate with custom columns.</param>
public delegate void PopulateColumnsDelegate(Activity activity, Dictionary<string, object> trace);

/// <summary>
/// Configuration options for the Azure Data Explorer exporter.
/// </summary>
public class AzureDataExplorerExporterOptions
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
    /// Gets or sets a function that can be used to populate custom columns in the trace data.
    /// </summary>
    public PopulateColumnsDelegate? PopulateColumns { get; set; }

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
    /// Initializes a new instance of the <see cref="AzureDataExplorerExporterOptions"/> class.
    /// </summary>
    /// <param name="clusterUri">URI of the Azure Data Explorer cluster.</param>
    /// <param name="databaseName">Name of the database to export data to.</param>
    /// <param name="tableName">Name of the table to export data to.</param>
    /// <param name="populateColumns">Optional function to populate custom columns.</param>
    /// <param name="firstPartyAppCertificatePath">Optional path to the First Party App certificate.</param>
    /// <param name="firstPartyAppClientId">Optional First Party App client ID.</param>
    /// <param name="firstPartyAppTenantId">Optional First Party App tenant ID.</param>
    public AzureDataExplorerExporterOptions(
        string clusterUri,
        string databaseName,
        string tableName,
        PopulateColumnsDelegate? populateColumns = null,
        string? firstPartyAppCertificatePath = "",
        string? firstPartyAppClientId = "",
        string? firstPartyAppTenantId = "")
    {
        ClusterUri = clusterUri;
        DatabaseName = databaseName;
        TableName = tableName;
        PopulateColumns = populateColumns;
        FirstPartyAppCertificatePath = firstPartyAppCertificatePath;
        FirstPartyAppClientId = firstPartyAppClientId;
        FirstPartyAppTenantId = firstPartyAppTenantId;
    }
}

/// <summary>
/// Exporter for sending OpenTelemetry trace data to Azure Data Explorer (Kusto).
/// </summary>
public class AzureDataExplorerExporter : BaseExporter<Activity>
{
    private IKustoIngestClient _kustoClient;
    private readonly string _databaseName;
    private readonly string _tableName;
    /// <summary>
    /// Gets or sets a function that can be used to populate custom columns in the trace data.
    /// </summary>
    public PopulateColumnsDelegate? PopulateColumns { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDataExplorerExporter"/> class using the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the exporter.</param>
    /// <param name="logger">Logger instance for the exporter.</param>
    public AzureDataExplorerExporter(AzureDataExplorerExporterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initializing Azure Data Explorer exporter with options: {options.ClusterUri}, {options.DatabaseName}, {options.TableName}, {options.FirstPartyAppClientId}, {options.FirstPartyAppTenantId}, {options.FirstPartyAppCertificatePath}");

        _databaseName = options.DatabaseName;
        _tableName = options.TableName;
        PopulateColumns = options.PopulateColumns;

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

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var logDataList = new List<object>();

            foreach (var activity in batch)
            {
                var traceData = ConvertActivityToTraceData(activity);
                logDataList.Add(traceData);
            }

            FlushBatch(logDataList);
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to export {batch.Count} activities - {ex.Message}");
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
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to flush batch of {logDataList.Count} trace records - {ex.Message}");

            // Re-throw to preserve original behavior for caller
            throw;
        }
    }

    /// <summary>
    /// Converts an Activity to a structured trace data object suitable for logging.
    /// </summary>
    private object ConvertActivityToTraceData(Activity activity)
    {
        var traceData = new Dictionary<string, object>
        {
            ["TraceId"] = activity.TraceId.ToString(),
            ["SpanId"] = activity.SpanId.ToString(),
            ["ParentSpanId"] = activity.ParentSpanId.ToString(),
            ["SpanName"] = activity.DisplayName,
            ["SpanKind"] = activity.Kind.ToString(),
            ["StartTime"] = activity.StartTimeUtc,
            ["EndTime"] = activity.StartTimeUtc.AddTicks(activity.Duration.Ticks),
            ["Duration"] = activity.Duration.TotalMilliseconds,
            ["Attributes"] = JsonSerializer.Serialize(activity.TagObjects), // Serialize attributes to JSON string
            ["Events"] = JsonSerializer.Serialize(activity.Events), // Serialize events to JSON string
            ["Status"] = activity.Status.ToString()
        };

        // Use the PopulateColumns plugin function if provided
        if (PopulateColumns != null)
        {
            PopulateColumns.Invoke(activity, traceData);
        }

        return traceData;
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
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Ingesting {recordCount} trace records to Azure Data Explorer");

            // Ingest the batch into Kusto - this will be queued if using QueuedIngestClient
            _kustoClient.IngestFromStream(stream, ingestionProperties);

            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Successfully queued {recordCount} trace records for ingestion");
        }
        catch (Exception ex)
        {
            var recordCount = logDataBatch is ICollection<object> collection ? collection.Count : logDataBatch.Count();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to ingest {recordCount} trace records - {ex.Message}");

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

