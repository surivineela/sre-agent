// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// Base class for CLI E2E tests that provides common functionality
/// </summary>
public abstract class CliTestBase : IAsyncLifetime
{
    protected CliTestSettings Settings { get; }
    protected ITestOutputHelper Output { get; }
    protected HttpClient ApiClient { get; }

    private readonly string _cliPath;
    private string? _testWorkingDirectory;

    protected CliTestBase(ITestOutputHelper output)
    {
        // Set UTF-8 encoding for better Unicode character rendering
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Output = output;
        Settings = CliTestSettings.Load();
        ApiClient = new HttpClient
        {
            BaseAddress = new Uri(Settings.ServerUrl),
            Timeout = TimeSpan.FromSeconds(Settings.TimeoutSeconds)
        };

        // Find CLI executable
        _cliPath = FindCliExecutable();
        Output.WriteLine($"Using CLI: {_cliPath}");
        Output.WriteLine($"Server URL: {Settings.ServerUrl}");
        Output.WriteLine($"Debug: {Settings.Debug}");
        Output.WriteLine($"Cleanup: {Settings.Cleanup}");
    }

    /// <summary>
    /// Initialize test - create working directory and initialize CLI
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create unique working directory for this test
        _testWorkingDirectory = Path.Combine(Path.GetTempPath(), $"cli-e2e-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkingDirectory);
        Output.WriteLine($"Test working directory: {_testWorkingDirectory}");

        // Initialize CLI with server URL - this will fail if server is not available
        var initResult = await RunCliCommand("init", "--resource-url", Settings.ServerUrl);
        if (!initResult.Success)
        {
            throw new Exception($"Failed to initialize CLI with server {Settings.ServerUrl}. Make sure the server is running.\n{initResult.Error}");
        }

        Output.WriteLine("CLI initialized successfully");
    }

