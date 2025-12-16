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
    private readonly string? _mockServerUrl;
    private readonly string _testConfigDir;
    private readonly bool _useRealServer;

    /// <summary>
    /// Checks if tests should run against a real server instead of the in-memory mock server.
    /// Set USE_REAL_SERVER environment variable to enable real server mode.
    /// </summary>
    public static bool UseRealServer => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_REAL_SERVER"));

    /// <summary>
    /// Gets the real server URL from environment variable.
    /// Use SREAGENT_SERVER_URL to specify the server URL (defaults to http://localhost:5000 if not set).
    /// </summary>
    public static string RealServerUrl => Environment.GetEnvironmentVariable("SREAGENT_SERVER_URL") ?? "http://localhost:5000";

    public CliTestRunner(string? mockServerUrl = null)
    {
        _useRealServer = UseRealServer;
        _mockServerUrl = _useRealServer ? null : mockServerUrl;

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

        // Create test-specific config directory
        _testConfigDir = Path.Combine(_testWorkingDirectory, ".sreagent");
        Directory.CreateDirectory(_testConfigDir);

        // Override config directory via environment variable for test isolation
        Environment.SetEnvironmentVariable("SRECTL_CONFIG_DIR", _testConfigDir);

        // Set UTF-8 encoding for better Unicode support
        Console.OutputEncoding = Encoding.UTF8;

        // Configure server URL based on test mode
        if (_useRealServer)
        {
            ConfigureRealServer(RealServerUrl);
        }
        else if (!string.IsNullOrEmpty(_mockServerUrl))
        {
            ConfigureMockServer(_mockServerUrl);
        }
    }

    /// <summary>
    /// Configure the CLI to use the mock server URL
    /// </summary>
    private void ConfigureMockServer(string serverUrl)
    {
        // Create CLI configuration file pointing to mock server
        // Use snake_case property names to match CliConfiguration JSON annotations
        var configContent = $@"{{
  ""resource_url"": ""{serverUrl}"",
  ""auth_required"": false
}}";
        var configFile = Path.Combine(_testConfigDir, "config.json");
        File.WriteAllText(configFile, configContent);
    }

    /// <summary>
    /// Configure the CLI to use a real server URL
    /// </summary>
    private void ConfigureRealServer(string serverUrl)
    {
        // Create CLI configuration file pointing to real server
        // Use snake_case property names to match CliConfiguration JSON annotations
        var configContent = $@"{{
  ""resource_url"": ""{serverUrl}"",
  ""auth_required"": true
}}";
        var configFile = Path.Combine(_testConfigDir, "config.json");
        File.WriteAllText(configFile, configContent);
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
    /// Creates a directory in the test working directory
    /// </summary>
    public void CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_testWorkingDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
    }

    /// <summary>
    /// Checks if a directory exists in the test working directory
    /// </summary>
    public bool DirectoryExists(string relativePath)
    {
        var fullPath = Path.Combine(_testWorkingDirectory, relativePath);
        return Directory.Exists(fullPath);
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
        // Clear environment variable override
        Environment.SetEnvironmentVariable("SRECTL_CONFIG_DIR", null);

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
