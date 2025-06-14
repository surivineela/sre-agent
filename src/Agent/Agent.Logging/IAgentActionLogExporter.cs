namespace Agent.Logging;

/// <summary>
/// Interface for exporting agent action log records.
/// </summary>
public interface IAgentActionLogExporter
{
    /// <summary>
    /// Exports a single agent action log record.
    /// </summary>
    /// <param name="logRecord">The agent action log record to export.</param>
    void Export(AgentActionLogRecord logRecord);

    /// <summary>
    /// Exports multiple agent action log records.
    /// </summary>
    /// <param name="logRecords">The collection of agent action log records to export.</param>
    void Export(IEnumerable<AgentActionLogRecord> logRecords);

    /// <summary>
    /// Flushes any buffered logs.
    /// </summary>
    void FlushBatch();

    /// <summary>
    /// Shuts down the exporter and flushes any remaining logs.
    /// </summary>
    void Shutdown();
}
