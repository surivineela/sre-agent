using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

namespace Agent.Logging;

/// <summary>
/// Customer-facing logger that provides both traditional logging and customer-safe tracing capabilities.
/// This logger automatically filters out sensitive information such as tokens, handoff details, 
/// and internal system operations from traces that customers can access.
/// </summary>
public class CustomerLogger : ApplicationInsightsLogger, IDisposable
{
    private CustomerTraceProcessor? _traceProcessor;
    private readonly object _tracingLock = new object();
    private bool _tracingInitialized = false;
    
    // Customer tracer for independent customer-facing tracing
    private readonly CustomerTracer _customerTracer;
    
    // Thread-local correlation for grouping related log messages
    private static readonly AsyncLocal<string> _currentCorrelationId = new AsyncLocal<string>();
    private static readonly AsyncLocal<DateTimeOffset> _correlationStartTime = new AsyncLocal<DateTimeOffset>();
    private const int CorrelationTimeoutMinutes = 5;

    public CustomerLogger() : base()
    {
        _customerTracer = new CustomerTracer(this, tracingEnabled: true);
    }

    public CustomerLogger(string connectionString) : base(connectionString)
    {
        _customerTracer = new CustomerTracer(this, tracingEnabled: true);
    }

    public CustomerLogger(string connectionString, bool customerTracingEnabled) : base(connectionString)
    {
        _customerTracer = new CustomerTracer(this, tracingEnabled: customerTracingEnabled);
    }

    /// <summary>
    /// Logs a message with automatic trace correlation when OpenTelemetry context is available.
    /// The trace information is automatically included without requiring any additional parameters.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void LogMessage(string message)
    {
        var enrichedProperties = EnrichWithTraceContext(null);
        base.LogMessage(message, SeverityLevel.Information, enrichedProperties);
    }

    /// <summary>
    /// Logs a message with automatic trace correlation when OpenTelemetry context is available.
    /// The trace information is automatically included and merged with provided properties.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="properties">Additional properties to include with the message.</param>
    public void LogMessage(string message, IDictionary<string, string>? properties)
    {
        var enrichedProperties = EnrichWithTraceContext(properties);
        base.LogMessage(message, SeverityLevel.Information, enrichedProperties);
    }

    /// <summary>
    /// Logs an error message with automatic trace correlation when OpenTelemetry context is available.
    /// The trace information is automatically included without requiring any additional parameters.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    public void LogError(string message)
    {
        var enrichedProperties = EnrichWithTraceContext(null);
        base.LogMessage(message, SeverityLevel.Error, enrichedProperties);
    }

    /// <summary>
    /// Logs an exception with automatic trace correlation when OpenTelemetry context is available.
    /// The trace information is automatically included without requiring any additional parameters.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    public void LogException(Exception ex)
    {
        LogException(ex, ex.Message);
    }

    /// <summary>
    /// Logs an exception with additional context and automatic trace correlation when OpenTelemetry context is available.
    /// The trace information is automatically included without requiring any additional parameters.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">Additional message context.</param>
    public new void LogException(Exception ex, string message)
    {
        // For exceptions, we want to include trace context in the message as well
        var enrichedProperties = EnrichWithTraceContext(null);
        
        // Add exception-specific trace context to the message
        var contextualMessage = message;
        if (Activity.Current != null)
        {
            contextualMessage = $"{message} [TraceId: {Activity.Current.TraceId}]";
        }
        
        base.LogException(ex, contextualMessage);
        
        // Also log the trace context as additional telemetry
        if (enrichedProperties?.Count > 0)
        {
            base.LogMessage($"Exception context: {message}", SeverityLevel.Error, enrichedProperties);
        }
    }

