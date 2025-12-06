// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// In-process CLI test runner that captures console output without spawning processes.
/// This provides fast, debuggable tests by calling Program.Main directly.
/// </summary>
public class CliTestRunner : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly string _originalDirectory;
    private readonly string _testWorkingDirectory;

    public CliTestRunner()
    {
        // Save original console streams
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _originalDirectory = Directory.GetCurrentDirectory();

        // Create isolated working directory under test assembly output directory
        var assemblyLocation = Path.GetDirectoryName(typeof(CliTestRunner).Assembly.Location)!;
        var testOutputRoot = Path.Combine(assemblyLocation, "TestOutput");
        Directory.CreateDirectory(testOutputRoot);

        _testWorkingDirectory = Path.Combine(testOutputRoot, $"cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkingDirectory);
        Directory.SetCurrentDirectory(_testWorkingDirectory);

        // Set UTF-8 encoding for better Unicode support
        Console.OutputEncoding = Encoding.UTF8;
    }

    /// <summary>
    /// Gets the test working directory path
    /// </summary>
    public string WorkingDirectory => _testWorkingDirectory;

    /// <summary>
    /// Runs a CLI command in-process and captures output
    /// </summary>
    /// <param name="args">Command arguments (e.g., "tool", "create", "--name", "test")</param>
    /// <returns>Result containing exit code and captured output</returns>
    public async Task<InProcessCliResult> RunAsync(params string[] args)
    {
        // Clear previous output
        _output.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();

        // Redirect console output
        Console.SetOut(_output);
        Console.SetError(_error);

        int exitCode;
        try
        {
            // Call Program.Main directly (in-process execution)
            exitCode = await Program.Main(args);
        }
        catch (Exception ex)
        {
            // Capture unhandled exceptions
            await _error.WriteLineAsync($"Unhandled exception: {ex}");
            exitCode = 1;
        }
        finally
        {
            // Restore original console streams
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);
        }

        return new InProcessCliResult
        {
            ExitCode = exitCode,
            StandardOutput = _output.ToString(),
            StandardError = _error.ToString(),
            Success = exitCode == 0
        };
    }

    /// <summary>
    /// Creates a file in the test working directory
    /// </summary>
    public void CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testWorkingDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    /// <summary>
    /// Checks if a file exists in the test working directory
    /// </summary>
    public bool FileExists(string relativePath)
    {
        var fullPath = Path.Combine(_testWorkingDirectory, relativePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Reads file content from the test working directory
    /// </summary>
    public string ReadFile(string relativePath)
    {
        var fullPath = Path.Combine(_testWorkingDirectory, relativePath);
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Gets the full path for a relative path in the test working directory
    /// </summary>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_testWorkingDirectory, relativePath);
    }

    /// <summary>
    /// Cleans up test resources. Set KEEP_TEST_OUTPUT=1 environment variable to preserve test directories.
    /// </summary>
    public void Dispose()
    {
        // Restore original directory
        try
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
        catch
        {
            // Ignore errors restoring directory
        }

        // Clean up test directory (unless KEEP_TEST_OUTPUT is set)
        var keepOutput = Environment.GetEnvironmentVariable("KEEP_TEST_OUTPUT");
        if (string.IsNullOrEmpty(keepOutput) || keepOutput == "0")
        {
            try
            {
                if (Directory.Exists(_testWorkingDirectory))
                {
                    Directory.Delete(_testWorkingDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _output.Dispose();
        _error.Dispose();
    }
}

/// <summary>
/// Result from running a CLI command using the new in-process test runner
/// </summary>
public class InProcessCliResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool Success { get; init; }

    /// <summary>
    /// Gets combined output (stdout + stderr)
    /// </summary>
    public string Output => StandardOutput + StandardError;
}
