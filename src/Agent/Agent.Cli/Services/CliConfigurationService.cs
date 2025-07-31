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
            return JsonSerializer.Deserialize<CliConfiguration>(json);
        }
        catch
        {
            return null;
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
}
