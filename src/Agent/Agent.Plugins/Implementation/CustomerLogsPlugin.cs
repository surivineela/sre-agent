// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class CustomerLogsPlugin : ICustomerLogsPlugin
{
    private readonly ILogger<CustomerLogsPlugin> _logger;
    private readonly CustomerLogger _customerLogger;

    public CustomerLogsPlugin(ILogger<CustomerLogsPlugin> logger, CustomerLogger customerLogger)
    {
        _logger = logger;
        _customerLogger = customerLogger;
    }

    public Task<string> LogToCustomerLogsAsync(string message, string logLevel = "Info", string? category = null)
    {
        try
        {
            var timestamp = DateTime.UtcNow;

            // Use the ApplicationInsights CustomerLogger to log to customer logs
            var categoryPrefix = !string.IsNullOrEmpty(category) ? $"[{category}] " : string.Empty;
            var logMessage = $"{categoryPrefix}{message}";
            _customerLogger.LogMessage(logMessage);

            var successMessage = $"Successfully logged message to customer logs at {timestamp:yyyy-MM-dd HH:mm:ss UTC}";
            _logger.LogInternalInformation("LogToCustomerLogs completed successfully");

            return Task.FromResult(successMessage);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to log to customer logs: {ex.Message}";
            _logger.LogInternalError(ex, "Error in LogToCustomerLogs");
            throw new Exception(errorMessage, ex);
        }
    }
}
