using System.Text.Json;
using System.Text;

namespace Agent.Cli.Helpers;

/// <summary>
/// Provides centralized debug logging for CLI commands with portable console output
/// </summary>
public static class DebugLogger
{
    private static bool _debugEnabled = false;
    private static readonly Palette _chars;
    private static readonly bool _supportsColor;

    static DebugLogger()
    {
        // Try for UTF-8 output, but don't require it
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { /* ignore */ }

        bool unicodeOk = CanRoundTrip("[OK][X]*>");
        _chars = unicodeOk ? Palette.Unicode : Palette.Ascii;

        // Respect NO_COLOR and redirection
        _supportsColor = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;
    }

    private static bool CanRoundTrip(string s)
    {
        var enc = Console.OutputEncoding;
        var b = enc.GetBytes(s);
        var s2 = enc.GetString(b);
        return s2 == s;
    }

    public record Palette(
        string Check, string Cross, string Bullet, string ArrowRight,
        string Debug, string Network, string Auth, string File, string Json, string Timing, string Config
    )
    {
        public static readonly Palette Unicode = new(
            "[OK]", "[X]", "*", ">",
            "*", "^", "@", "#", "~", "&", "="
        );

        public static readonly Palette Ascii = new(
            "[OK]", "[X]", "*", ">",
            "*", "^", "@", "#", "~", "&", "="
        );
    }

