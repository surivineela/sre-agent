using Agent.Logging;

#pragma warning disable IDE0130 // Extension methods should be in the same namespace as the containing type
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Extension methods should be in the same namespace as the containing type

public static class LoggerExtensions
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
}