    public new void LogCustomEvent(string eventName, Dictionary<string, string> properties)
    {
        var enrichedProperties = EnrichWithTraceContext(properties);
        if (enrichedProperties == null || enrichedProperties.Count == 0)
        {
            enrichedProperties = new Dictionary<string, string>
            {
                ["LogTimestamp"] = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        base.LogCustomEvent(eventName, enrichedProperties);
    }

    public void LogToolEvents(string toolEventName, Dictionary<string, string> properties)
    {
        var enrichedProperties = EnrichWithTraceContext(properties) ?? new Dictionary<string, string>();
        enrichedProperties.Add("gen_ai.operation.name", "execute_tool");
        enrichedProperties.Add("gen_ai.tool.name", toolEventName);

        // remove agent related properties from tool events
        enrichedProperties.Remove("gen_ai.agent.name");

        DependencyTelemetry dependency = new DependencyTelemetry
        {
            Name = toolEventName,
            Type = "ToolCall",
            Data = toolEventName,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            Success = true
        };

        foreach (var kvp in enrichedProperties)
        {
            dependency.Properties.Add(kvp.Key, kvp.Value);
        }
    }

    public void LogToolEvents(string toolEventName, string executionId, string output, Dictionary<string, string> properties, bool isExecutionFailed = false)
    {
        var enrichedProperties = EnrichWithTraceContext(properties) ?? new Dictionary<string, string>();
        enrichedProperties.Add("gen_ai.operation.name", "execute_tool");
        enrichedProperties.Add("gen_ai.tool.name", toolEventName);
        enrichedProperties.Add("gen_ai.tool.execution_id", executionId);

        // remove agent related properties from tool events
        enrichedProperties.Remove("gen_ai.agent.name");

        DependencyTelemetry dependency = new DependencyTelemetry
        {
            Name = toolEventName,
            Type = "ToolCall",
            Data = toolEventName,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            Success = !isExecutionFailed
        };

        foreach (var kvp in enrichedProperties)
        {
            dependency.Properties.Add(kvp.Key, kvp.Value);
        }

        base.LogDependency(dependency);
    }

    public void LogAgentResponseEvents(string agentEventName, string response, Dictionary<string, string> properties)
    {
        var enrichedProperties = EnrichWithTraceContext(properties) ?? new Dictionary<string, string>();
        var formattedResponse = FormatInOutCommunications(response);
        DependencyTelemetry dependency = new DependencyTelemetry
        {
            Name = agentEventName,
            Type = "GenAI",
            Data = agentEventName,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            Success = true
        };
        enrichedProperties.Add("gen_ai.operation.name", "invoke_agent");
        enrichedProperties.Add("gen_ai.output.messages", formattedResponse);
        foreach (var kvp in enrichedProperties)
        {
            dependency.Properties.Add(kvp.Key, kvp.Value);
        }
        base.LogDependency(dependency);
    }

    public void LogUserInputEvents(string userInputEventName, string userInput, Dictionary<string, string> properties, bool isStartOfTrace = false)
    {
        var enrichedProperties = EnrichWithTraceContext(properties) ?? new Dictionary<string, string>();
        var formattedInput= FormatInOutCommunications(userInput, isUserInput: true);
        DependencyTelemetry dependency = new DependencyTelemetry
        {
            Name = userInputEventName,
            Type = "GenAI",
            Data = userInputEventName,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            Success = true
        };

        enrichedProperties.Add("gen_ai.operation.name", "invoke_agent");
        enrichedProperties.Add("gen_ai.input.messages", formattedInput);
        foreach (var kvp in enrichedProperties)
        {
            dependency.Properties.Add(kvp.Key, kvp.Value);
        }
        base.LogDependency(dependency, isStartOfTrace);
    }

    public new async Task FlushAsync(CancellationToken cancellationToken)
    {
        await base.FlushAsync(cancellationToken);
        
        // Note: Tracing flush is handled by the overall TracerProvider, not individual processors
    }

    /// <summary>
    /// Initializes customer-safe tracing with the specified configuration.
    /// This creates a processor that can be added to existing tracing infrastructure.
    /// </summary>
    /// <param name="options">Options for configuring customer trace processing.</param>
    /// <param name="customerExporters">Optional customer-safe trace exporters.</param>
    /// <param name="logger">Optional logger for debugging trace filtering.</param>
    /// <returns>A CustomerTraceProcessor that can be added to TracerProvider configuration.</returns>
    public CustomerTraceProcessor InitializeCustomerTracing(
        CustomerTraceProcessorOptions? options = null,
        IEnumerable<BaseExporter<Activity>>? customerExporters = null,
        ILogger? logger = null)
    {
        lock (_tracingLock)
        {
            if (_tracingInitialized)
            {
                return _traceProcessor!; // Already initialized
            }

            try
            {
                _traceProcessor = new CustomerTraceProcessor(this, options, logger, customerExporters);
                _tracingInitialized = true;

                LogMessage("Customer tracing initialized successfully", new Dictionary<string, string>
                {
                    ["TracingEnabled"] = "true",
                    ["InitializedAt"] = DateTimeOffset.UtcNow.ToString("O")
                });

                return _traceProcessor;
            }
            catch (Exception ex)
            {
                LogException(ex, "Failed to initialize customer tracing");
                throw;
            }
        }
    }

    /// <summary>
    /// Initializes customer-safe tracing with Azure Data Explorer export capabilities.
    /// </summary>
    /// <param name="adxOptions">Azure Data Explorer configuration for customer traces.</param>
    /// <param name="processorOptions">Options for trace processing and filtering.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    /// <returns>A CustomerTraceProcessor that can be added to TracerProvider configuration.</returns>
    public CustomerTraceProcessor InitializeCustomerTracingWithADX(
        CustomerTraceExporterOptions adxOptions,
        CustomerTraceProcessorOptions? processorOptions = null,
        ILogger? logger = null)
    {
        var customerExporter = new CustomerTraceExporter(adxOptions);
        return InitializeCustomerTracing(processorOptions, new[] { customerExporter }, logger);
    }

    /// <summary>
    /// Gets the customer tracer for creating customer-facing activities.
    /// </summary>
    public CustomerTracer CustomerTracer => _customerTracer;

    /// <summary>
    /// Starts a customer activity for processing a user message.
    /// </summary>
    /// <param name="messageContent">The user message content.</param>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="userId">The user identifier (optional).</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartMessageProcessing(string messageContent, Guid threadId, string? userId = null)
    {
        return _customerTracer.StartMessageProcessing(messageContent, threadId, userId);
    }

    /// <summary>
    /// Starts a customer activity for incident handling.
    /// </summary>
    /// <param name="incidentId">The incident identifier.</param>
    /// <param name="incidentType">The type of incident.</param>
    /// <param name="severity">The incident severity (optional).</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartIncidentHandling(string incidentId, string incidentType, string? severity = null)
    {
        return _customerTracer.StartIncidentHandling(incidentId, incidentType, severity);
    }

    /// <summary>
    /// Starts a customer activity for a specific agent operation.
    /// </summary>
    /// <param name="agentName">The name of the agent.</param>
    /// <param name="operationName">The specific operation being performed.</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartAgentOperation(string agentName, string operationName)
    {
        return _customerTracer.StartAgentOperation(agentName, operationName);
    }

    /// <summary>
    /// Starts a generic customer operation.
    /// </summary>
    /// <param name="operationName">The name of the customer operation.</param>
    /// <param name="operationType">The type of operation.</param>
    /// <returns>A disposable customer activity or null if tracing is disabled.</returns>
    public CustomerActivity? StartCustomerOperation(string operationName, string operationType = "user_operation")
    {
        return _customerTracer.StartUserOperation(operationName, operationType);
    }

    /// <summary>
    /// Gets the current customer trace context if available.
    /// </summary>
    /// <returns>Customer trace context or null if no active customer activity.</returns>
    public CustomerTraceContext? GetCurrentCustomerTraceContext()
    {
        return _customerTracer.GetCurrentTraceContext();
    }

    /// <summary>
    /// Logs a customer-safe trace event with the specified properties.
    /// This method ensures that the trace data is automatically filtered for customer safety.
    /// </summary>
    /// <param name="eventName">The name of the trace event.</param>
    /// <param name="properties">Properties to include with the trace event.</param>
    /// <param name="spanName">Optional custom span name for the trace.</param>
    public void LogCustomerTrace(string eventName, Dictionary<string, string> properties, string? spanName = null)
    {
        // Log the event through traditional logging
        LogCustomEvent(eventName, properties);

        // Create a customer-safe trace span using the customer tracer
        using var customerActivity = _customerTracer.StartUserOperation(spanName ?? eventName, "trace_event");
        if (customerActivity != null)
        {
            // Add safe properties as attributes
            foreach (var prop in properties)
            {
                customerActivity.AddAttribute(prop.Key, prop.Value);
            }

            customerActivity.RecordEvent(eventName, "Customer trace event logged");
            customerActivity.SetSuccess($"Logged event: {eventName}");
        }
    }

    /// <summary>
    /// Logs an error event with customer-safe tracing.
    /// </summary>
    /// <param name="errorMessage">The error message to log.</param>
    /// <param name="exception">Optional exception details.</param>
    /// <param name="properties">Additional properties for the error event.</param>
    public void LogCustomerError(string errorMessage, Exception? exception = null, Dictionary<string, string>? properties = null)
    {
        var errorProps = properties ?? new Dictionary<string, string>();
        errorProps["error.message"] = errorMessage;
        errorProps["error.timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        if (exception != null)
        {
            errorProps["error.type"] = exception.GetType().Name;
            LogException(exception, errorMessage);
        }
        else
        {
            LogError(errorMessage);
        }

        LogCustomerTrace("CustomerError", errorProps, "customer.error");
    }

    /// <summary>
    /// Logs the start of a customer operation with tracing.
    /// </summary>
    /// <param name="operationName">Name of the operation being started.</param>
    /// <param name="properties">Properties describing the operation.</param>
    /// <returns>An IDisposable that should be disposed when the operation completes.</returns>
    public IDisposable? StartCustomerOperation(string operationName, Dictionary<string, string>? properties = null)
    {
        var operationProps = properties ?? new Dictionary<string, string>();
        operationProps["operation.name"] = operationName;
        operationProps["operation.started"] = DateTimeOffset.UtcNow.ToString("O");

        LogCustomerTrace("CustomerOperationStarted", operationProps, $"customer.operation.{operationName}");

        // Return a disposable that will log completion
        return new CustomerOperationScope(this, operationName, operationProps);
    }

    /// <summary>
    /// ActivitySource for customer tracing operations.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new ActivitySource("Agent.Customer.Tracing");

    /// <summary>
    /// Gets or creates a correlation ID for grouping related log messages within a time window.
    /// This helps correlate messages that happen in the same logical operation even without OpenTelemetry context.
    /// </summary>
    /// <param name="now">Current timestamp for correlation timeout logic.</param>
    /// <returns>Correlation ID for this log context.</returns>
    private static string GetOrCreateCorrelationId(DateTimeOffset now)
    {
        var existingId = _currentCorrelationId.Value;
        var startTime = _correlationStartTime.Value;
        
        // Check if we have a valid correlation context
        if (!string.IsNullOrEmpty(existingId) && 
            startTime != default && 
            now - startTime < TimeSpan.FromMinutes(CorrelationTimeoutMinutes))
        {
            return existingId;
        }
        
        // Create a new correlation context
        var newId = Guid.NewGuid().ToString("N")[..12]; // 12-character correlation ID
        _currentCorrelationId.Value = newId;
        _correlationStartTime.Value = now;
        
        return newId;
    }

    /// <summary>
    /// Disposes of tracing resources when the logger is disposed.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Enriches properties with current customer trace context if available.
    /// Uses the dedicated customer tracer for clean trace context without internal system information.
    /// </summary>
    /// <param name="existingProperties">Existing properties to merge with trace context.</param>
    /// <returns>Dictionary with trace context added, or correlation info if no active trace available.</returns>
    private Dictionary<string, string>? EnrichWithTraceContext(IDictionary<string, string>? existingProperties)
    {
        // Create enriched properties dictionary
        var enriched = existingProperties != null 
            ? new Dictionary<string, string>(existingProperties)
            : new Dictionary<string, string>();

        // Construct Agent ARM Id and emit it under gen_ai.agent.id
        // Foundry NextGen UI uses this field in customDimension to group traces and display in the UI
        var agentName = Environment.GetEnvironmentVariable("AGENT_NAME");
        var agentSubId = Environment.GetEnvironmentVariable("AGENT_SUBSCRIPTION_ID");
        var agentResourceGroup = Environment.GetEnvironmentVariable("AGENT_RESOURCE_GROUP");
        if (!string.IsNullOrEmpty(agentName) && !string.IsNullOrEmpty(agentSubId) && !string.IsNullOrEmpty(agentResourceGroup))
        {
            // trim down the last part of the agent name divided by "--"
            agentName = agentName.LastIndexOf("--") > 0 
                ? agentName[..agentName.LastIndexOf("--")] 
                : agentName;
            var agentArmId = $"/subscriptions/{agentSubId}/resourceGroups/{agentResourceGroup}/providers/Microsoft.App/agents/{agentName}";
            enriched["gen_ai.agent.id"] = agentArmId;
            enriched["gen_ai.agent.name"] = agentName;
        }

        // First, try to get customer trace context from the context manager
        var customerTraceInfo = CustomerTraceManager.GetCurrentTraceInfo();
        if (customerTraceInfo != null)
        {
            // Merge customer trace information
            foreach (var kvp in customerTraceInfo)
            {
                enriched[kvp.Key] = kvp.Value;
            }
            
            enriched["LogTimestamp"] = DateTimeOffset.UtcNow.ToString("O");
            return enriched;
        }

        // Fallback: Try to get customer trace context directly from the tracer
        var customerTraceContext = _customerTracer.GetCurrentTraceContext();
        if (customerTraceContext != null)
        {
            enriched["TraceId"] = customerTraceContext.TraceId;
            enriched["SpanId"] = customerTraceContext.SpanId;
            
            if (!string.IsNullOrEmpty(customerTraceContext.ParentSpanId))
            {
                enriched["ParentSpanId"] = customerTraceContext.ParentSpanId;
            }

            if (!string.IsNullOrEmpty(customerTraceContext.OperationName))
            {
                enriched["OperationName"] = customerTraceContext.OperationName;
            }

            if (!string.IsNullOrEmpty(customerTraceContext.OperationType))
            {
                enriched["OperationType"] = customerTraceContext.OperationType;
            }

            enriched["TraceContext"] = "customer_trace";
            enriched["TraceSource"] = "customer_tracer";
            enriched["LogTimestamp"] = DateTimeOffset.UtcNow.ToString("O");
            
            return enriched;
        }

        // No trace context available - use correlation ID for grouping related logs
        var now = DateTimeOffset.UtcNow;
        var correlationId = GetOrCreateCorrelationId(now);
        
        enriched["CorrelationId"] = correlationId;
        enriched["LogSource"] = "customer_logger";
        enriched["LogTimestamp"] = now.ToString("O");
        enriched["TraceContext"] = "correlation";
        
        return enriched;
    }

    private string FormatInOutCommunications(string response, bool isUserInput = false)
    {
        var formattedResponse = new[]
        {
            new
            {
                role = isUserInput ? "user": "assistant",
                parts = new[]
                {
                    new
                    {
                        type = "text",
                        content = response
                    }
                }
            }
        };
        return JsonSerializer.Serialize(formattedResponse);
    }

    /// <summary>
    /// Disposes of tracing resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Note: Don't dispose TracerProvider here as it's managed by the overall tracing infrastructure
            _traceProcessor?.Dispose();
            _customerTracer?.Dispose();
        }
    }

    /// <summary>
    /// Helper class for managing customer operation scopes.
    /// </summary>
    private class CustomerOperationScope : IDisposable
    {
        private readonly CustomerLogger _logger;
        private readonly string _operationName;
        private readonly Dictionary<string, string> _properties;
        private readonly DateTimeOffset _startTime;
        private bool _disposed = false;

        public CustomerOperationScope(CustomerLogger logger, string operationName, Dictionary<string, string> properties)
        {
            _logger = logger;
            _operationName = operationName;
            _properties = properties;
            _startTime = DateTimeOffset.UtcNow;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                var endTime = DateTimeOffset.UtcNow;
                var duration = endTime - _startTime;

                var completionProps = new Dictionary<string, string>(_properties)
                {
                    ["operation.completed"] = endTime.ToString("O"),
                    ["operation.duration.ms"] = duration.TotalMilliseconds.ToString("F2")
                };

                _logger.LogCustomerTrace("CustomerOperationCompleted", completionProps, $"customer.operation.{_operationName}");
                _disposed = true;
            }
        }
    }
}
