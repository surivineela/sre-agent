using Microsoft.Extensions.Logging;

namespace Agent.Logging;

public static class LoggerExtensions
{
    public static void LogInternalInformation<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.InternalLogType } }))
        {
            logger.LogInformation($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.InternalLogType);
        }
    }

    public static void LogExternalInformation<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            logger.LogInformation($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.ExternalLogType);
        }
    }

    public static void LogInternalWarning<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.InternalLogType } }))
        {
            logger.LogWarning($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.InternalLogType);
        }
    }

    public static void LogExternalWarning<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            logger.LogWarning($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.ExternalLogType);
        }
    }

    public static void LogInternalError<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.InternalLogType } }))
        {
            logger.LogError($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.InternalLogType);
        }
    }

    public static void LogExternalError<T>(this ILogger<T> logger, string message)
    {
        using (logger.BeginScope(new Dictionary<string, object> { { AzureDataExplorerLogger.LogTypeName, AzureDataExplorerLogger.ExternalLogType } }))
        {
            logger.LogError($"{{{AzureDataExplorerLogger.LogTypeName}}}>>> {message}", AzureDataExplorerLogger.ExternalLogType);
        }
    }
}
