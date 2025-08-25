using System.Text.Json;
using Agent.Cli.Models;

namespace Agent.Cli.Services;

public class CliConfigurationService
{
    private const string ConfigFileName = ".sreagent-config.json";
    
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

    public static bool IsLocalhost(string url)
    {
        return url.Contains("localhost") || url.Contains("127.0.0.1");
    }

    public IEnumerable<string> GetAvailableProfiles()
    {
        const string profilesDir = ".sreagent-profiles";
        if (!Directory.Exists(profilesDir))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(profilesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))!;
    }

    public async Task<CliConfiguration?> LoadProfileAsync(string profileName)
    {
        try
        {
            var profilePath = Path.Combine(".sreagent-profiles", $"{profileName}.json");
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
        var profilesDir = ".sreagent-profiles";
        Directory.CreateDirectory(profilesDir);

        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(profilePath, json);
    }

    public string? GetCurrentProfile()
    {
        const string currentProfileFile = ".sreagent-current-profile";
        if (!File.Exists(currentProfileFile))
            return null;

        try
        {
            return File.ReadAllText(currentProfileFile).Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCurrentProfileAsync(string profileName)
    {
        const string currentProfileFile = ".sreagent-current-profile";
        await File.WriteAllTextAsync(currentProfileFile, profileName);
    }
}
