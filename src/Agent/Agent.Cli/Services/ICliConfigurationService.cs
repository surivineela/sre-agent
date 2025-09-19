using Agent.Cli.Models;

namespace Agent.Cli.Services;
public interface ICliConfigurationService
{
    static abstract bool IsLocalhost(string url);
    void DeleteProfile(string profileName);
    IEnumerable<string> GetAvailableProfiles();
    string GetConfigDirectory();
    string? GetCurrentProfile();
    Task<bool> HasValidConfigurationAsync();
    Task<CliConfiguration?> LoadConfigurationAsync();
    Task<CliConfiguration?> LoadProfileAsync(string profileName);
    bool ProfileExists(string profileName);
    Task SaveConfigurationAsync(CliConfiguration config);
    Task SaveProfileAsync(string profileName, CliConfiguration config);
    Task SetCurrentProfileAsync(string profileName);
}