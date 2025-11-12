using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// In-memory exporter implementation for storing Activity objects in memory
/// </summary>
public class InMemoryActivityExporter : BaseExporter<Activity>
{
    private ICollection<Activity> _exportedActivities;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryActivityExporter"/> class.
    /// </summary>
    /// <param name="exportedActivities">Collection to store exported activities</param>
    public InMemoryActivityExporter(ICollection<Activity> exportedActivities)
    {
        _exportedActivities = exportedActivities ?? throw new ArgumentNullException(nameof(exportedActivities));
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            foreach (var activity in batch)
            {
                _exportedActivities.Add(activity);
            }

            return ExportResult.Success;
        }
        catch (Exception)
        {
            return ExportResult.Failure;
        }
    }
}
