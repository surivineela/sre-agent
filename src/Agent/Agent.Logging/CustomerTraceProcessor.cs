// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Agent.Logging;

/// <summary>
/// Configuration options for the Customer Trace Processor.
/// </summary>
public class CustomerTraceProcessorOptions
{
    /// <summary>
    /// Gets or sets whether to log filtering actions for debugging purposes.
    /// Default is false for production environments.
    /// </summary>
    public bool LogFilteringActions { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum duration threshold for activities to be included in customer traces.
    /// Activities shorter than this duration may be considered too granular for customer visibility.
    /// Default is null (no filtering by duration).
    /// </summary>
    public TimeSpan? MinimumDurationThreshold { get; set; }

    /// <summary>
    /// Gets or sets whether to include system-level spans in customer traces.
    /// Default is false for security.
    /// </summary>
    public bool IncludeSystemSpans { get; set; } = false;
}

/// <summary>
/// OpenTelemetry processor that processes only CustomerActivity instances (from "Agent.Customer" ActivitySource)
/// for export to customer-facing trace stores. This processor ensures that internal OpenTelemetry activities
/// are completely excluded from customer logs, eliminating the need for complex filtering logic.
/// Only activities created by CustomerTracer are processed, guaranteeing customer-safe tracing.
/// </summary>
public class CustomerTraceProcessor : BaseProcessor<Activity>
{
    private readonly CustomerLogger _customerLogger;
    private readonly CustomerTraceProcessorOptions _options;
    private readonly ILogger? _logger;
    private readonly List<BaseExporter<Activity>> _customerExporters;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerTraceProcessor"/> class.
    /// </summary>
    /// <param name="customerLogger">The customer logger instance.</param>
    /// <param name="options">Configuration options for the processor.</param>
    /// <param name="logger">Optional logger for debugging filtering actions.</param>
    /// <param name="customerExporters">Optional list of customer-safe trace exporters.</param>
    public CustomerTraceProcessor(
        CustomerLogger customerLogger,
        CustomerTraceProcessorOptions? options = null,
        ILogger? logger = null,
        IEnumerable<BaseExporter<Activity>>? customerExporters = null)
    {
        _customerLogger = customerLogger ?? throw new ArgumentNullException(nameof(customerLogger));
        _options = options ?? new CustomerTraceProcessorOptions();
        _logger = logger;
        _customerExporters = customerExporters?.ToList() ?? new List<BaseExporter<Activity>>();
    }

    public override void OnStart(Activity activity)
    {
        // Only process CustomerActivity instances from "Agent.Customer" ActivitySource
        if (ShouldProcessActivityForCustomer(activity))
        {
            LogCustomerTraceStart(activity);
        }

        base.OnStart(activity);
    }

