using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;

namespace Agent.Logging;

public class AzureDataExplorerLogger : ILogger
{
    private readonly LogBuffer _logBuffer;

    public const string LogTypeName = "LogType";
    public const string InternalLogType = "Internal";
    public const string ExternalLogType = "External";

    private readonly IKustoIngestClient _internalKustoClient; // For "Internal" logs
    private readonly string _internalDatabaseName;
    private readonly string _internalTableName;

    private readonly IKustoIngestClient? _externalKustoClient; // For "External" logs
    private readonly string? _externalDatabaseName;
    private readonly string? _externalTableName;

    private readonly bool _isExternalKustoClientEnabled;

    private readonly CommonColumn _commonColumn;

    public AzureDataExplorerLogger() { _logBuffer = new LogBuffer(); }

    public AzureDataExplorerLogger(
        CommonColumn commonColumn,
        IKustoIngestClient internalKustoClient,
        string internalDatabaseName,
        string internalTableName,
        IKustoIngestClient? externalKustoClient = null,
        string? externalDatabaseName = null,
        string? externalTableName = null)
    {
        _commonColumn = commonColumn ?? new CommonColumn();

        _internalKustoClient = internalKustoClient;
        _internalDatabaseName = internalDatabaseName;
        _internalTableName = internalTableName;

        _externalKustoClient = externalKustoClient;
        _externalDatabaseName = externalDatabaseName;
        _externalTableName = externalTableName;
        _isExternalKustoClientEnabled = externalKustoClient != null && !string.IsNullOrEmpty(externalDatabaseName) && !string.IsNullOrEmpty(externalTableName);

        _logBuffer = new LogBuffer();
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var logMessage = formatter(state, exception);

        if (!string.IsNullOrEmpty(logMessage)
            && logMessage.Contains(">>> "))
        {
            var logMessageStartIndex = logMessage.IndexOf(">>>", StringComparison.Ordinal) + 4;
            logMessage = logMessage.Substring(logMessageStartIndex);
        }

        var logData = new
        {
            PreciseTimeStamp = DateTime.UtcNow,
            LogLevel = logLevel.ToString(),
            Message = logMessage,
            Exception = exception?.ToString(),
            AgentName = _commonColumn.AgentName,
            ContainerImage = _commonColumn.ContainerImage,
            ContainerGroupName = _commonColumn.ContainerGroupName,
        };

        // Determine the logType from the state or scope
        string logType = ExtractLogType(state);

        // Route logs based on the logType property
        if (logType == InternalLogType)
        {
            _logBuffer.Logs.Enqueue(logData);
        }
        else if (logType == ExternalLogType && _isExternalKustoClientEnabled)
        {
            IngestToCluster(
                _externalKustoClient,
                databaseName: _externalDatabaseName,
                tableName: _externalTableName,
                logData: logData);
        }
        else if (logType == ExternalLogType)
        {
            Console.WriteLine($"[{DateTime.UtcNow}] [ExternalKusto] {logMessage}");
        }
        else
        {
            Console.WriteLine($"[{DateTime.UtcNow}] [Console] {logMessage}");

            // TODO: Uncomment to make strongly opinionated
            //throw new Exception($"Invalid logType value: {logType}");
        }
    }

    private string ExtractLogType<TState>(TState state)
    {
        if (state is IReadOnlyList<KeyValuePair<string, object>> stateProperties)
        {
            foreach (var property in stateProperties)
            {
                if (property.Key == LogTypeName && property.Value is string logTypeValue)
                {
                    return logTypeValue;
                }
            }
        }

        return null;
        // TODO: Uncomment to make strongly opinionated
        //throw new Exception("LogType property not found in log message");
    }

    public void FlushLogBuffer()
    {
        if (_logBuffer.Logs.Count > 0)
        {
            var logDataList = new List<object>();
            while (_logBuffer.Logs.TryDequeue(out var logData))
            {
                logDataList.Add(logData);
            }

            IngestBatchToCluster(logDataList);
        }
    }

    private void IngestBatchToCluster(IEnumerable<object> logDataBatch)
    {
        var jsonData = JsonSerializer.Serialize(logDataBatch);

        var ingestionProperties = new KustoIngestionProperties(_internalDatabaseName, _internalTableName)
        {
            Format = DataSourceFormat.multijson // Specify JSON format
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
        _internalKustoClient.IngestFromStreamAsync(stream, ingestionProperties).Wait();
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
}
