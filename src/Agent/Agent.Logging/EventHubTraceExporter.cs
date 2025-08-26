using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// Configuration options for the Azure Data Explorer exporter.
/// </summary>
public class EventHubTraceExporterOptions
{
    public string FullyQualifiedNamespace { get; set; }

    public string EventHubName { get; set; }

    public PopulateColumnsDelegate? PopulateColumns { get; set; }

    public string? FirstPartyAppCertificatePath { get; set; }

    public string? FirstPartyAppClientId { get; set; }

    public string? FirstPartyAppTenantId { get; set; }

    public EventHubTraceExporterOptions(
        string fullyQualifiedNamespace,
        string eventHubName,
        PopulateColumnsDelegate? populateColumns = null,
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
/// Exporter for sending OpenTelemetry trace data to Azure Event Hub.
/// </summary>
public class EventHubTraceExporter : BaseExporter<Activity>
{
    private EventHubProducerClient _producerClient;
    private readonly string _fullyQualifiedNamespace;
    private readonly string _eventHubName;

    public PopulateColumnsDelegate? PopulateColumns { get; set; }

    public EventHubTraceExporter(EventHubTraceExporterOptions options, TokenCredential cred)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrEmpty(_fullyQualifiedNamespace))
        {
            throw new ArgumentException("FullyQualifiedNamespace must be specified");
        }

        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initializing Event Hub exporter with options: {options.FullyQualifiedNamespace}, {options.EventHubName}, {options.FirstPartyAppClientId}, {options.FirstPartyAppTenantId}, {options.FirstPartyAppCertificatePath}");

        _fullyQualifiedNamespace = options.FullyQualifiedNamespace;
        _eventHubName = options.EventHubName;
        PopulateColumns = options.PopulateColumns;

        _producerClient = new EventHubProducerClient(_fullyQualifiedNamespace, _eventHubName, cred);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        return ExportInternalAsync(batch).GetAwaiter().GetResult();
    }

    private async Task<ExportResult> ExportInternalAsync(Batch<Activity> batch)
    {
        try
        {
            using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync();

            foreach (var activity in batch)
            {
                string serializedData = ConvertActivityToTraceData(activity);
                EventData eventData = new EventData(Encoding.UTF8.GetBytes(serializedData));
                eventData.Properties.Add("Format", "json");

                eventBatch.TryAdd(eventData);
            }

            await _producerClient.SendAsync(eventBatch);
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to export {batch.Count} activities - {ex.Message}");
            return ExportResult.Failure;
        }
    }


    /// <summary>
    /// Converts an Activity to a structured trace data object suitable for logging.
    /// </summary>
    private string ConvertActivityToTraceData(Activity activity)
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

        return JsonSerializer.Serialize(traceData);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var cts = new CancellationTokenSource(timeoutMilliseconds);
        _producerClient.CloseAsync(cts.Token).GetAwaiter().GetResult();
        return true;
    }
}

