// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface;

public interface ICustomerLogsPlugin
{
    /// <summary>
    /// Log a message to customer logs
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="logLevel">Log level (Info, Warning, Error, Debug)</param>
    /// <param name="category">Optional category for the log entry</param>
    /// <returns>Success message</returns>
    Task<string> LogToCustomerLogsAsync(string message, string logLevel = "Info", string? category = null);
}