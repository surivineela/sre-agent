// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;

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
                var formatted = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(content),
                    new JsonSerializerOptions { WriteIndented = true }
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
                var formatted = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(content),
                    new JsonSerializerOptions { WriteIndented = true }
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
    /// Enable or disable debug logging.
    /// Output: No direct output; sets internal debug flag.
    /// </summary>
    public static void SetDebugMode(bool enabled)
    {
        _debugEnabled = enabled;
    }

    /// <summary>
    /// Check if debug mode is enabled.
    /// Output: Returns boolean (no direct output).
    /// </summary>
    public static bool IsDebugEnabled => _debugEnabled;

    /// <summary>
    /// Log debug message if debug mode is enabled.
    /// Output: Single line with timestamp and debug symbol (e.g., "[14:30:00.123] * DEBUG: Initializing service").
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
    /// Log debug message with category if debug mode is enabled.
    /// Output: Single line with timestamp, debug symbol, and category (e.g., "[14:30:00.123] * DEBUG [Network]: Connecting to API").
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
    /// Log HTTP request details if debug mode is enabled.
    /// Output: Multiple lines with timestamp, request method/URL, Content-Type, and formatted body (JSON if applicable).
    /// Example:
    ///   [14:30:00.123] > HTTP REQUEST: POST https://api.example.com/tools
    ///   Content-Type: application/json
    ///   Body: { "name": "mytool" }
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
    /// Log HTTP request with headers (Authorization redacted).
    /// Output: Multiple lines with timestamp, request method/URL, all headers (Authorization redacted), Content-Type, and formatted body.
    /// Example:
    ///   [14:30:00.123] > HTTP REQUEST: POST https://api.example.com/tools
    ///   Authorization: <redacted>
    ///   User-Agent: srectl/1.0
    ///   Content-Type: application/json
    ///   Body: { "name": "mytool" }
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
    /// Log HTTP response details if debug mode is enabled.
    /// Output: Multiple lines with timestamp, response status with icon, optional response time, and formatted body.
    /// Example:
    ///   [14:30:00.456] < HTTP RESPONSE (123ms): [OK] 200 OK
    ///   Body: { "id": "123", "name": "mytool" }
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
    /// Log HTTP response with headers.
    /// Output: Multiple lines with timestamp, response status with icon, all response headers, and formatted body.
    /// Example:
    ///   [14:30:00.456] < HTTP RESPONSE (123ms): [OK] 200 OK
    ///   Content-Type: application/json
    ///   Content-Length: 42
    ///   Body: { "id": "123" }
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
    /// Log authentication details if debug mode is enabled.
    /// Output: Single line with timestamp and auth symbol (e.g., "[14:30:00.123] @ AUTH: Retrieved token from cache").
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
    /// Log file operations if debug mode is enabled.
    /// Output: One or two lines with timestamp, file operation, path, and optional details.
    /// Example:
    ///   [14:30:00.123] # FILE READ: /path/to/tool.yaml
    ///   Size: 1024 bytes
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
    /// Log JSON parsing/serialization if debug mode is enabled.
    /// Output: Multiple lines with timestamp, JSON operation, optional context, and indented formatted JSON.
    /// Example:
    ///   [14:30:00.123] ~ JSON SERIALIZE (tool definition):
    ///   {
    ///     "name": "mytool",
    ///     "version": "1.0"
    ///   }
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
    /// Log validation results if debug mode is enabled.
    /// Output: One or more lines with validation status icon and target, followed by error list if invalid.
    /// Example:
    ///   ? VALIDATION: [X] tool.yaml
    ///   - Missing required field: name
    ///   - Invalid version format
    /// </summary>
    public static void LogValidation(string target, bool isValid, List<string>? errors = null)
    {
        if (!_debugEnabled) return;

        string statusIcon = isValid ? _chars.Check : _chars.Cross;
        var color = isValid ? ConsoleColor.DarkGray : ConsoleColor.DarkGray;
        WriteWithColor(color, $"? VALIDATION: {statusIcon} {target}");

        if (!isValid && errors != null && errors.Count != 0)
        {
            foreach (var error in errors)
            {
                WriteWithColor(ConsoleColor.DarkGray, $"   - {error}");
            }
        }
    }

    /// <summary>
    /// Log timing information if debug mode is enabled.
    /// Output: Single line with timestamp, timing symbol, operation, and formatted duration.
    /// Example: "[14:30:00.123] & TIMING: API request completed in 1.50s"
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
    /// Log configuration details if debug mode is enabled.
    /// Output: Single line with timestamp, config symbol, key, and value (redacted if sensitive).
    /// Example: "[14:30:00.123] = CONFIG: API_URL = https://api.example.com" or "= CONFIG: API_KEY = <***>"
    /// </summary>
    public static void LogConfig(string key, string? value, bool isSensitive = false)
    {
        if (!_debugEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var displayValue = isSensitive ? (string.IsNullOrEmpty(value) ? "<not set>" : "<***>") : value ?? "<not set>";
        WriteWithColor(ConsoleColor.DarkGray, $"[{timestamp}] {_chars.Config} CONFIG: {key} = {displayValue}");
    }

    /// <summary>
    /// Log network connectivity information if debug mode is enabled.
    /// Output: Single line with timestamp and network symbol (e.g., "[14:30:00.123] ^ NETWORK: Connected to api.example.com").
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
    /// Log a separator line for visual grouping in debug output.
    /// Output: Single line with 60-char separator, optionally with timestamp and label.
    /// Example: "[14:30:00.123] ── HTTP Request ─────────────────────────────────────"
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
    /// Log request start with unique ID for correlation.
    /// Output: Single line with timestamp, tree branch start symbol, operation, and unique ID. Returns the ID for correlation.
    /// Example: "[14:30:00.123] ┌─ START Create Tool [ID: a1b2c3d4]"
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
    /// Log request end with correlation ID.
    /// Output: Single line with timestamp, tree branch end symbol, operation, status icon, and correlation ID.
    /// Example: "[14:30:00.456] └─ END Create Tool: [OK] SUCCESS [ID: a1b2c3d4]"
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
