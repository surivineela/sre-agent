// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Agent.Plugins.Models.WorkspaceTools;

/// <summary>
/// Configuration constants for terminal session management.
/// </summary>
public static class TerminalConfiguration
{
    /// <summary>
    /// Maximum time to wait for a foreground command to complete.
    /// </summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Maximum time to wait for shell integration to initialize.
    /// </summary>
    public static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Time to wait for background command PID echo.
    /// </summary>
    public static readonly TimeSpan BackgroundPidWait = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum initialization attempts (initial + retries).
    /// </summary>
    public const int MaxInitRetries = 2;

    /// <summary>
    /// Number of characters to keep from the start of output.
    /// </summary>
    public const int HeadChars = 500;

    /// <summary>
    /// Number of characters to keep from the end of output.
    /// </summary>
    public const int TailChars = 1000;

    /// <summary>
    /// Directory for background command output files.
    /// </summary>
    public static string BackgroundOutputDir => GetTmpPath();

    private static string GetTmpPath()
    {
        string sandboxRoot;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sandboxRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SreAgent", "TerminalRoot");
        }
        else
        {
            sandboxRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "sreagent", "terminalRoot");
        }

        return Path.Combine(sandboxRoot, "tmp");
    }
}

/// <summary>
/// Exception thrown when terminal session initialization fails.
/// </summary>
public class TerminalInitializationException : Exception
{
    public TerminalInitializationException(string message) : base(message) { }
    public TerminalInitializationException(string message, Exception inner) : base(message, inner) { }
}
