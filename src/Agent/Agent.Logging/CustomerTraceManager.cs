// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;

namespace Agent.Logging;

/// <summary>
/// Manages customer trace context that spans across the entire request lifecycle,
/// from inbound communication through reasoning loops to outbound communication.
/// Uses AsyncLocal for thread-safe context propagation.
/// </summary>
public static class CustomerTraceManager
{
    private static readonly AsyncLocal<CustomerActivity?> _currentActivity = new AsyncLocal<CustomerActivity?>();
    private static readonly AsyncLocal<Dictionary<string, string>?> _currentMetadata = new AsyncLocal<Dictionary<string, string>?>();
    
    /// <summary>
    /// Gets the current customer activity if one is active.
    /// </summary>
    /// <returns>The current customer activity or null if none is active.</returns>
    public static CustomerActivity? GetCurrentActivity()
    {
        return _currentActivity.Value;
    }
    
    /// <summary>
    /// Gets the current trace information as a dictionary suitable for logging enrichment.
    /// </summary>
    /// <returns>Dictionary containing trace information or null if no trace is active.</returns>
    public static Dictionary<string, string>? GetCurrentTraceInfo()
    {
        var activity = _currentActivity.Value;
        var metadata = _currentMetadata.Value ?? new Dictionary<string, string>();

        if (activity != null)
        {
            var traceInfo = new Dictionary<string, string>(metadata)
            {
                ["TraceId"] = activity.TraceId,
                ["SpanId"] = activity.SpanId,
                ["TraceContext"] = "customer_trace",
                ["TraceSource"] = "customer_tracer"
            };
            
            return traceInfo;
        }
        
        return metadata.Any() ? metadata : null;
    }
    
    /// <summary>
    /// Sets metadata that will be included in trace information.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public static void SetMetadata(string key, string value)
    {
        if (_currentMetadata.Value == null)
        {
            _currentMetadata.Value = new Dictionary<string, string>();
        }
        
        _currentMetadata.Value[key] = value;
    }
    
    /// <summary>
    /// Starts a new customer trace scope that will be maintained across async operations.
    /// The scope should be disposed to clean up resources.
    /// </summary>
    /// <param name="activity">Optional customer activity to associate with this scope.</param>
    /// <returns>A disposable scope that maintains the trace context.</returns>
    public static CustomerTraceScope StartScope(CustomerActivity? activity = null)
    {
        return new CustomerTraceScope(activity);
    }
    
    /// <summary>
    /// Sets the current customer activity for the scope.
    /// This is used internally by CustomerTraceScope.
    /// </summary>
    /// <param name="activity">The activity to set as current.</param>
    internal static void SetCurrentActivity(CustomerActivity? activity)
    {
        _currentActivity.Value = activity;
    }
    
    /// <summary>
    /// Sets the current metadata for the scope.
    /// This is used internally by CustomerTraceScope.
    /// </summary>
    /// <param name="metadata">The metadata to set as current.</param>
    internal static void SetCurrentMetadata(Dictionary<string, string>? metadata)
    {
        _currentMetadata.Value = metadata;
    }
    
    /// <summary>
    /// Gets the current metadata.
    /// This is used internally by CustomerTraceScope.
    /// </summary>
    /// <returns>The current metadata or null if none is set.</returns>
    internal static Dictionary<string, string>? GetCurrentMetadata()
    {
        return _currentMetadata.Value;
    }
}

/// <summary>
/// Represents a scoped customer trace context that maintains trace information
/// across the lifecycle of a request through async operations and service boundaries.
/// </summary>
public class CustomerTraceScope : IDisposable
{
    private readonly CustomerActivity? _previousActivity;
    private readonly Dictionary<string, string>? _previousMetadata;
    private readonly CustomerActivity? _scopeActivity;
    private bool _disposed;
    
    internal CustomerTraceScope(CustomerActivity? activity)
    {
        // Store previous context
        _previousActivity = CustomerTraceManager.GetCurrentActivity();
        _previousMetadata = CustomerTraceManager.GetCurrentMetadata();
        
        // Set new context
        _scopeActivity = activity;
        CustomerTraceManager.SetCurrentActivity(activity);
        
        // Initialize metadata if not provided by previous context
        if (_previousMetadata == null)
        {
            CustomerTraceManager.SetCurrentMetadata(new Dictionary<string, string>());
        }
    }
    
    /// <summary>
    /// Associates a customer activity with this scope.
    /// This allows the activity's trace information to be accessible throughout the request.
    /// </summary>
    /// <param name="activity">The customer activity to associate.</param>
    public void SetActivity(CustomerActivity activity)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CustomerTraceScope));
            
        CustomerTraceManager.SetCurrentActivity(activity);
    }

    /// <summary>
    /// Adds metadata to the current trace scope.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void AddMetadata(string key, string value)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CustomerTraceScope));
            
        CustomerTraceManager.SetMetadata(key, value);
    }
    
    /// <summary>
    /// Disposes the scope and restores the previous trace context.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
            
        // Restore previous context
        CustomerTraceManager.SetCurrentActivity(_previousActivity);
        CustomerTraceManager.SetCurrentMetadata(_previousMetadata);
        
        _disposed = true;
    }
}
