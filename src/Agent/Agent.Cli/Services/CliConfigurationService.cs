// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Cli.Models;

namespace Agent.Cli.Services;

public class CliConfigurationService : ICliConfigurationService
{
    private static readonly string UserConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sreagent");
    private static readonly string ConfigFileName = Path.Combine(UserConfigDir, "config.json");
    private static readonly string ProfilesDir = Path.Combine(UserConfigDir, "profiles");
    private static readonly string CurrentProfileFile = Path.Combine(UserConfigDir, "current-profile");

    static CliConfigurationService()
    {
        // Ensure the .sreagent directory exists
        Directory.CreateDirectory(UserConfigDir);
        Directory.CreateDirectory(ProfilesDir);
    }

    public async Task<CliConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(ConfigFileName))
                return null;

            var json = await File.ReadAllTextAsync(ConfigFileName);
            var config = JsonSerializer.Deserialize<CliConfiguration>(json);
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file '{ConfigFileName}' is corrupted: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
        }
    }

    public async Task SaveConfigurationAsync(CliConfiguration config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(ConfigFileName, json);
    }

    public async Task<bool> HasValidConfigurationAsync()
    {
        try
        {
            var config = await LoadConfigurationAsync();
            return config != null && !string.IsNullOrWhiteSpace(config.ResourceUrl);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsLocalhost(string url)
    {
        return url.Contains("localhost") || url.Contains("127.0.0.1");
    }

    public IEnumerable<string> GetAvailableProfiles()
    {
        if (!Directory.Exists(ProfilesDir))
            return [];

        return Directory.GetFiles(ProfilesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    public async Task<CliConfiguration?> LoadProfileAsync(string profileName)
    {
        try
        {
            var profilePath = Path.Combine(ProfilesDir, $"{profileName}.json");
            if (!File.Exists(profilePath))
                return null;

            var json = await File.ReadAllTextAsync(profilePath);
            return JsonSerializer.Deserialize<CliConfiguration>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveProfileAsync(string profileName, CliConfiguration config)
    {
        // Profiles directory is already created in the static constructor
        var profilePath = Path.Combine(ProfilesDir, $"{profileName}.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(profilePath, json);
    }

    public string? GetCurrentProfile()
    {
        if (!File.Exists(CurrentProfileFile))
            return null;

        try
        {
            return File.ReadAllText(CurrentProfileFile).Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCurrentProfileAsync(string profileName)
    {
        await File.WriteAllTextAsync(CurrentProfileFile, profileName);
    }

    public void DeleteProfile(string profileName)
    {
        var profilePath = Path.Combine(ProfilesDir, $"{profileName}.json");
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        // If this was the current profile, clear the current profile setting
        var currentProfile = GetCurrentProfile();
        if (currentProfile == profileName)
        {
            if (File.Exists(CurrentProfileFile))
            {
                File.Delete(CurrentProfileFile);
            }
        }
    }

    public bool ProfileExists(string profileName)
    {
        var profilePath = Path.Combine(ProfilesDir, $"{profileName}.json");
        return File.Exists(profilePath);
    }

    public string GetConfigDirectory()
    {
        return UserConfigDir;
    }
}
