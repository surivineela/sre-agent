// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Agent.Logging;

/// <summary>
/// OpenTelemetry log processor that filters log records based on specific EventId values.
/// Only processes logs with EventId 1001 or 1002, which correspond to agent action logs.
/// </summary>
public sealed class AgentActionLogProcessor : BaseProcessor<LogRecord>
{
    private readonly string name;
    private readonly BaseExporter<LogRecord>? exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentActionLogProcessor"/> class.
    /// </summary>
    /// <param name="exporter">The exporter to use for filtered log records. Can be null for test environments.</param>
    /// <param name="name">Optional name for debugging purposes.</param>
    public AgentActionLogProcessor(BaseExporter<LogRecord>? exporter, string name = "AgentActionLogProcessor")
    {
        this.exporter = exporter;
        this.name = name;
    }

    /// <summary>
    /// Processes a log record, filtering based on EventId.
    /// Only logs with EventId 1001 or 1002 are processed further.
    /// </summary>
    /// <param name="record">The log record to process.</param>
    public override void OnEnd(LogRecord record)
    {
        // Check if the log record has an EventId of 1001 or 1002
        if (ShouldProcessRecord(record) && exporter != null)
        {
            // Export the filtered log record using the provided exporter
            exporter.Export(new Batch<LogRecord>(new[] { record }, 1));
        }
        // If EventId doesn't match our criteria or no exporter is configured, the log is filtered out
    }

    /// <summary>
    /// Determines whether a log record should be processed based on its EventId.
    /// </summary>
    /// <param name="record">The log record to evaluate.</param>
    /// <returns>True if the record should be processed; otherwise, false.</returns>
    private static bool ShouldProcessRecord(LogRecord record)
    {
        int eventId = record.EventId.Id;
        bool hasCorrectEventId = eventId == 1001 || eventId == 1002;
        bool isFromAgent = record.CategoryName?.StartsWith("Agent.", StringComparison.OrdinalIgnoreCase) == true;
                
        return hasCorrectEventId && isFromAgent;
    }
}
