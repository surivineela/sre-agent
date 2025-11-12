// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.Utility)]
    public class CustomerLogsPluginDefinition
    {
        private readonly ICustomerLogsPlugin _plugin;

        public CustomerLogsPluginDefinition(ICustomerLogsPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description("Log a message to customer logs with specified log level and optional category. Use this to record important events, errors, or information for customer visibility.")]
        public Task<string> LogToCustomerLogs(
            [Description("The message to log")] string message,
            [Description("Log level: Info, Warning, Error, or Debug")] string logLevel = "Info",
            [Description("Optional category to classify the log entry")] string? category = null)
        {
            return _plugin.LogToCustomerLogsAsync(message, logLevel, category);
        }
    }
}
