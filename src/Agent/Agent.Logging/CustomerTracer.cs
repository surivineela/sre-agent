// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Logging;
using OpenTelemetry.Trace;

namespace Agent.Logging;

/// <summary>
/// Customer-facing tracer that provides safe tracing capabilities without exposing sensitive internal information.
/// This tracer operates independently from internal system tracing and creates customer-appropriate spans.
/// </summary>
public class CustomerTracer : IDisposable
{
    private static readonly ActivitySource CustomerActivitySource = new ActivitySource("Agent.Customer");
    private readonly CustomerLogger _customerLogger;
    private readonly bool _tracingEnabled;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the CustomerTracer.
    /// </summary>
    /// <param name="customerLogger">The customer logger to use for trace correlation.</param>
    /// <param name="tracingEnabled">Whether customer tracing is enabled.</param>
    public CustomerTracer(CustomerLogger customerLogger, bool tracingEnabled = true)
    {
        _customerLogger = customerLogger;
        _tracingEnabled = tracingEnabled;
    }

    /// <summary>
    /// Starts a new customer activity for a user operation.
    /// </summary>
    /// <param name="operationName">The name of the customer operation.</param>
    /// <param name="operationType">The type of operation (e.g., "message_processing", "incident_handling").</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartUserOperation(string operationName, string operationType = "user_operation")
    {
        if (!_tracingEnabled)
        {
            return null;
        }

        var activity = CustomerActivitySource.StartActivity($"customer.{operationType}");
        if (activity == null)
        {
            return null;
        }

        // Set customer-safe attributes
        activity.SetTag("operation.name", operationName);
        activity.SetTag("operation.type", operationType);
        activity.SetTag("customer.facing", "true");
        activity.SetTag("started.at", DateTimeOffset.UtcNow.ToString("O"));

        return new CustomerActivity(activity, _customerLogger);
    }

    /// <summary>
    /// Starts a new customer activity for processing a user message.
    /// </summary>
    /// <param name="messageContent">The user message content (will be truncated for safety).</param>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="userId">The user identifier (optional).</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartMessageProcessing(string messageContent, Guid threadId, string? userId = null)
    {
        if (!_tracingEnabled)
        {
            return null;
        }

        var activity = CustomerActivitySource.StartActivity("customer.message.processing");
        if (activity == null)
        {
            return null;
        }

        // Set customer-safe attributes with length limits
        activity.SetTag("thread.id", threadId.ToString());
        activity.SetTag("message.length", messageContent.Length.ToString());
        activity.SetTag("message.preview", TruncateForSafety(messageContent, 100));
        activity.SetTag("operation.type", "message_processing");

        if (!string.IsNullOrEmpty(userId))
        {
            activity.SetTag("user.id", SanitizeUserId(userId));
        }

        activity.SetTag("started.at", DateTimeOffset.UtcNow.ToString("O"));

        return new CustomerActivity(activity, _customerLogger);
    }

    /// <summary>
    /// Starts a new customer activity for incident handling.
    /// </summary>
    /// <param name="incidentId">The incident identifier.</param>
    /// <param name="incidentType">The type of incident.</param>
    /// <param name="severity">The incident severity (optional).</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartIncidentHandling(string incidentId, string incidentType, string? severity = null)
    {
        if (!_tracingEnabled)
        {
            return null;
        }

        var activity = CustomerActivitySource.StartActivity("customer.incident.handling");
        if (activity == null)
        {
            return null;
        }

        // Set incident-specific attributes
        activity.SetTag("incident.id", incidentId);
        activity.SetTag("incident.type", incidentType);
        activity.SetTag("operation.type", "incident_handling");

        if (!string.IsNullOrEmpty(severity))
        {
            activity.SetTag("incident.severity", severity);
        }

        activity.SetTag("started.at", DateTimeOffset.UtcNow.ToString("O"));

        return new CustomerActivity(activity, _customerLogger);
    }

    /// <summary>
    /// Starts a new customer activity for a specific agent operation.
    /// </summary>
    /// <param name="agentName">The name of the agent.</param>
    /// <param name="operationName">The specific operation being performed.</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartAgentOperation(string agentName, string operationName)
    {
        if (!_tracingEnabled)
        {
            return null;
        }

        var activity = CustomerActivitySource.StartActivity($"customer.agent.{SanitizeAgentName(agentName)}");
        if (activity == null)
        {
            return null;
        }

        // Set agent-specific attributes
        activity.SetTag("agent.name", SanitizeAgentName(agentName));
        activity.SetTag("operation.name", operationName);
        activity.SetTag("operation.type", "agent_operation");
        activity.SetTag("started.at", DateTimeOffset.UtcNow.ToString("O"));

        return new CustomerActivity(activity, _customerLogger);
    }

