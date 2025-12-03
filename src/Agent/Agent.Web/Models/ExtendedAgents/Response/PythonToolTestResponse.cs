// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Response;

/// <summary>
/// Response model for Python tool test results
/// </summary>
public class PythonToolTestResponse
{
    /// <summary>
    /// Whether the Python function executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result returned by the main() function
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Exit code from Python execution (0 = success)
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Standard output from Python execution
    /// </summary>
    public string? Stdout { get; set; }

    /// <summary>
    /// Standard error from Python execution
    /// </summary>
    public string? Stderr { get; set; }

    /// <summary>
    /// Files generated during execution (saved to /mnt/data/)
    /// </summary>
    public List<string>? Files { get; set; }

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error type if an exception occurred
    /// </summary>
    public string? ErrorType { get; set; }
}
