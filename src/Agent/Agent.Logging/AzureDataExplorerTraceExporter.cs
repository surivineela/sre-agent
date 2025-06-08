using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Kusto.Data.Common;
using Kusto.Ingest;

using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// Exporter for sending OpenTelemetry trace data to Azure Data Explorer (Kusto).
/// </summary>
public class AzureDataExplorerExporter : BaseExporter<Activity>
{
    private readonly IKustoIngestClient _kustoClient;
    private readonly string _databaseName;
    private readonly string _tableName;
    private readonly DataSourceFormat _format;
    private readonly LogBuffer? _logBuffer;
    private readonly bool _useBatchProcessing;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDataExplorerExporter"/> class.
    /// </summary>
    /// <param name="kustoClient">The Kusto ingestion client.</param>
    /// <param name="databaseName">Name of the database to ingest data into.</param>
    /// <param name="tableName">Name of the table to ingest data into.</param>
    /// <param name="useBatchProcessing">Whether to use batch processing (defaults to false).</param>
    public AzureDataExplorerExporter(
        IKustoIngestClient kustoClient,
        string databaseName,
        string tableName,
        bool useBatchProcessing = false)
    {
        _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _useBatchProcessing = useBatchProcessing;
        _format = useBatchProcessing ? DataSourceFormat.multijson : DataSourceFormat.json;

        if (useBatchProcessing)
        {
            _logBuffer = new LogBuffer();
        }
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            if (_useBatchProcessing && _logBuffer != null)
            {
                // Batch processing mode
                foreach (var activity in batch)
                {
                    var traceData = ConvertActivityToTraceData(activity);
                    _logBuffer.Logs.Enqueue(traceData);
                }

                FlushBatch();
            }
            else
            {
                // Direct processing mode for each activity
                foreach (var activity in batch)
                {
                    var traceData = ConvertActivityToTraceData(activity);
                    IngestToCluster(_kustoClient, _databaseName, _tableName, traceData);
                }
            }

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting telemetry to Azure Data Explorer: {ex}");
            return ExportResult.Failure;
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

        var logDataList = new List<object>();
        while (_logBuffer.Logs.TryDequeue(out var logData))
        {
            logDataList.Add(logData);
        }

        IngestBatchToCluster(logDataList);
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
            ["Status"] = activity.Status.ToString(),
            ["ThreadId"] = activity.GetTagItem("thread.id")?.ToString() ?? string.Empty,
            ["OperationName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "operation.name").Value?.ToString() ?? string.Empty,
            ["ToolName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "tool.name").Value?.ToString() ?? string.Empty,
            ["AgentName"] = activity.TagObjects.FirstOrDefault(t => t.Key == "agent.name").Value?.ToString() ?? string.Empty,
        };

        // Add status information if available
        if (!string.IsNullOrEmpty(activity.StatusDescription))
        {
            traceData["StatusDescription"] = activity.StatusDescription;
        }

        // Add all tags (attributes)
        foreach (var tag in activity.TagObjects)
        {
            traceData[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        // Add all baggages
        foreach (var baggage in activity.Baggage)
        {
            traceData[$"Baggage_{baggage.Key}"] = baggage.Value ?? string.Empty;
        }

        return traceData;
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

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // Ensure any remaining logs are flushed
        if (_useBatchProcessing)
        {
            FlushBatch();
        }
        return true;
    }
}
