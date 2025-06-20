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
    /// <param name="parameter">Parameters associated with the action</param>
    /// <param name="status">The status/result of the action</param>
    /// <param name="duration">The duration of the action in milliseconds</param>
    public void LogAction(
        string action,
        string parameter,
        string status,
        long duration,
        string threadId = "",
        string subagent = "",
        long inputToken = 0,
        long outputToken = 0)
    {
        var logRecord = new AgentActionLogRecord
        {
            Action = action,
            Parameter = parameter,
            Status = status,
            Duration = duration,
            PreciseTimeStamp = DateTimeOffset.UtcNow,
            ThreadId = threadId,
            SubAgentName = subagent,
            InputToken = inputToken,
            OutputToken = outputToken
        };

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            { nameof(logRecord.Action), logRecord.Action },
            { nameof(logRecord.Parameter), logRecord.Parameter },
            { nameof(logRecord.Status), logRecord.Status },
            { nameof(logRecord.Duration), logRecord.Duration },
            { nameof(logRecord.PreciseTimeStamp), logRecord.PreciseTimeStamp },
            { nameof(logRecord.ThreadId), logRecord.ThreadId },
            { nameof(logRecord.SubAgentName), logRecord.SubAgentName },
            { nameof(logRecord.InputToken), logRecord.InputToken },
            { nameof(logRecord.OutputToken), logRecord.OutputToken }
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
    public void LogAction(string action,  string parameter, string status, int duration, Exception exception)
    {
        var logRecord = new AgentActionLogRecord
        {
            Action = action,
            Parameter = parameter,
            Status = status,
            Duration = duration,
            PreciseTimeStamp = DateTimeOffset.UtcNow,
        };

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            { nameof(logRecord.Action), logRecord.Action },
            { nameof(logRecord.Parameter), logRecord.Parameter },
            { nameof(logRecord.Status), logRecord.Status },
            { nameof(logRecord.Duration), logRecord.Duration },
            { nameof(logRecord.PreciseTimeStamp), logRecord.PreciseTimeStamp },
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
    /// PreciseTimeStamp when the action occurred
    /// </summary>
    public DateTimeOffset PreciseTimeStamp { get; set; }

    /// <summary>
    /// ThreadId
    /// </summary>
    public string ThreadId { get; set; }

    /// <summary>
    /// Sub Agent Name
    /// </summary>
    public string SubAgentName { get; set; }

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
    /// The input token count for the action
    /// </summary>
    public long InputToken { get; set; }

    /// <summary>
    /// The output token count for the action
    /// </summary>
    public long OutputToken { get; set; }
}
