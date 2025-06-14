using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Kusto.Data.Common;
using Kusto.Ingest;
using Kusto.Data;
using Microsoft.Extensions.Logging;

namespace Agent.Logging;

/// <summary>
/// Exporter for sending agent action log records to Azure Data Explorer (Kusto) WatchTower table.
/// </summary>
public class AgentActionLogADXExporter : IAgentActionLogExporter
{
    private readonly IKustoIngestClient _kustoClient;
    private readonly string _databaseName;
    private readonly string _tableName;
    private readonly DataSourceFormat _format;
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
        ILogger<AgentActionLogADXExporter> logger,
        bool useBatchProcessing = false)
    {
        var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(clusteruri)
        .WithAadAzCliAuthentication();
        _kustoClient = KustoIngestFactory.CreateDirectIngestClient(kustoConnectionStringBuilder);
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _useBatchProcessing = useBatchProcessing;
        _format = useBatchProcessing ? DataSourceFormat.multijson : DataSourceFormat.json;

        if (useBatchProcessing)
        {
            _logBuffer = new LogBuffer();
        }
    }

    /// <summary>
    /// Exports a single agent action log record to Azure Data Explorer.
    /// </summary>
    /// <param name="logRecord">The agent action log record to export.</param>
    public void Export(AgentActionLogRecord logRecord)
    {
        try
        {
            if (_useBatchProcessing && _logBuffer != null)
            {
                // Batch processing mode
                var actionData = ConvertLogRecordToKustoData(logRecord);
                _logBuffer.Logs.Enqueue(actionData);
            }
            else
            {
                // Direct processing mode
                var actionData = ConvertLogRecordToKustoData(logRecord);
                IngestToCluster(_kustoClient, _databaseName, _tableName, actionData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting agent action log record to Azure Data Explorer");
        }
    }

    /// <summary>
    /// Exports multiple agent action log records to Azure Data Explorer.
    /// </summary>
    /// <param name="logRecords">The collection of agent action log records to export.</param>
    public void Export(IEnumerable<AgentActionLogRecord> logRecords)
    {
        try
        {
            if (_useBatchProcessing && _logBuffer != null)
            {
                // Batch processing mode
                foreach (var logRecord in logRecords)
                {
                    var actionData = ConvertLogRecordToKustoData(logRecord);
                    _logBuffer.Logs.Enqueue(actionData);
                }
            }
            else
            {
                // Direct processing mode for each record
                foreach (var logRecord in logRecords)
                {
                    var actionData = ConvertLogRecordToKustoData(logRecord);
                    IngestToCluster(_kustoClient, _databaseName, _tableName, actionData);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting agent action log records to Azure Data Explorer");
        }
    }

    /// <summary>
    /// Flushes the current batch of logs to Azure Data Explorer.
    /// </summary>
    public void FlushBatch()
    {
        if (_logBuffer == null || _logBuffer.Logs.Count == 0)
        {
            return;
        }

        try
        {
            var logDataList = new List<object>();
            while (_logBuffer.Logs.TryDequeue(out var logData))
            {
                logDataList.Add(logData);
            }

            IngestBatchToCluster(logDataList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing batch to Azure Data Explorer");
        }
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
            ["Exception"] = logRecord.Exception ?? string.Empty,
        };

        return kustoData;
    }

    private void IngestBatchToCluster(IEnumerable<object> logDataBatch)
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

        // Ingest the batch into Kusto
        _kustoClient.IngestFromStreamAsync(stream, ingestionProperties).Wait();
    }

    private void IngestToCluster(IKustoIngestClient client, string databaseName, string tableName, object logData)
    {
        var ingestionProperties = new KustoIngestionProperties(databaseName, tableName)
        {
            Format = DataSourceFormat.json
        };

        var jsonData = JsonSerializer.Serialize(logData);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

        client.IngestFromStreamAsync(stream, ingestionProperties).Wait();
    }

    /// <summary>
    /// Finalizes the exporter and flushes any remaining logs.
    /// </summary>
    public void Shutdown()
    {
        // Ensure any remaining logs are flushed
        if (_useBatchProcessing)
        {
            FlushBatch();
        }
    }
}
