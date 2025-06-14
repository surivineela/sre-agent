using Microsoft.Extensions.Logging;

namespace Agent.Logging;

/// <summary>
/// Logger for recording agent actions with structured data
/// </summary>
public class AgentActionLogger
{
    private readonly ILogger<AgentActionLogger> _logger;
    private readonly IAgentActionLogExporter? _exporter;

    public AgentActionLogger(ILogger<AgentActionLogger> logger, IAgentActionLogExporter? exporter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exporter = exporter;
    }

    /// <summary>
    /// Logs an agent action with structured information
    /// </summary>
    /// <param name="action">The action being performed</param>
    /// <param name="module">The module or component performing the action</param>
    /// <param name="parameter">Parameters associated with the action</param>
    /// <param name="status">The status/result of the action</param>
    /// <param name="duration">The duration of the action in milliseconds</param>
    public void LogAction(string action, string parameter, string status, long duration)
    {
        var logRecord = new AgentActionLogRecord
        {
            Action = action,
            Parameter = parameter,
            Status = status,
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow
        };

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            { nameof(logRecord.Action), logRecord.Action },
            { nameof(logRecord.Parameter), logRecord.Parameter },
            { nameof(logRecord.Status), logRecord.Status },
            { nameof(logRecord.Duration), logRecord.Duration },
            { nameof(logRecord.Timestamp), logRecord.Timestamp }
        });

        _logger.LogInformation(
            "Agent Action: {Action} with parameters {Parameter} completed with status {Status} in {Duration}ms",
            action, parameter, status, duration);

        // Export to external system if exporter is configured
        _exporter?.Export(logRecord);
    }

    /// <summary>
    /// Logs an agent action with structured information and exception details
    /// </summary>
    /// <param name="action">The action being performed</param>
    /// <param name="parameter">Parameters associated with the action</param>
    /// <param name="status">The status/result of the action</param>
    /// <param name="duration">The duration of the action in milliseconds</param>
    /// <param name="exception">Exception that occurred during the action</param>
    public void LogAction(string action,  string parameter, string status, long duration, Exception exception)
    {
        var logRecord = new AgentActionLogRecord
        {
            Action = action,
            Parameter = parameter,
            Status = status,
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow,
            Exception = exception?.ToString()
        };

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            { nameof(logRecord.Action), logRecord.Action },
            { nameof(logRecord.Parameter), logRecord.Parameter },
            { nameof(logRecord.Status), logRecord.Status },
            { nameof(logRecord.Duration), logRecord.Duration },
            { nameof(logRecord.Timestamp), logRecord.Timestamp },
            { nameof(logRecord.Exception), logRecord.Exception ?? string.Empty }
        });

        _logger.LogError(exception,
            "Agent Action: {Action} with parameters {Parameter} failed with status {Status} in {Duration}ms",
            action, parameter, status, duration);

        // Export to external system if exporter is configured
        _exporter?.Export(logRecord);
    }
}

/// <summary>
/// Record structure for agent action logging
/// </summary>
public class AgentActionLogRecord
{
    /// <summary>
    /// The action being performed
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Parameters associated with the action
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// The status/result of the action (e.g., "Success", "Failed", "Timeout")
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The duration of the action in milliseconds
    /// </summary>
    public long Duration { get; set; }

    /// <summary>
    /// Timestamp when the action occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Exception details if the action failed
    /// </summary>
    public string? Exception { get; set; }
}