    /// <summary>
    /// Gets the current customer trace context if available.
    /// </summary>
    /// <returns>Customer trace context or null if no active customer activity.</returns>
    public CustomerTraceContext? GetCurrentTraceContext()
    {
        var currentActivity = Activity.Current;

        // Only return context if it's from our customer activity source
        if (currentActivity?.Source?.Name == CustomerActivitySource.Name)
        {
            var tags = currentActivity.Tags.ToList();
            var tagsAsProperties = new Dictionary<string, string?>();
            if (tags.Count() != 0)
            {
                foreach (var tag in tags)
                {
                    tagsAsProperties[tag.Key] = tag.Value;
                }
            }
            return new CustomerTraceContext
            {
                TraceId = currentActivity.TraceId.ToString(),
                SpanId = currentActivity.SpanId.ToString(),
                ParentSpanId = currentActivity.ParentSpanId != default ? currentActivity.ParentSpanId.ToString() : null,
                OperationName = currentActivity.GetTagItem("operation.name")?.ToString(),
                OperationType = currentActivity.GetTagItem("operation.type")?.ToString(),
                StartTime = currentActivity.StartTimeUtc,
                Tags = tagsAsProperties
            };
        }

        return null;
    }

    /// <summary>
    /// Truncates content for safety, ensuring no sensitive information is exposed.
    /// </summary>
    private static string TruncateForSafety(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        // Remove common sensitive patterns
        var sanitized = content;

        // Remove potential tokens or keys (simple pattern matching)
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\b[A-Za-z0-9+/=]{20,}\b", "[TOKEN]");

        // Truncate to safe length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength] + "...";
        }

        return sanitized;
    }

    /// <summary>
    /// Sanitizes user ID to prevent exposure of sensitive identifiers.
    /// </summary>
    private static string SanitizeUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return "anonymous";
        }

        // Keep only the first 8 characters and hash pattern for correlation
        var prefix = userId.Length > 8 ? userId[..8] : userId;
        var hash = userId.GetHashCode().ToString("x8");
        return $"{prefix}...{hash}";
    }

    /// <summary>
    /// Sanitizes agent name to ensure only safe identifiers are exposed.
    /// </summary>
    private static string SanitizeAgentName(string agentName)
    {
        if (string.IsNullOrEmpty(agentName))
        {
            return "unknown_agent";
        }

        // Remove any potential sensitive prefixes and normalize
        var sanitized = agentName.ToLowerInvariant();

        // Remove common internal prefixes
        var prefixesToRemove = new[] { "internal.", "system.", "debug.", "test." };
        foreach (var prefix in prefixesToRemove)
        {
            if (sanitized.StartsWith(prefix))
            {
                sanitized = sanitized[prefix.Length..];
            }
        }

        // Keep only alphanumeric and safe characters
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^a-z0-9_]", "_");

        return string.IsNullOrEmpty(sanitized) ? "agent" : sanitized;
    }

    /// <summary>
    /// Gets the ActivitySource used by this customer tracer.
    /// This can be used to configure OpenTelemetry to listen to customer activities.
    /// </summary>
    public static ActivitySource GetActivitySource() => CustomerActivitySource;

    /// <summary>
    /// Disposes of the customer tracer resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the customer tracer resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Note: Don't dispose the static ActivitySource as it may be shared
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents an active customer tracing activity that can be used to add context and complete the operation.
/// </summary>
public class CustomerActivity : IDisposable
{
    private readonly Activity _activity;
    private readonly CustomerLogger _customerLogger;
    private readonly DateTimeOffset _startTime;
    private bool _disposed = false;

