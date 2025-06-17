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
    void Shutdown();
}
