// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// Configuration settings for CLI E2E tests
/// </summary>
public class CliTestSettings
{
    /// <summary>
    /// Server URL to test against (e.g., https://localhost:7023)
    /// </summary>
    public string ServerUrl { get; set; } = "https://localhost:7023";

    /// <summary>
    /// Timeout in seconds for CLI commands
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// If true, test data will be cleaned up after tests
    /// </summary>
    public bool Cleanup { get; set; } = true;

    /// <summary>
    /// Optional: Override CLI executable path. If null, will auto-detect.
    /// </summary>
    public string? CliPath { get; set; }

    /// <summary>
    /// If true, adds --debug flag to all CLI commands for verbose output
    /// </summary>
    public bool Debug { get; set; } = false;

    /// <summary>
    /// Load settings from CliTestSettings.json and environment variables
    /// </summary>
    public static CliTestSettings Load()
    {
        var settings = new CliTestSettings();

        // Load from JSON file if it exists
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "CliTestSettings.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var loadedSettings = JsonSerializer.Deserialize<CliTestSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loadedSettings != null)
                {
                    settings = loadedSettings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load CliTestSettings.json: {ex.Message}");
            }
        }

        // Override from environment variables if present
        var envUrl = Environment.GetEnvironmentVariable("AGENT_SERVER_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            settings.ServerUrl = envUrl;
        }

        var envDebug = Environment.GetEnvironmentVariable("AGENT_CLI_DEBUG");
        if (!string.IsNullOrEmpty(envDebug) && bool.TryParse(envDebug, out var debugFlag))
        {
            settings.Debug = debugFlag;
        }

        return settings;
    }
}
