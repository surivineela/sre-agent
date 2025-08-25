using System.Text.Json;

namespace Agent.Cli.Helpers;

/// <summary>
/// Provides centralized debug logging for CLI commands
/// </summary>
public static class DebugLogger
{
    private static bool _debugEnabled = false;

    /// <summary>
    /// Enable or disable debug logging
    /// </summary>
    public static void SetDebugMode(bool enabled)
    {
        _debugEnabled = enabled;
    }

    /// <summary>
    /// Check if debug mode is enabled
    /// </summary>
    public static bool IsDebugEnabled => _debugEnabled;

    /// <summary>
    /// Log debug message if debug mode is enabled
    /// </summary>
    public static void Debug(string message)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"🔍 DEBUG: {message}");
        }
    }

    /// <summary>
    /// Log debug message with category if debug mode is enabled
    /// </summary>
    public static void Debug(string category, string message)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"🔍 DEBUG [{category}]: {message}");
        }
    }

    /// <summary>
    /// Log HTTP request details if debug mode is enabled
    /// </summary>
    public static void LogHttpRequest(string method, string url, string? contentType = null, string? content = null)
    {
        if (!_debugEnabled) return;

        Console.WriteLine($"🌐 HTTP REQUEST: {method} {url}");
        if (!string.IsNullOrEmpty(contentType))
        {
            Console.WriteLine($"   Content-Type: {contentType}");
        }
        if (!string.IsNullOrEmpty(content))
        {
            var truncatedContent = content.Length > 500 ? content.Substring(0, 500) + "... [truncated]" : content;
            Console.WriteLine($"   Body: {truncatedContent}");
        }
    }

    /// <summary>
    /// Log HTTP response details if debug mode is enabled
    /// </summary>
    public static void LogHttpResponse(int statusCode, string statusName, string? content = null, long? responseTime = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = statusCode >= 200 && statusCode < 300 ? "✅" : "❌";
        string responseTimeText = responseTime.HasValue ? $" ({responseTime}ms)" : "";

        Console.WriteLine($"🌐 HTTP RESPONSE{responseTimeText}: {statusIcon} {statusCode} {statusName}");
        if (!string.IsNullOrEmpty(content))
        {
            var truncatedContent = content.Length > 500 ? content.Substring(0, 500) + "... [truncated]" : content;
            Console.WriteLine($"   Body: {truncatedContent}");
        }
    }

    /// <summary>
    /// Log authentication details if debug mode is enabled
    /// </summary>
    public static void LogAuth(string message)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"🔐 AUTH: {message}");
        }
    }

    /// <summary>
    /// Log file operations if debug mode is enabled
    /// </summary>
    public static void LogFile(string operation, string filePath, string? details = null)
    {
        if (!_debugEnabled) return;

        Console.WriteLine($"📁 FILE {operation.ToUpper()}: {filePath}");
        if (!string.IsNullOrEmpty(details))
        {
            Console.WriteLine($"   {details}");
        }
    }

    /// <summary>
    /// Log JSON parsing/serialization if debug mode is enabled
    /// </summary>
    public static void LogJson(string operation, object? data, string? context = null)
    {
        if (!_debugEnabled) return;

        try
        {
            var json = data != null ? JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }) : "null";
            var truncatedJson = json.Length > 1000 ? json.Substring(0, 1000) + "... [truncated]" : json;

            var contextText = !string.IsNullOrEmpty(context) ? $" ({context})" : "";
            Console.WriteLine($"📄 JSON {operation.ToUpper()}{contextText}:");
            Console.WriteLine($"   {truncatedJson.Replace("\n", "\n   ")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📄 JSON {operation.ToUpper()} ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// Log validation results if debug mode is enabled
    /// </summary>
    public static void LogValidation(string target, bool isValid, List<string>? errors = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = isValid ? "✅" : "❌";
        Console.WriteLine($"🔍 VALIDATION: {statusIcon} {target}");

        if (!isValid && errors != null && errors.Any())
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"   • {error}");
            }
        }
    }

    /// <summary>
    /// Log timing information if debug mode is enabled
    /// </summary>
    public static void LogTiming(string operation, TimeSpan duration)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"⏱️ TIMING: {operation} completed in {duration.TotalMilliseconds:F2}ms");
        }
    }

    /// <summary>
    /// Log configuration details if debug mode is enabled
    /// </summary>
    public static void LogConfig(string key, string? value, bool isSensitive = false)
    {
        if (!_debugEnabled) return;

        var displayValue = isSensitive ? (string.IsNullOrEmpty(value) ? "<not set>" : "<***>") : value ?? "<not set>";
        Console.WriteLine($"⚙️ CONFIG: {key} = {displayValue}");
    }

    /// <summary>
    /// Log network connectivity information if debug mode is enabled
    /// </summary>
    public static void LogNetwork(string message)
    {
        if (_debugEnabled)
        {
            Console.WriteLine($"🌍 NETWORK: {message}");
        }
    }
}
