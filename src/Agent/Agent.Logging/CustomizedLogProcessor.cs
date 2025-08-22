// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Agent.Logging;

/// <summary>
/// OpenTelemetry log processor that filters log records based on a customizable predicate function.
/// Allows users to provide their own implementation of the ShouldProcessRecord logic.
/// </summary>
public sealed class CustomizedLogProcessor : BaseProcessor<LogRecord>
{
    private readonly string name;
    private readonly BaseExporter<LogRecord>? exporter;

    /// <summary>
    /// Gets or sets the predicate function that determines whether a log record should be processed.
    /// </summary>
    public Func<LogRecord, bool> ShouldProcessRecord { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedLogProcessor"/> class.
    /// </summary>
    /// <param name="exporter">The exporter to use for filtered log records. Can be null for test environments.</param>
    /// <param name="shouldProcessRecord">The predicate function to determine if a log record should be processed.</param>
    /// <param name="name">Optional name for debugging purposes.</param>
    /// <exception cref="ArgumentNullException">Thrown when shouldProcessRecord is null.</exception>
    public CustomizedLogProcessor(
        BaseExporter<LogRecord>? exporter,
        Func<LogRecord, bool> shouldProcessRecord,
        string name = "CustomizedLogProcessor")
    {
        this.exporter = exporter;
        this.ShouldProcessRecord = shouldProcessRecord ?? throw new ArgumentNullException(nameof(shouldProcessRecord));
        this.name = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedLogProcessor"/> class with a default filter.
    /// The default filter processes logs with EventId 1001 or 1002 from categories starting with "Agent.".
    /// </summary>
    /// <param name="exporter">The exporter to use for filtered log records. Can be null for test environments.</param>
    /// <param name="name">Optional name for debugging purposes.</param>
    public CustomizedLogProcessor(BaseExporter<LogRecord>? exporter, string name = "CustomizedLogProcessor")
        : this(exporter, DefaultShouldProcessRecord, name)
    {
    }

    /// <summary>
    /// Processes a log record, filtering based on the configured predicate function.
    /// </summary>
    /// <param name="record">The log record to process.</param>
    public override void OnEnd(LogRecord record)
    {
        // Check if the log record should be processed using the configured predicate
        if (ShouldProcessRecord(record) && exporter != null)
        {
            // Export the filtered log record using the provided exporter
            exporter.Export(new Batch<LogRecord>(new[] { record }, 1));
        }
        // If the predicate returns false or no exporter is configured, the log is filtered out
    }

    /// <summary>
    /// Default implementation that determines whether a log record should be processed.
    /// Processes logs with EventId 1001 or 1002 from categories starting with "Agent.".
    /// </summary>
    /// <param name="record">The log record to evaluate.</param>
    /// <returns>True if the record should be processed; otherwise, false.</returns>
    private static bool DefaultShouldProcessRecord(LogRecord record)
    {
        bool isFromAgent = record.CategoryName?.StartsWith("Agent.", StringComparison.OrdinalIgnoreCase) == true;

        return isFromAgent;
    }
}
