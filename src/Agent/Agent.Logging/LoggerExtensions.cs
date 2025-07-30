using Agent.Logging;

#pragma warning disable IDE0130 // Extension methods should be in the same namespace as the containing type
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Extension methods should be in the same namespace as the containing type

public static partial class LoggerExtensions
{
    public static void LogInternalInformation<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        LogInternalInformationHelper(logger, message, args);
    }

    public static void LogInternalInformation(this ILogger logger, string message, params object?[] args)
    {
        LogInternalInformationHelper(logger, message, args);
    }

    public static void LogInternalInformation<T>(this ILogger<T> logger, Exception exception, string message, params object?[] args)
    {
        LogInternalInformationHelper(logger, exception, message, args);
    }

    public static void LogExternalInformation<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            logger.LogInformation(message, args);
        }
    }

    private static void LogInternalInformationHelper(ILogger logger, Exception exception, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogInformation(exception, $"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }

    private static void LogInternalInformationHelper(ILogger logger, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogInformation($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }

    public static void LogInternalWarning<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        LogInternalWarningHelper(logger, message, args);
    }

    public static void LogInternalWarning(this ILogger logger, string message, params object?[] args)
    {
        LogInternalWarningHelper(logger, message, args);
    }

    public static void LogInternalWarning(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        LogInternalWarningHelper(logger, exception, message, args);
    }

    public static void LogInternalWarning<T>(this ILogger<T> logger, Exception exception, string message, params object?[] args)
    {
        LogInternalWarningHelper(logger, exception, message, args);
    }

    public static void LogExternalWarning<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            string escapedMessage = message.Replace("{", "{{").Replace("}", "}}");

            logger.LogWarning($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.ExternalLogType);
        }
    }

    private static void LogInternalWarningHelper(ILogger logger, Exception exception, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogWarning(exception, $"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }

    private static void LogInternalWarningHelper(ILogger logger, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogWarning($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }

    public static void LogInternalError<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        LogInternalErrorHelper(logger, message, args);
    }

    public static void LogInternalError(this ILogger logger, string message, params object?[] args)
    {
        LogInternalErrorHelper(logger, message, args);
    }

    public static void LogInternalError<T>(this ILogger<T> logger, Exception exception, string message, params object?[] args)
    {
        LogInternalErrorHelper(logger, exception, message, args);
    }

    public static void LogInternalError(this ILogger logger, Exception exception, string message, params object?[] args)
    {
        LogInternalErrorHelper(logger, exception, message, args);
    }

    public static void LogExternalError<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            string escapedMessage = message.Replace("{", "{{").Replace("}", "}}");

            logger.LogError($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.ExternalLogType);
        }
    }

    private static void LogInternalErrorHelper(ILogger logger, Exception exception, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogError(exception, $"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }

    private static void LogInternalErrorHelper(ILogger logger, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogError($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }


    public static void LogInternalDebug<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        LogInternalDebugHelper(logger, message, args);
    }

    public static void LogInternalDebug(this ILogger logger, string message, params object?[] args)
    {
        LogInternalDebugHelper(logger, message, args);
    }

    private static void LogInternalDebugHelper(ILogger logger, string message, params object?[] args)
    {
        if (args.Length == 0)
        {
            message = message.Replace("{", "{{").Replace("}", "}}");
        }

        args = args.Prepend(AzureDataExplorerLogger.InternalLogType).ToArray();
        logger.LogDebug($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", args);
    }


    /// <summary>
    /// Logs an agent action with structured information using LoggerMessage source generation
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="action">The action being performed</param>
    /// <param name="parameter">Parameters associated with the action</param>
    /// <param name="status">The status/result of the action</param>
    /// <param name="duration">The duration of the action in milliseconds</param>
    /// <param name="threadId">Thread identifier</param>
    /// <param name="subAgentName">Sub-agent name</param>
    /// <param name="inputToken">Input token count</param>
    /// <param name="outputToken">Output token count</param>
    /// <param name="threadSource">Thread source</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Agent Action: {Action} with parameters {Parameter} completed with status {Status} in {Duration}ms for thread {ThreadId}, subAgent: {SubAgentName}, inputTokens: {InputToken}, outputTokens: {OutputToken}, threadSource: {ThreadSource}")]
    public static partial void LogAgentAction(
        this ILogger logger,
        string action,
        string parameter,
        string status,
        long duration,
        string threadId,
        string subAgentName = "",
        long inputToken = 0,
        long outputToken = 0,
        string threadSource = "");

    /// <summary>
    /// Logs an agent action with exception information using LoggerMessage source generation
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="action">The action being performed</param>
    /// <param name="parameter">Parameters associated with the action</param>
    /// <param name="status">The status/result of the action</param>
    /// <param name="duration">The duration of the action in milliseconds</param>
    /// <param name="threadId">Thread identifier</param>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Agent Action: {Action} with parameters {Parameter} failed with status {Status} in {Duration}ms for thread {ThreadId}")]
    public static partial void LogAgentActionError(
        this ILogger logger,
        Exception exception,
        string action,
        string parameter,
        string status,
        long duration,
        string threadId);
}
