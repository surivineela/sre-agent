// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// Result of a CLI command execution
/// </summary>
public class CliResult
{
    /// <summary>
    /// Exit code from the CLI process (0 = success)
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the CLI
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Standard error from the CLI
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// True if the command succeeded (exit code 0)
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Combined output and error
    /// </summary>
    public string CombinedOutput => string.IsNullOrEmpty(Error) ? Output : $"{Output}\n{Error}";
}