    internal CustomerActivity(Activity activity, CustomerLogger customerLogger)
    {
        _activity = activity;
        _customerLogger = customerLogger;
        _startTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the trace ID of this customer activity.
    /// </summary>
    public string TraceId => _activity.TraceId.ToString();

    /// <summary>
    /// Gets the span ID of this customer activity.
    /// </summary>
    public string SpanId => _activity.SpanId.ToString();

    /// <summary>
    /// Adds a safe attribute to the customer activity.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value (will be sanitized if needed).</param>
    public void AddAttribute(string key, string value)
    {
        if (_disposed || string.IsNullOrEmpty(key))
        {
            return;
        }

        // Sanitize the key to ensure it's customer-safe
        var safeKey = SanitizeAttributeKey(key);
        var safeValue = SanitizeAttributeValue(value);

        _activity.SetTag(safeKey, safeValue);
    }

    /// <summary>
    /// Records a customer-safe event in the activity.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="description">Optional description of the event.</param>
    public void RecordEvent(string eventName, string? description = null)
    {
        if (_disposed || string.IsNullOrEmpty(eventName))
        {
            return;
        }

        var tags = new ActivityTagsCollection();
        if (!string.IsNullOrEmpty(description))
        {
            tags["description"] = SanitizeAttributeValue(description);
        }
        tags["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        _activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
    }

    /// <summary>
    /// Marks the customer activity as successful and logs completion.
    /// </summary>
    /// <param name="resultSummary">Optional summary of the operation result.</param>
    public void SetSuccess(string? resultSummary = null)
    {
        if (_disposed)
        {
            return;
        }

        _activity.SetStatus(ActivityStatusCode.Ok);
        _activity.SetTag("result.status", "success");

        if (!string.IsNullOrEmpty(resultSummary))
        {
            _activity.SetTag("result.summary", SanitizeAttributeValue(resultSummary));
        }

        LogCompletion("success");
    }

    /// <summary>
    /// Marks the customer activity as failed and logs the error.
    /// </summary>
    /// <param name="errorMessage">The error message (will be sanitized).</param>
    /// <param name="errorType">The type of error (optional).</param>
    public void SetError(string errorMessage, string? errorType = null)
    {
        if (_disposed)
        {
            return;
        }

        _activity.SetStatus(ActivityStatusCode.Error, SanitizeAttributeValue(errorMessage));
        _activity.SetTag("result.status", "error");
        _activity.SetTag("error.message", SanitizeAttributeValue(errorMessage));

        if (!string.IsNullOrEmpty(errorType))
        {
            _activity.SetTag("error.type", errorType);
        }

        LogCompletion("error");
    }

    /// <summary>
    /// Logs the completion of the customer activity.
    /// </summary>
    private void LogCompletion(string status)
    {
        var duration = DateTimeOffset.UtcNow - _startTime;
        var operationName = _activity.GetTagItem("operation.name")?.ToString() ?? "unknown_operation";

        _customerLogger.LogMessage($"Customer operation completed: {operationName}", new Dictionary<string, string>
        {
            ["TraceId"] = _activity.TraceId.ToString(),
            ["SpanId"] = _activity.SpanId.ToString(),
            ["OperationName"] = operationName,
            ["Status"] = status,
            ["Duration"] = duration.TotalMilliseconds.ToString("F2"),
            ["CompletedAt"] = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    /// <summary>
    /// Sanitizes attribute keys to ensure they're customer-safe.
    /// </summary>
    private static string SanitizeAttributeKey(string key)
    {
        // Ensure key doesn't contain sensitive prefixes
        var sensitiveKeyPrefixes = new[] { "internal.", "debug.", "system.", "secret.", "token.", "auth." };

        foreach (var prefix in sensitiveKeyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return $"customer.{key[prefix.Length..]}";
            }
        }

        return key.StartsWith("customer.") ? key : $"customer.{key}";
    }

    /// <summary>
    /// Sanitizes attribute values to prevent exposure of sensitive information.
    /// </summary>
    private static string SanitizeAttributeValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Limit length to prevent log explosion
        const int maxLength = 1000;
        var sanitized = value.Length > maxLength ? value[..maxLength] + "..." : value;

        // Remove potential tokens or sensitive data patterns
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\b[A-Za-z0-9+/=]{20,}\b", "[REDACTED]");

        return sanitized;
    }

    /// <summary>
    /// Disposes of the customer activity and completes the span.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Set duration and complete timestamp
            var duration = DateTimeOffset.UtcNow - _startTime;
            _activity.SetTag("duration.ms", duration.TotalMilliseconds.ToString("F2"));
            _activity.SetTag("completed.at", DateTimeOffset.UtcNow.ToString("O"));

            // Complete the activity
            _activity.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents the current customer trace context.
/// </summary>
public class CustomerTraceContext
{
    /// <summary>
    /// Gets or sets the trace ID.
    /// </summary>
    public required string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span ID.
    /// </summary>
    public required string SpanId { get; set; }

    /// <summary>
    /// Gets or sets the parent span ID if available.
    /// </summary>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// Gets or sets the start time of the trace.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    ///
    /// </summary>
    public Dictionary<string, string?> Tags { get; set; } = new Dictionary<string, string?>();
}
