// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit.Abstractions;

namespace Agent.Tests.Common.XUnit
{
    /// <summary>
    /// Exports Open Telemetry logs to XUnit test output.
    /// </summary>
    internal class OpenTelemetryXUnitExporter : BaseExporter<LogRecord>
    {
        private readonly XunitLogFormatter _formatter;
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly string _loggingProviderName;
        private long _startTime;

        public OpenTelemetryXUnitExporter(ITestOutputHelper testOutputHelper, XunitLogFormatter formatter, string providerName)
        {
            _testOutputHelper = testOutputHelper;
            _formatter = formatter;
            _startTime = Stopwatch.GetTimestamp();
            _loggingProviderName = providerName.Replace("Microsoft.ContainerApps.", string.Empty);
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var logRecord in batch)
            {
                string message = string.Empty;
                if (logRecord.Attributes != null)
                {
                    message = _formatter.FormatMessage(logRecord.LogLevel, logRecord.EventId, logRecord.Attributes, logRecord.Exception, _startTime, logRecord.TraceId.ToString(), logRecord.SpanId.ToString(), _loggingProviderName, logRecord.CategoryName ?? string.Empty, DefaultFormatter);
                }
                else
                {
                    message = logRecord.FormattedMessage ?? "Open Telemetry missing log message and state.";
                }

                try
                {
                    _testOutputHelper.WriteLine(message);
                }
                catch (InvalidOperationException)
                {
                    // ignore exceptions when some thread tries to write a log after the test has terminated (i.e. background timer)
                }
            }

            return ExportResult.Success;
        }

        private static string DefaultFormatter(IEnumerable<KeyValuePair<string, object?>> state, Exception? exception)
        {
            StringBuilder sb = new StringBuilder();

            string exceptionString = exception?.ToString() ?? "";

            foreach (KeyValuePair<string, object?> item in state)
            {
                // Special casing {OriginalFormat}
                // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                // for explanation.
                KeyValuePair<string, object?> kv = item.Key.Equals("{OriginalFormat}")
                    ? new KeyValuePair<string, object?>("Body", item.Value)
                    : item;

                // remove EventID, it will be provided to the formatter as an individual parameter

                if (string.Equals(kv.Key, "EventId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(kv.Key, "Exception", StringComparison.OrdinalIgnoreCase))
                {
                    if (exception == null)
                    {
                        exceptionString = kv.Value?.ToString() ?? string.Empty;
                    }

                    continue;
                }

                sb.Append($"[{kv.Key}] : '{kv.Value}', ");
            }

            if (!string.IsNullOrEmpty(exceptionString))
            {
                sb.AppendLine();
                sb.AppendLine(exceptionString);
            }

            return sb.ToString();
        }
    }
}