    /// <summary>
    /// Cleanup test resources
    /// </summary>
    public Task DisposeAsync()
    {
        ApiClient?.Dispose();

        // Clean up test working directory
        if (!string.IsNullOrEmpty(_testWorkingDirectory) && Directory.Exists(_testWorkingDirectory))
        {
            try
            {
                Directory.Delete(_testWorkingDirectory, recursive: true);
                Output.WriteLine($"Cleaned up test directory: {_testWorkingDirectory}");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Run a CLI command and capture output
    /// </summary>
    /// <param name="args">Command arguments (e.g., "agent", "create", "--name", "TestAgent")</param>
    /// <returns>CLI result with exit code and output</returns>
    protected async Task<CliResult> RunCliCommand(params string[] args)
    {
        // Add --debug flag if enabled in settings
        var commandArgs = args.ToList();
        if (Settings.Debug)
        {
            commandArgs.Add("--debug");
        }

        Output.WriteLine($"\n$ srectl {string.Join(" ", commandArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");

        var startInfo = new ProcessStartInfo
        {
            FileName = _cliPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _testWorkingDirectory ?? Directory.GetCurrentDirectory()
        };

        foreach (var arg in commandArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new Exception("Failed to start CLI process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(Settings.TimeoutSeconds);
        var completedInTime = await process.WaitForExitAsync(timeout);

        if (!completedInTime)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }

            throw new TimeoutException($"CLI command timed out after {timeout.TotalSeconds}s: srectl {string.Join(" ", args)}");
        }

        var output = await outputTask;
        var error = await errorTask;

        var result = new CliResult
        {
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };

        Output.WriteLine($"Exit Code: {result.ExitCode}");
        if (!string.IsNullOrEmpty(output))
        {
            Output.WriteLine($"Output:\n{output}");
        }
        if (!string.IsNullOrEmpty(error))
        {
            Output.WriteLine($"Error:\n{error}");
        }

        return result;
    }

    /// <summary>
    /// Find the CLI executable (srectl.exe)
    /// </summary>
    private string FindCliExecutable()
    {
        // Option 1: User specified in settings
        if (!string.IsNullOrEmpty(Settings.CliPath) && File.Exists(Settings.CliPath))
        {
            Output.WriteLine($"Using CLI from settings: {Settings.CliPath}");
            return Settings.CliPath;
        }

        // Option 2: Check if srectl is in PATH (installed globally)
        try
        {
            var whereResult = Process.Start(new ProcessStartInfo
            {
                FileName = "where",  // Windows
                Arguments = "srectl",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (whereResult != null)
            {
                var paths = whereResult.StandardOutput.ReadToEnd().Trim();
                whereResult.WaitForExit();

                if (whereResult.ExitCode == 0 && !string.IsNullOrEmpty(paths))
                {
                    var firstPath = paths.Split('\n')[0].Trim();
                    if (File.Exists(firstPath))
                    {
                        Output.WriteLine($"Found CLI in PATH: {firstPath}");
                        return firstPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Could not search PATH: {ex.Message}");
        }

        // Option 3: Look for built executable in Agent.Cli/bin
        var testProjectDir = AppContext.BaseDirectory;
        var agentSolutionDir = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
        var cliProjectDir = Path.Combine(agentSolutionDir, "Agent.Cli");

        var possiblePaths = new[]
        {
            Path.Combine(cliProjectDir, "bin", "Debug", "net9.0", "srectl.exe"),
            Path.Combine(cliProjectDir, "bin", "Release", "net9.0", "srectl.exe"),
            Path.Combine(cliProjectDir, "bin", "Debug", "net9.0", "Agent.Cli.exe"),
            Path.Combine(cliProjectDir, "bin", "Release", "net9.0", "Agent.Cli.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Output.WriteLine($"Found CLI at: {path}");
                return path;
            }
        }

        throw new FileNotFoundException(
            "Could not find srectl executable. Please:\n" +
            "1. Build and install the CLI: cd Agent.Cli/scripts && .\\build_and_install_exe.ps1\n" +
            "2. Or build the CLI: cd Agent.Cli && dotnet build\n" +
            "3. Or specify CliPath in CliTestSettings.json");
    }

    /// <summary>
    /// Helper: Create a test agent on the server
    /// </summary>
    protected async Task<string> CreateTestAgentAsync(string? name = null, string? instructions = null)
    {
        name ??= $"TestAgent_{Guid.NewGuid():N}"[..20]; // Take first 20 chars of TestAgent_guid
        instructions ??= "This is a test agent created for automated end-to-end testing purposes to validate CLI commands";

        var result = await RunCliCommand(
            "agent", "create",
            "--name", name,
            "--instructions", instructions
        );

        if (!result.Success)
        {
            throw new Exception($"Failed to create test agent '{name}': {result.Error}");
        }

        Output.WriteLine($"[SUCCESS] Created test agent: {name}");
        return name;
    }

    /// <summary>
    /// Helper: Delete a test agent from the server
    /// </summary>
    protected async Task DeleteTestAgentAsync(string name)
    {
        if (!Settings.Cleanup)
        {
            Output.WriteLine($"Skipping cleanup for agent: {name} (Cleanup=false)");
            return;
        }

        try
        {
            var result = await RunCliCommand("agent", "delete", "--name", name);
            if (result.Success)
            {
                Output.WriteLine($"[CLEANUP] Deleted test agent: {name}");
            }
            else
            {
                Output.WriteLine($"Warning: Failed to cleanup agent '{name}': {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Warning: Exception during cleanup of agent '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Helper: Generate a unique test name with timestamp
    /// </summary>
    protected string GenerateTestName(string prefix = "Test")
    {
        var guid = Guid.NewGuid().ToString("N")[..6];
        return $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{guid}";
    }
}

/// <summary>
/// Extension methods to support WaitForExitAsync with timeout
/// </summary>
internal static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout)
    {
        try
        {
            return await process.WaitForExitAsync(new CancellationTokenSource(timeout).Token)
                .ContinueWith(t => !t.IsCanceled && !t.IsFaulted);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