    private static void WriteWithColor(ConsoleColor color, string message)
    {
        if (_supportsColor)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = old;
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private static void WriteMultiLine(ConsoleColor color, string prefix, string content, int indentSize = 3)
    {
        var lines = content.Split('\n');
        var indent = new string(' ', indentSize);
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var linePrefix = i == 0 ? prefix : indent;
            
            if (_supportsColor)
            {
                var old = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"{linePrefix}{line}");
                Console.ForegroundColor = old;
            }
            else
            {
                Console.WriteLine($"{linePrefix}{line}");
            }
        }
    }

    private static string FormatRequestBody(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";
            
        // Try to format JSON
        if (IsJson(content))
        {
            try
            {
                var formatted = System.Text.Json.JsonSerializer.Serialize(
                    System.Text.Json.JsonSerializer.Deserialize<object>(content),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
                return formatted;
            }
            catch
            {
                return content;
            }
        }
        
        return content;
    }

    private static string FormatResponseBody(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";
            
        // Try to format JSON
        if (IsJson(content))
        {
            try
            {
                var formatted = System.Text.Json.JsonSerializer.Serialize(
                    System.Text.Json.JsonSerializer.Deserialize<object>(content),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
                return formatted;
            }
            catch
            {
                return content;
            }
        }
        
        return content;
    }

    private static bool IsJson(string content)
    {
        var trimmed = content.Trim();
        return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) || 
               (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
    }

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
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Debug} DEBUG: {message}");
        }
    }

    /// <summary>
    /// Log debug message with category if debug mode is enabled
    /// </summary>
    public static void Debug(string category, string message)
    {
        if (_debugEnabled)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Debug} DEBUG [{category}]: {message}");
        }
    }

    /// <summary>
    /// Log HTTP request details if debug mode is enabled
    /// </summary>
    public static void LogHttpRequest(string method, string url, string? contentType = null, string? content = null)
    {
        if (!_debugEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.ArrowRight} HTTP REQUEST: {method} {url}");
        
        if (!string.IsNullOrEmpty(contentType))
        {
            WriteWithColor(ConsoleColor.DarkGray, $"   Content-Type: {contentType}");
        }
        
        if (!string.IsNullOrEmpty(content))
        {
            var displayContent = FormatRequestBody(content);
            if (displayContent.Contains('\n'))
            {
                WriteMultiLine(ConsoleColor.DarkGray, "   Body: ", displayContent);
            }
            else
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   Body: {displayContent}");
            }
        }
    }

    /// <summary>
    /// Log HTTP request with headers (Authorization redacted)
    /// </summary>
    public static void LogHttpRequest(string method, string url, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? contentType, string? content)
    {
        if (!_debugEnabled) return;
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.ArrowRight} HTTP REQUEST: {method} {url}");
        
        foreach (var header in headers)
        {
            var name = header.Key;
            var value = string.Join(", ", header.Value);
            if (string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                value = "<redacted>";
            }
            WriteWithColor(ConsoleColor.DarkGray, $"   {name}: {value}");
        }
        
        if (!string.IsNullOrEmpty(contentType))
        {
            WriteWithColor(ConsoleColor.DarkGray, $"   Content-Type: {contentType}");
        }
        
        if (!string.IsNullOrEmpty(content))
        {
            var displayContent = FormatRequestBody(content);
            if (displayContent.Contains('\n'))
            {
                WriteMultiLine(ConsoleColor.DarkGray, "   Body: ", displayContent);
            }
            else
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   Body: {displayContent}");
            }
        }
    }

    /// <summary>
    /// Log HTTP response details if debug mode is enabled
    /// </summary>
    public static void LogHttpResponse(int statusCode, string statusName, string? content = null, long? responseTime = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = statusCode >= 200 && statusCode < 300 ? _chars.Check : _chars.Cross;
        string responseTimeText = responseTime.HasValue ? $" ({responseTime}ms)" : "";
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        var color = statusCode >= 200 && statusCode < 300 ? ConsoleColor.DarkGray : ConsoleColor.DarkGray;
        WriteWithColor(color, $"[{timestamp}] < HTTP RESPONSE{responseTimeText}: {statusIcon} {statusCode} {statusName}");
        
        if (!string.IsNullOrEmpty(content))
        {
            var displayContent = FormatResponseBody(content);
            if (displayContent.Contains('\n'))
            {
                WriteMultiLine(ConsoleColor.DarkGray, "   Body: ", displayContent);
            }
            else
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   Body: {displayContent}");
            }
        }
    }

    /// <summary>
    /// Log HTTP response with headers
    /// </summary>
    public static void LogHttpResponse(int statusCode, string statusName, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? content = null, long? responseTime = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = statusCode >= 200 && statusCode < 300 ? _chars.Check : _chars.Cross;
        string responseTimeText = responseTime.HasValue ? $" ({responseTime}ms)" : "";
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        var color = statusCode >= 200 && statusCode < 300 ? ConsoleColor.DarkGray : ConsoleColor.DarkGray;
        WriteWithColor(color, $"[{timestamp}] < HTTP RESPONSE{responseTimeText}: {statusIcon} {statusCode} {statusName}");
        
        foreach (var header in headers)
        {
            WriteWithColor(ConsoleColor.DarkGray, $"   {header.Key}: {string.Join(", ", header.Value)}");
        }
        
        if (!string.IsNullOrEmpty(content))
        {
            var displayContent = FormatResponseBody(content);
            if (displayContent.Contains('\n'))
            {
                WriteMultiLine(ConsoleColor.DarkGray, "   Body: ", displayContent);
            }
            else
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   Body: {displayContent}");
            }
        }
    }

    /// <summary>
    /// Log authentication details if debug mode is enabled
    /// </summary>
    public static void LogAuth(string message)
    {
        if (_debugEnabled)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Auth} AUTH: {message}");
        }
    }

    /// <summary>
    /// Log file operations if debug mode is enabled
    /// </summary>
    public static void LogFile(string operation, string filePath, string? details = null)
    {
        if (!_debugEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.File} FILE {operation.ToUpper()}: {filePath}");
        if (!string.IsNullOrEmpty(details))
        {
            WriteWithColor(ConsoleColor.DarkGray, $"   {details}");
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
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var json = data != null ? JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }) : "null";

            var contextText = !string.IsNullOrEmpty(context) ? $" ({context})" : "";
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Json} JSON {operation.ToUpper()}{contextText}:");
            WriteMultiLine(ConsoleColor.DarkGray, "   ", json);
        }
        catch (Exception ex)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Json} JSON {operation.ToUpper()} ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// Log validation results if debug mode is enabled
    /// </summary>
    public static void LogValidation(string target, bool isValid, List<string>? errors = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = isValid ? _chars.Check : _chars.Cross;
        var color = isValid ? ConsoleColor.DarkGray : ConsoleColor.DarkGray;
        WriteWithColor(color, $"? VALIDATION: {statusIcon} {target}");

        if (!isValid && errors != null && errors.Any())
        {
            foreach (var error in errors)
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   - {error}");
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
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var timeDisplay = duration.TotalSeconds < 1
                ? $"{duration.TotalMilliseconds:F2}ms"
                : duration.TotalMinutes >= 1
                    ? $"{duration.Minutes}m {duration.Seconds}s {duration.Milliseconds}ms"
                    : $"{duration.TotalSeconds:F2}s";
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Timing} TIMING: {operation} completed in {timeDisplay}");
        }
    }

    /// <summary>
    /// Log configuration details if debug mode is enabled
    /// </summary>
    public static void LogConfig(string key, string? value, bool isSensitive = false)
    {
        if (!_debugEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var displayValue = isSensitive ? (string.IsNullOrEmpty(value) ? "<not set>" : "<***>") : value ?? "<not set>";
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Config} CONFIG: {key} = {displayValue}");
    }

    /// <summary>
    /// Log network connectivity information if debug mode is enabled
    /// </summary>
    public static void LogNetwork(string message)
    {
        if (_debugEnabled)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Network} NETWORK: {message}");
        }
    }

    /// <summary>
    /// Log a separator line for visual grouping in debug output
    /// </summary>
    public static void LogSeparator(string? label = null)
    {
        if (_debugEnabled)
        {
            var line = new string('─', 60);
            if (!string.IsNullOrEmpty(label))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] ── {label} {new string('─', Math.Max(0, 45 - label.Length))}");
            }
            else
            {
                WriteWithColor(ConsoleColor.DarkGray, line);
            }
        }
    }

    /// <summary>
    /// Log request start with unique ID for correlation
    /// </summary>
    public static string LogRequestStart(string operation, string? details = null)
    {
        if (!_debugEnabled) return "";

        var requestId = Guid.NewGuid().ToString("N")[..8];
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var detailsText = !string.IsNullOrEmpty(details) ? $" ({details})" : "";
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] ┌─ START {operation}{detailsText} [ID: {requestId}]");
        return requestId;
    }

    /// <summary>
    /// Log request end with correlation ID
    /// </summary>
    public static void LogRequestEnd(string requestId, string operation, bool success = true, string? details = null)
    {
        if (!_debugEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var status = success ? "SUCCESS" : "FAILED";
        var statusIcon = success ? _chars.Check : _chars.Cross;
        var detailsText = !string.IsNullOrEmpty(details) ? $" ({details})" : "";
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] └─ END {operation}: {statusIcon} {status}{detailsText} [ID: {requestId}]");
    }
}
