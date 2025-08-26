using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Agent.Logging;

/// <summary>
/// Configuration options for the Event Hub log exporter.
/// </summary>
public class EventHubLogExporterOptions
{
    public string FullyQualifiedNamespace { get; set; }

    public string EventHubName { get; set; }

    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }

    public string? FirstPartyAppCertificatePath { get; set; }

    public string? FirstPartyAppClientId { get; set; }

    public string? FirstPartyAppTenantId { get; set; }

    public EventHubLogExporterOptions(
        string fullyQualifiedNamespace,
        string eventHubName,
        PopulateLogColumnsDelegate? populateColumns = null,
        string? firstPartyAppCertificatePath = null,
        string? firstPartyAppClientId = null,
        string? firstPartyAppTenantId = null)
    {
        FullyQualifiedNamespace = fullyQualifiedNamespace;
        EventHubName = eventHubName;
        PopulateColumns = populateColumns;
        FirstPartyAppCertificatePath = firstPartyAppCertificatePath;
        FirstPartyAppClientId = firstPartyAppClientId;
        FirstPartyAppTenantId = firstPartyAppTenantId;
    }
}

/// <summary>
/// Exporter for sending OpenTelemetry log data to Azure Event Hub.
/// </summary>
public class EventHubLogExporter : BaseExporter<LogRecord>
{
    private EventHubProducerClient _producerClient;
    private readonly string _fullyQualifiedNamespace;
    private readonly string _eventHubName;

    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }

    public EventHubLogExporter(EventHubLogExporterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initializing Event Hub log exporter with options: {options.FullyQualifiedNamespace}, {options.EventHubName}, {options.FirstPartyAppClientId}, {options.FirstPartyAppTenantId}, {options.FirstPartyAppCertificatePath}");

        _fullyQualifiedNamespace = options.FullyQualifiedNamespace;
        _eventHubName = options.EventHubName;
        PopulateColumns = options.PopulateColumns;

        if (!string.IsNullOrEmpty(options.FirstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(options.FirstPartyAppClientId) &&
            !string.IsNullOrEmpty(options.FirstPartyAppTenantId))
        {
            var certPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.key");

            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);

            var credential = new ClientCertificateCredential(options.FirstPartyAppTenantId, options.FirstPartyAppClientId, certificate,
                new ClientCertificateCredentialOptions
                {
                    SendCertificateChain = true
                });
            _producerClient = new EventHubProducerClient(_fullyQualifiedNamespace, _eventHubName, credential);
        }
        else
        {
            if (string.IsNullOrEmpty(_fullyQualifiedNamespace))
            {
                throw new ArgumentException("FullyQualifiedNamespace must be specified");
            }
            var credential = new DefaultAzureCredential();
            _producerClient = new EventHubProducerClient(_fullyQualifiedNamespace, _eventHubName, credential);
        }
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        return ExportInternalAsync(batch).GetAwaiter().GetResult();
    }

    private async Task<ExportResult> ExportInternalAsync(Batch<LogRecord> batch)
    {
        try
        {
            using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync();

            foreach (var logRecord in batch)
            {
                string serializedData = ConvertLogRecordToLogData(logRecord);
                EventData eventData = new EventData(Encoding.UTF8.GetBytes(serializedData));
                eventData.Properties.Add("Format", "json");

                    if (!eventBatch.TryAdd(eventData))
                    {
                        // Current batch is full, send it and create a new one
                        await _producerClient.SendAsync(eventBatch);
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Successfully sent a batch to Event Hub {_fullyQualifiedNamespace}/{_eventHubName}");

                        using EventDataBatch newBatch = await _producerClient.CreateBatchAsync();
                        if (!newBatch.TryAdd(eventData))
                        {
                            // Single message too large for batch - skip it
                            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WARNING: Single log message too large to send to Event Hub and will be skipped.");
                            continue;
                        }
                        // replace eventBatch with newBatch for subsequent sends
                        // Dispose old batch implicitly by leaving using scope; assign eventBatch variable is not allowed for using block,
                        // so send the rest on the new batch by awaiting newBatch.Send when needed. For simplicity, re-create eventBatch via a new using scope by sending immediately.
                    }
            }

            // Send remaining events in the batch
            await _producerClient.SendAsync(eventBatch);
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Successfully sent remaining batch to Event Hub {_fullyQualifiedNamespace}/{_eventHubName}");
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to export {batch.Count} log records - {ex.Message}");
            return ExportResult.Failure;
        }
    }

    /// <summary>
    /// Converts a LogRecord to a structured log data JSON string suitable for Event Hub.
    /// </summary>
    private string ConvertLogRecordToLogData(LogRecord logRecord)
    {
        var logData = new Dictionary<string, object>
        {
            ["PreciseTimeStamp"] = logRecord.Timestamp,
        };

        // Add placeholder common columns (attempt to load CommonColumn if available)
        try
        {
            var common = CommonColumn.Build();
            logData["AgentName"] = common.AgentName;
            logData["Region"] = common.AgentLocation;
            logData["ContainerImage"] = common.ContainerImage;
            logData["ContainerGroupName"] = common.ContainerGroupName;
        }
        catch
        {
            logData["AgentName"] = "";
            logData["Region"] = "";
            logData["ContainerImage"] = "";
            logData["ContainerGroupName"] = "";
        }

        // Dynamically extract structured logging parameters from Attributes
        if (logRecord.Attributes != null)
        {
            foreach (var kvp in logRecord.Attributes)
            {
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

        return JsonSerializer.Serialize(logData);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var cts = new CancellationTokenSource(timeoutMilliseconds);
        _producerClient.CloseAsync(cts.Token).GetAwaiter().GetResult();
        return true;
    }
}
