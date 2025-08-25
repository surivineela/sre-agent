using System.CommandLine;
using System.CommandLine.Parsing;

namespace Agent.Cli.Services;

/// <summary>
/// Provides logging functionality with support for debug and quiet modes.
/// </summary>
public static class LoggingService
{
    private static bool _debugMode = false;
    private static bool _quietMode = false;

    /// <summary>
    /// Initialize logging based on command line options.
    /// </summary>
    public static void Initialize(ParseResult parseResult)
    {
        // Simple implementation for now - check command line arguments
        var args = Environment.GetCommandLineArgs();
        _debugMode = args.Contains("--debug");
        _quietMode = args.Contains("--quiet");
    }

    /// <summary>
    /// Write debug message (only shown in debug mode).
    /// </summary>
    public static void Debug(string message)
    {
        if (_debugMode && !_quietMode)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }
    }

    /// <summary>
    /// Write info message (shown unless in quiet mode).
    /// </summary>
    public static void Info(string message)
    {
        if (!_quietMode)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Write warning message (always shown).
    /// </summary>
    public static void Warning(string message)
    {
        Console.WriteLine($"⚠️  {message}");
    }

    /// <summary>
    /// Write error message (always shown).
    /// </summary>
    public static void Error(string message)
    {
        Console.WriteLine($"❌ {message}");
    }

    /// <summary>
    /// Write success message (shown unless in quiet mode).
    /// </summary>
    public static void Success(string message)
    {
        if (!_quietMode)
        {
            Console.WriteLine($"✅ {message}");
        }
    }

    /// <summary>
    /// Write verbose debug information.
    /// </summary>
    public static void Verbose(string message)
    {
        if (_debugMode && !_quietMode)
        {
            Console.WriteLine($"[VERBOSE] {message}");
        }
    }

    /// <summary>
    /// Gets if debug mode is enabled.
    /// </summary>
    public static bool IsDebugEnabled => _debugMode;

    /// <summary>
    /// Gets if quiet mode is enabled.
    /// </summary>
    public static bool IsQuietEnabled => _quietMode;
}