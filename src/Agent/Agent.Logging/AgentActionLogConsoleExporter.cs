using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Agent.Logging;

/// <summary>
/// Exporter for writing agent action log records to the console.
/// </summary>
public class AgentActionLogConsoleExporter : IAgentActionLogExporter
{
    private readonly ILogger<AgentActionLogConsoleExporter> _logger;
    private readonly LogBuffer? _logBuffer;
    private readonly bool _useBatchProcessing;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentActionLogConsoleExporter"/> class.
    /// </summary>
    /// <param name="logger">Logger for the exporter.</param>
    /// <param name="useBatchProcessing">Whether to use batch processing (defaults to false).</param>
    public AgentActionLogConsoleExporter(
        ILogger<AgentActionLogConsoleExporter> logger,
        bool useBatchProcessing = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _useBatchProcessing = useBatchProcessing;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (useBatchProcessing)
        {
            _logBuffer = new LogBuffer();
        }
    }

    /// <summary>
    /// Exports a single agent action log record to the console.
    /// </summary>
    /// <param name="logRecord">The agent action log record to export.</param>
    public void Export(AgentActionLogRecord logRecord)
    {
        try
        {
            if (_useBatchProcessing && _logBuffer != null)
            {
                // Batch processing mode
                var actionData = ConvertLogRecordToConsoleData(logRecord);
                _logBuffer.Logs.Enqueue(actionData);
            }
            else
            {
                // Direct processing mode
                WriteToConsole(logRecord);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting agent action log record to console");
        }
    }

    /// <summary>
    /// Exports multiple agent action log records to the console.
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
                    var actionData = ConvertLogRecordToConsoleData(logRecord);
                    _logBuffer.Logs.Enqueue(actionData);
                }
            }
            else
            {
                // Direct processing mode for each record
                foreach (var logRecord in logRecords)
                {
                    WriteToConsole(logRecord);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting agent action log records to console");
        }
    }

    /// <summary>
    /// Flushes the current batch of logs to the console.
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

            WriteBatchToConsole(logDataList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing batch to console");
        }
    }

    /// <summary>
    /// Converts an AgentActionLogRecord to a structured data object suitable for console output.
    /// </summary>
    private object ConvertLogRecordToConsoleData(AgentActionLogRecord logRecord)
    {
        return new
        {
            Action = logRecord.Action,
            Parameter = logRecord.Parameter,
            Status = logRecord.Status,
            Duration = logRecord.Duration,
            Timestamp = logRecord.Timestamp,
            Exception = logRecord.Exception ?? string.Empty
        };
    }

    private void WriteToConsole(AgentActionLogRecord logRecord)
    {
        var actionData = ConvertLogRecordToConsoleData(logRecord);
        var jsonOutput = JsonSerializer.Serialize(actionData, _jsonOptions);

        _logger.LogInformation($"[Agent Action Log] {jsonOutput}");

        // Also log through the structured logger
        _logger.LogInformation("Agent Action: {Action}, Parameter: {Parameter}, Status: {Status}, Duration: {Duration}ms, Timestamp: {Timestamp}",
            logRecord.Action, logRecord.Parameter, logRecord.Status, logRecord.Duration, logRecord.Timestamp);
    }

    private void WriteBatchToConsole(IEnumerable<object> logDataBatch)
    {
        var jsonOutput = JsonSerializer.Serialize(logDataBatch, _jsonOptions);

        _logger.LogInformation($"[Agent Action Log Batch] {jsonOutput}");

        // Also log the batch count through the structured logger
        var count = logDataBatch is ICollection<object> collection ? collection.Count : logDataBatch.Count();
        _logger.LogInformation("Flushed batch of {Count} agent action log records to console", count);
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

        _logger.LogInformation("Agent action log console exporter shutdown completed");
    }
}
