// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit.Abstractions;

namespace Agent.Tests.Common.XUnit
{
    /// <summary>
    /// Exports Open Telemetry logs to XUnit test output.
    /// </summary>
    internal static class OpenTelemetryXUnitLoggingExtensions
    {
        internal static IWebHostBuilder OverrideOtelLoggingForTestOutput(this IWebHostBuilder builder, ITestOutputHelper output)
        {
            return builder.ConfigureLogging((context, logging) =>
            {

                // We use OpenTelemetry for logging with a an exporter that writes to Geneva.
                // This gets the OpenTelemetryLoggerOptions that we register for Geneva logging and appends a processor that writes logs to the XUnit test output.

                ServiceDescriptor? currentOtelServiceDescriptor = logging.Services.FirstOrDefault(x => x.ServiceType == typeof(IConfigureOptions<OpenTelemetryLoggerOptions>) && x.ImplementationInstance != null);
                bool hasOtelProvider = false;

                if (currentOtelServiceDescriptor != null)
                {
                    ConfigureNamedOptions<OpenTelemetryLoggerOptions>? currentOtelOptions = currentOtelServiceDescriptor.ImplementationInstance as ConfigureNamedOptions<OpenTelemetryLoggerOptions>;

                    if (currentOtelOptions != null)
                    {
                        logging.Services.Remove(currentOtelServiceDescriptor);
                        logging.Services.AddSingleton<IConfigureOptions<OpenTelemetryLoggerOptions>>(new ConfigureNamedOptions<OpenTelemetryLoggerOptions>(currentOtelOptions.Name, opt =>
                        {
                            // Call the original configuration action first so that our Open Telemetry processing appears in the test logs
                            currentOtelOptions.Action?.Invoke(opt);

                            // Then add the XUnit test output processor
                            opt.AddProcessor(new SimpleLogRecordExportProcessor(new OpenTelemetryXUnitExporter(output, new XunitLogFormatter(), "Agent")));
                        }));

                        hasOtelProvider = true;
                    }
                }

                if (!hasOtelProvider)
                {
                    XunitTestHostLoggerProvider loggerProvider = new XunitTestHostLoggerProvider(output, context.Configuration, Stopwatch.GetTimestamp(), "INTEGRATION TEST");

                    ILogger logger = loggerProvider.CreateLogger("Test Logging Setup");
                    logger.LogWarning($"Missing Open Telemetry logging provider for application Agent.Web.");

                    logging.AddProvider(loggerProvider);
                }
            });
        }
    }
}
