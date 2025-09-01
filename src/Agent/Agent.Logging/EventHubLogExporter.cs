using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Agent.Logging;

public class EventHubLogExporterOptions
{
    public string FullyQualifiedNamespace { get; set; }
    public string EventHubName { get; set; }
    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }
    // MaxBatchSizeInBytes removed: exporter uses default EventHub batch sizing
    public int? FlushIntervalMilliseconds { get; set; }
    public int? MaxQueueSize { get; set; }
    public string? FirstPartyAppCertificatePath { get; set; }
    public string? FirstPartyAppClientId { get; set; }
    public string? FirstPartyAppTenantId { get; set; }

    public EventHubLogExporterOptions(string fullyQualifiedNamespace, string eventHubName, PopulateLogColumnsDelegate? populateColumns = null,
        string? firstPartyAppCertificatePath = null, string? firstPartyAppClientId = null, string? firstPartyAppTenantId = null)
    {
        FullyQualifiedNamespace = fullyQualifiedNamespace;
        EventHubName = eventHubName;
        PopulateColumns = populateColumns;
        FirstPartyAppCertificatePath = firstPartyAppCertificatePath;
        FirstPartyAppClientId = firstPartyAppClientId;
        FirstPartyAppTenantId = firstPartyAppTenantId;
    }
}

public class EventHubLogExporter : BaseExporter<LogRecord>
{
    private readonly EventHubProducerClient _producerClient;
    private readonly CommonColumn _commonColumns;

    private readonly Channel<string> _channel;
    private readonly Task _senderTask;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly int _flushIntervalMilliseconds;

    public PopulateLogColumnsDelegate? PopulateColumns { get; set; }

    public EventHubLogExporter(EventHubLogExporterOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

    // no explicit max batch size; rely on Event Hub SDK defaults
        _flushIntervalMilliseconds = options.FlushIntervalMilliseconds ?? 2000;

        // create producer first
        if (!string.IsNullOrEmpty(options.FirstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(options.FirstPartyAppClientId) &&
            !string.IsNullOrEmpty(options.FirstPartyAppTenantId))
        {
            var certPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.key");
            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);
            var credential = new ClientCertificateCredential(options.FirstPartyAppTenantId, options.FirstPartyAppClientId, certificate,
                new ClientCertificateCredentialOptions { SendCertificateChain = true });
            _producerClient = new EventHubProducerClient(options.FullyQualifiedNamespace, options.EventHubName, credential);
        }
        else
        {
            if (string.IsNullOrEmpty(options.FullyQualifiedNamespace)) throw new ArgumentException("FullyQualifiedNamespace must be specified");
            var credential = new DefaultAzureCredential();
            _producerClient = new EventHubProducerClient(options.FullyQualifiedNamespace, options.EventHubName, credential);
        }

        PopulateColumns = options.PopulateColumns;

        try { _commonColumns = CommonColumn.Build(); }
        catch { _commonColumns = new CommonColumn(); }

        var maxQueue = options.MaxQueueSize ?? 10000;
        var channelOptions = new BoundedChannelOptions(maxQueue) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true, SingleWriter = false };
        _channel = Channel.CreateBounded<string>(channelOptions);

        // start sender after producer is ready
        _senderTask = Task.Run(() => SenderLoopAsync(_cts.Token));
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            foreach (var record in batch)
            {
                var serialized = ConvertLogRecordToLogData(record);
                _channel.Writer.TryWrite(serialized);
            }

            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private string ConvertLogRecordToLogData(LogRecord logRecord)
    {
        var logData = new Dictionary<string, object>
        {
            ["PreciseTimeStamp"] = logRecord.Timestamp,
            ["LogLevel"] = logRecord.LogLevel.ToString(),
            ["Message"] = logRecord.FormattedMessage ?? logRecord.Body?.ToString() ?? string.Empty,
            ["Exception"] = logRecord.Exception?.ToString() ?? string.Empty,
        };

        try
        {
            logData["AgentName"] = _commonColumns.AgentName;
            logData["Region"] = _commonColumns.AgentLocation;
            logData["ContainerImage"] = _commonColumns.ContainerImage;
            logData["ContainerGroupName"] = _commonColumns.ContainerGroupName;
        }
        catch { }

        if (logRecord.Attributes != null)
        {
            foreach (var kvp in logRecord.Attributes)
            {
                if (kvp.Key != "{OriginalFormat}") logData[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        PopulateColumns?.Invoke(logRecord, logData);

        return JsonSerializer.Serialize(logData);
    }

    private async Task SenderLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;
        var buffer = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // wait for a message or timeout
                if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        buffer.Add(item);
                        // limit buffer growth; send periodically
                        if (buffer.Count >= 500) break;
                    }
                }
                else
                {
                    // timed out or channel completed
                }

                if (buffer.Count == 0)
                {
                    await Task.Delay(_flushIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var eventBatch = await _producerClient.CreateBatchAsync(cancellationToken).ConfigureAwait(false);

                foreach (var msg in buffer)
                {
                    var ed = new EventData(Encoding.UTF8.GetBytes(msg));
                    ed.Properties.Add("Format", "json");
                    if (!eventBatch.TryAdd(ed))
                    {
                        if (eventBatch.Count > 0) await _producerClient.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);
                        eventBatch.Dispose();
                        eventBatch = await _producerClient.CreateBatchAsync(cancellationToken).ConfigureAwait(false);
                        if (!eventBatch.TryAdd(ed)) continue; // skip too-large item
                    }
                }

                if (eventBatch.Count > 0) await _producerClient.SendAsync(eventBatch, cancellationToken).ConfigureAwait(false);
                buffer.Clear();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: EventHub sender loop - {ex.Message}");
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }

        // flush remaining items
        try
        {
            var remaining = new List<string>();
            while (reader.TryRead(out var item)) remaining.Add(item);
            if (remaining.Count > 0)
            {
                var eventBatch = await _producerClient.CreateBatchAsync().ConfigureAwait(false);
                foreach (var msg in remaining)
                {
                    var ed = new EventData(Encoding.UTF8.GetBytes(msg));
                    ed.Properties.Add("Format", "json");
                    if (!eventBatch.TryAdd(ed))
                    {
                        if (eventBatch.Count > 0) await _producerClient.SendAsync(eventBatch).ConfigureAwait(false);
                        eventBatch.Dispose();
                        eventBatch = await _producerClient.CreateBatchAsync().ConfigureAwait(false);
                        if (!eventBatch.TryAdd(ed)) continue;
                    }
                }
                if (eventBatch.Count > 0) await _producerClient.SendAsync(eventBatch).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR flushing remaining messages - {ex.Message}");
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try
        {
            _cts.Cancel();
            _channel.Writer.Complete();
            _senderTask.Wait(Math.Max(1000, timeoutMilliseconds));
        }
        catch { }

        try
        {
            var cts = new CancellationTokenSource(Math.Max(1000, timeoutMilliseconds));
            _producerClient.CloseAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch { }

        return true;
    }
}
