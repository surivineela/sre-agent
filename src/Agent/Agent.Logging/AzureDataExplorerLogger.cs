using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Extensions.Logging;

namespace Agent.Logging;

public class AzureDataExplorerLogger : ILogger
{
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

    private readonly string _agentName;

    public AzureDataExplorerLogger(
        string agentName,
        IKustoIngestClient internalKustoClient,
        string internalDatabaseName,
        string internalTableName,
        IKustoIngestClient? externalKustoClient = null,
        string? externalDatabaseName = null,
        string? externalTableName = null)
    {
        _agentName = agentName;

        _internalKustoClient = internalKustoClient;
        _internalDatabaseName = internalDatabaseName;
        _internalTableName = internalTableName;

        _externalKustoClient = externalKustoClient;
        _externalDatabaseName = externalDatabaseName;
        _externalTableName = externalTableName;
        _isExternalKustoClientEnabled = externalKustoClient != null && !string.IsNullOrEmpty(externalDatabaseName) && !string.IsNullOrEmpty(externalTableName);
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var logMessage = formatter(state, exception);

        var logMessageStartIndex = logMessage.IndexOf(">>>", StringComparison.Ordinal) + 4;

        var logData = new
        {
            PreciseTimeStamp = DateTime.UtcNow,
            LogLevel = logLevel.ToString(),
            Message = logMessage.Substring(logMessageStartIndex),
            Exception = exception?.ToString(),
            AgentName = _agentName
        };

        // Determine the logType from the state or scope
        string logType = ExtractLogType(state);

        // Route logs based on the logType property
        if (logType == InternalLogType)
        {
            try
            {
                IngestToCluster(
                    _internalKustoClient,
                    databaseName: _internalDatabaseName,
                    tableName: _internalTableName,
                    logData: logData);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{DateTime.UtcNow}] [Console] Failed to ingest log to internal cluster: {e.Message}");
                Console.WriteLine($"[{DateTime.UtcNow}] [Console] Log data: {JsonSerializer.Serialize(logData)}");
            }
        }
        else if (logType == ExternalLogType && _isExternalKustoClientEnabled)
        {
            IngestToCluster(
                _externalKustoClient,
                databaseName: _externalDatabaseName,
                tableName: _externalTableName,
                logData: logData);
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
