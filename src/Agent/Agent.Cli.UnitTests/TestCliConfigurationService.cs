using System.Text.Json;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.UnitTests;

public class TestCliConfigurationService : ICliConfigurationService
{
    private readonly string _configPath;
    private readonly string _testConfigDir;
    private readonly string _profilesDir;
    private readonly string _currentProfileFile;

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public TestCliConfigurationService(string configPath)
    {
        _configPath = configPath;
        _testConfigDir = Path.GetDirectoryName(configPath) ?? Path.GetTempPath();
        _profilesDir = Path.Combine(_testConfigDir, "profiles");
        _currentProfileFile = Path.Combine(_testConfigDir, "current-profile");

        // Ensure directories exist
        Directory.CreateDirectory(_testConfigDir);
        Directory.CreateDirectory(_profilesDir);
    }

    public static bool IsLocalhost(string url)
    {
        return true;
    }

    public async Task<CliConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<CliConfiguration>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void DeleteProfile(string profileName)
    {
        var profilePath = Path.Combine(_profilesDir, $"{profileName}.json");
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        // If this was the current profile, clear the current profile setting
        var currentProfile = GetCurrentProfile();
        if (currentProfile == profileName)
        {
            if (File.Exists(_currentProfileFile))
            {
                File.Delete(_currentProfileFile);
            }
        }
    }

    public IEnumerable<string> GetAvailableProfiles()
    {
        if (!Directory.Exists(_profilesDir))
        {
            return [];
        }

        return Directory.GetFiles(_profilesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    public string GetConfigDirectory()
    {
        return _testConfigDir;
    }

    public string? GetCurrentProfile()
    {
        if (!File.Exists(_currentProfileFile))
            return null;

        try
        {
            return File.ReadAllText(_currentProfileFile).Trim();
        }
        catch
        {
            return null;
        }
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

    public async Task<CliConfiguration?> LoadProfileAsync(string profileName)
    {
        try
        {
            var profilePath = Path.Combine(_profilesDir, $"{profileName}.json");
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

    public bool ProfileExists(string profileName)
    {
        var profilePath = Path.Combine(_profilesDir, $"{profileName}.json");
        return File.Exists(profilePath);
    }

    public async Task SaveConfigurationAsync(CliConfiguration config)
    {
        var json = JsonSerializer.Serialize(config, _options);
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task SaveProfileAsync(string profileName, CliConfiguration config)
    {
        var profilePath = Path.Combine(_profilesDir, $"{profileName}.json");
        
        var json = JsonSerializer.Serialize(config, _options);
        await File.WriteAllTextAsync(profilePath, json);
    }

    public async Task SetCurrentProfileAsync(string profileName)
    {
        await File.WriteAllTextAsync(_currentProfileFile, profileName);
    }
}
