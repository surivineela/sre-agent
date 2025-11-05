// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;

namespace Agent.Logging;

/// <summary>
/// Utility class for filtering sensitive information from traces that should not be exposed to customers.
/// This includes tokens, detailed handoff information, and internal system operations.
/// </summary>
public static class CustomerTraceFilteringUtilities
{
    /// <summary>
    /// Set of trace attributes that contain sensitive information and should be filtered out for customer traces.
    /// </summary>
    public static readonly HashSet<string> SensitiveAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Token and authentication related
        TraceAttribute.ModelInputTokensCount,
        TraceAttribute.ModelOutputTokensCount,
        TraceAttribute.ModelTotalTokensCount,
        
        // Handoff details that reveal internal agent architecture
        TraceAttribute.HandoffAgentName,
        TraceAttribute.HandoffReasoning,
        
        // Internal tool details that may expose system architecture
        TraceAttribute.ToolInput,
        TraceAttribute.ToolOutput,
        TraceAttribute.ToolDescription,
        
        // Model internals that may expose prompting strategies
        TraceAttribute.ModelInput,
        TraceAttribute.ModelOutput,
        TraceAttribute.ModelTools,
        TraceAttribute.ModelTemperature,
        
        // Internal agent task details
        TraceAttribute.AgentTaskInput,
        TraceAttribute.AgentTaskStep,
        