    public override void OnEnd(Activity activity)
    {
        try
        {
            // Only process CustomerActivity instances (already customer-safe)
            if (ShouldProcessActivityForCustomer(activity))
            {
                ProcessCustomerSafeActivity(activity);

                // Export to customer-safe exporters if configured
                if (_customerExporters.Count > 0)
                {
                    ExportToCustomerSafeExporters(activity);
                }
            }
            else if (_options.LogFilteringActions && _logger != null)
            {
                _logger.LogDebug("Activity {ActivityId} filtered out - not a CustomerActivity (ActivitySource: {ActivitySource}): {SpanName}", 
                    activity.SpanId, activity.Source?.Name ?? "unknown", activity.DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing activity {ActivityId} for customer traces", activity.SpanId);
        }

        base.OnEnd(activity);
    }

    /// <summary>
    /// Determines if an activity should be processed for customer visibility.
    /// Only processes activities that originate from the CustomerTracer (ActivitySource = "Agent.Customer").
    /// This ensures that internal OpenTelemetry activities are completely excluded from customer logs.
    /// </summary>
    /// <param name="activity">The activity to evaluate.</param>
    /// <returns>True if the activity is a CustomerActivity and should be included in customer traces.</returns>
    private bool ShouldProcessActivityForCustomer(Activity activity)
    {
        if (activity == null)
            return false;

        // Only process activities from the CustomerTracer ActivitySource
        // This ensures we only emit CustomerActivity instances, not internal OpenTelemetry activities
        if (activity.Source?.Name != "Agent.Customer")
        {
            return false;
        }

        // Apply duration filtering if configured (only for customer activities)
        if (_options.MinimumDurationThreshold.HasValue && 
            activity.Duration < _options.MinimumDurationThreshold.Value)
        {
            return false;
        }

        // Since we're only processing CustomerActivity instances, we can skip the extensive filtering
        // that was needed for internal activities. CustomerActivity instances are already customer-safe.
        return true;
    }

    /// <summary>
    /// Processes a CustomerActivity that is already customer-safe.
    /// Since this is a CustomerActivity from the "Agent.Customer" ActivitySource,
    /// no additional filtering or sanitization is needed.
    /// </summary>
    /// <param name="activity">The CustomerActivity to process.</param>
    private void ProcessCustomerSafeActivity(Activity activity)
    {
        // Log any significant events (errors, warnings) that customers should be aware of
        LogCustomerRelevantEvents(activity);
    }

    /// <summary>
    /// Logs the start of a CustomerActivity.
    /// </summary>
    /// <param name="activity">The CustomerActivity that started.</param>
    private void LogCustomerTraceStart(Activity activity)
    {
        var properties = new Dictionary<string, string>
        {
            ["TraceId"] = activity.TraceId.ToString(),
            ["SpanId"] = activity.SpanId.ToString(),
            ["SpanName"] = activity.DisplayName ?? "CustomerActivity",
            ["StartTime"] = activity.StartTimeUtc.ToString("O"),
            ["TraceType"] = "CustomerTrace",
            ["ActivitySource"] = activity.Source?.Name ?? "Agent.Customer"
        };

        _customerLogger.LogCustomEvent("TraceActivityStarted", properties);
    }

    /// <summary>
    /// Logs customer-relevant events from the activity.
    /// </summary>
    /// <param name="activity">The activity to extract events from.</param>
    private void LogCustomerRelevantEvents(Activity activity)
    {
        foreach (var activityEvent in activity.Events)
        {
            // Only log events that are relevant to customers (errors, warnings, significant milestones)
            if (IsCustomerRelevantEvent(activityEvent))
            {
                var properties = new Dictionary<string, string>
                {
                    ["TraceId"] = activity.TraceId.ToString(),
                    ["SpanId"] = activity.SpanId.ToString(),
                    ["EventName"] = activityEvent.Name,
                    ["EventTime"] = activityEvent.Timestamp.ToString("O"),
                    ["TraceType"] = "CustomerTraceEvent"
                };

                // Add safe event attributes
                foreach (var tag in activityEvent.Tags.Take(5)) // Limit to prevent excessive properties
                {
                    if (!CustomerTraceFilteringUtilities.SensitiveAttributes.Contains(tag.Key))
                    {
                        properties[$"event.{tag.Key}"] = tag.Value?.ToString() ?? "";
                    }
                }

                _customerLogger.LogMessage($"Trace event: {activityEvent.Name}", properties);
            }
        }
    }

    /// <summary>
    /// Exports the activity to customer-safe trace exporters.
    /// </summary>
    /// <param name="activity">The activity to export.</param>
    private void ExportToCustomerSafeExporters(Activity activity)
    {
        var batch = new Batch<Activity>(new[] { activity }, 1);
        
        foreach (var exporter in _customerExporters)
        {
            try
            {
                exporter.Export(batch);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export activity {ActivityId} to customer exporter {ExporterType}",
                    activity.SpanId, exporter.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Determines if an activity event is relevant to customers.
    /// </summary>
    /// <param name="activityEvent">The activity event to evaluate.</param>
    /// <returns>True if the event should be visible to customers.</returns>
    private static bool IsCustomerRelevantEvent(ActivityEvent activityEvent)
    {
        var eventName = activityEvent.Name?.ToLowerInvariant() ?? "";
        
        // Include events that indicate issues or significant milestones
        var relevantEventTypes = new[]
        {
            "error",
            "exception",
            "warning",
            "completed",
            "started",
            "failed"
        };

        // Exclude internal system events
        var internalEventTypes = new[]
        {
            "debug",
            "internal",
            "handoff",
            "token",
            "auth"
        };

        return relevantEventTypes.Any(type => eventName.Contains(type)) &&
               !internalEventTypes.Any(type => eventName.Contains(type));
    }
        
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var exporter in _customerExporters)
            {
                exporter?.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