        // Internal approval and reasoning details
        TraceAttribute.ApprovalDescription,
        TraceAttribute.TriggeredBy,
        TraceAttribute.TriggeredMessage
    };

    /// <summary>
    /// Set of operation names that represent internal system operations and should be filtered out for customer traces.
    /// </summary>
    private static readonly HashSet<string> InternalOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        TraceOperationName.Handoff,
        TraceOperationName.ModelGeneration,
        TraceOperationName.Critic,
        TraceOperationName.Summarizer,
        TraceOperationName.UserApproval,
        TraceOperationName.UserContinueTool
    };

    /// <summary>
    /// Set of span purposes that are internal and should be filtered out for customer traces.
    /// </summary>
    private static readonly HashSet<string> InternalSpanPurposes = new(StringComparer.OrdinalIgnoreCase)
    {
        "internal",
        "debug",
        "system",
        "handoff",
        "model_generation",
        "token_processing"
    };

    /// <summary>
    /// Determines if an activity should be included in customer traces.
    /// </summary>
    /// <param name="activity">The activity to evaluate.</param>
    /// <returns>True if the activity should be included in customer traces, false otherwise.</returns>
    public static bool ShouldIncludeActivityInCustomerTraces(Activity activity)
    {
        if (activity == null)
            return false;

        // Filter out activities with internal operation names
        var operationName = activity.Tags.FirstOrDefault(t => t.Key == TraceAttribute.OperationName).Value;
        if (operationName != null && InternalOperations.Contains(operationName))
            return false;

        // Filter out activities with internal span purposes
        var spanPurpose = activity.Tags.FirstOrDefault(t => t.Key == TraceAttribute.SpanPurpose).Value;
        if (spanPurpose != null && InternalSpanPurposes.Contains(spanPurpose))
            return false;

        // Filter out activities that are purely internal tool operations
        var toolName = activity.Tags.FirstOrDefault(t => t.Key == TraceAttribute.ToolName).Value;
        if (toolName != null && IsInternalTool(toolName))
            return false;

        return true;
    }

    /// <summary>
    /// Creates a sanitized copy of trace data suitable for customer exposure by removing sensitive attributes.
    /// </summary>
    /// <param name="traceData">The original trace data dictionary.</param>
    /// <returns>A new dictionary with sensitive attributes removed.</returns>
    public static Dictionary<string, object> SanitizeTraceDataForCustomer(Dictionary<string, object> traceData)
    {
        var sanitizedData = new Dictionary<string, object>();

        foreach (var kvp in traceData)
        {
            if (!SensitiveAttributes.Contains(kvp.Key))
            {
                // Include safe attributes, but sanitize their values if needed
                sanitizedData[kvp.Key] = SanitizeAttributeValue(kvp.Key, kvp.Value);
            }
        }

        // Always include basic trace correlation information
        if (!sanitizedData.ContainsKey("TraceId"))
            sanitizedData["TraceId"] = traceData.GetValueOrDefault("TraceId", "");
        
        if (!sanitizedData.ContainsKey("SpanId"))
            sanitizedData["SpanId"] = traceData.GetValueOrDefault("SpanId", "");

        // Add customer-safe metadata
        sanitizedData["CustomerTrace"] = true;
        sanitizedData["FilteredAt"] = DateTimeOffset.UtcNow.ToString("O");

        return sanitizedData;
    }

    /// <summary>
    /// Creates a sanitized version of an Activity suitable for customer exposure.
    /// </summary>
    /// <param name="activity">The original activity.</param>
    /// <returns>A dictionary representing the sanitized activity data.</returns>
    public static Dictionary<string, object> CreateCustomerSafeActivityData(Activity activity)
    {
        var safeData = new Dictionary<string, object>
        {
            ["TraceId"] = activity.TraceId.ToString(),
            ["SpanId"] = activity.SpanId.ToString(),
            ["ParentSpanId"] = activity.ParentSpanId.ToString(),
            ["SpanName"] = SanitizeSpanName(activity.DisplayName),
            ["SpanKind"] = activity.Kind.ToString(),
            ["StartTime"] = activity.StartTimeUtc,
            ["EndTime"] = activity.StartTimeUtc.AddTicks(activity.Duration.Ticks),
            ["Duration"] = activity.Duration.TotalMilliseconds,
            ["Status"] = activity.Status.ToString(),
            ["CustomerTrace"] = true,
            ["FilteredAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        // Add safe attributes only
        var safeAttributes = new Dictionary<string, object>();
        foreach (var tag in activity.Tags)
        {
            if (!SensitiveAttributes.Contains(tag.Key))
            {
                safeAttributes[tag.Key] = SanitizeAttributeValue(tag.Key, tag.Value ?? string.Empty);
            }
        }
        safeData["Attributes"] = safeAttributes;

        // Add sanitized events (if any)
        var safeEvents = activity.Events
            .Where(e => !IsInternalEvent(e.Name))
            .Select(e => new
            {
                Name = e.Name,
                Timestamp = e.Timestamp,
                // Don't include event attributes as they may contain sensitive data
                AttributeCount = e.Tags.Count()
            })
            .ToList();
        
        if (safeEvents.Any())
        {
            safeData["Events"] = safeEvents;
        }

        return safeData;
    }

    /// <summary>
    /// Sanitizes an attribute value to ensure it doesn't contain sensitive information.
    /// </summary>
    private static object SanitizeAttributeValue(string attributeName, object value)
    {
        if (value == null)
            return string.Empty;

        var stringValue = value.ToString() ?? "";

        // Sanitize known patterns that might contain sensitive data
        if (attributeName.Contains("url", StringComparison.OrdinalIgnoreCase))
        {
            // Remove query parameters that might contain tokens
            var uri = stringValue;
            var queryIndex = uri.IndexOf('?');
            if (queryIndex >= 0)
            {
                uri = uri.Substring(0, queryIndex);
            }
            return uri;
        }

        if (attributeName.Contains("message", StringComparison.OrdinalIgnoreCase))
        {
            // Limit message length to prevent exposure of large internal data
            return stringValue.Length > 500 ? stringValue.Substring(0, 500) + "..." : stringValue;
        }

        return value;
    }

    /// <summary>
    /// Sanitizes a span name to remove internal implementation details.
    /// </summary>
    private static string SanitizeSpanName(string spanName)
    {
        if (string.IsNullOrEmpty(spanName))
            return spanName ?? string.Empty;

        // Remove internal prefixes that expose system architecture
        var internalPrefixes = new[] { "internal.", "system.", "debug.", "agent.internal." };
        foreach (var prefix in internalPrefixes)
        {
            if (spanName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return spanName.Substring(prefix.Length);
            }
        }

        return spanName;
    }

    /// <summary>
    /// Determines if a tool name represents an internal system tool.
    /// </summary>
    private static bool IsInternalTool(string toolName)
    {
        var internalToolPatterns = new[]
        {
            "handoff",
            "transfer_to_",
            "debug_",
            "internal_",
            "system_"
        };

        return internalToolPatterns.Any(pattern => 
            toolName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if an event name represents an internal system event.
    /// </summary>
    private static bool IsInternalEvent(string eventName)
    {
        var internalEventPatterns = new[]
        {
            "internal",
            "debug",
            "system",
            "handoff",
            "token",
            "auth"
        };

        return internalEventPatterns.Any(pattern => 
            eventName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
