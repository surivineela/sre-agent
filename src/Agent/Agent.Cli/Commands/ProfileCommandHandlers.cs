using System.CommandLine;
using System.CommandLine.Parsing;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles profile-related command operations.
/// </summary>
public static class ProfileCommandHandlers
{
    /// <summary>
    /// Handles the profile list command.
    /// </summary>
    public static Task HandleListCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ProfileCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting profile list command");

        try
        {
            var configService = new CliConfigurationService();
            var profiles = configService.GetAvailableProfiles().ToList();
            var currentProfile = configService.GetCurrentProfile();

            DebugLogger.Debug("Profiles", $"Found {profiles.Count} profiles, current: {currentProfile ?? "none"}");

            if (!profiles.Any())
            {
                Console.WriteLine("No profiles found.");
                return Task.CompletedTask;
            }

            Console.WriteLine("Available profiles:");
            foreach (var profile in profiles)
            {
                var marker = profile == currentProfile ? " (current)" : "";
                Console.WriteLine($"  • {profile}{marker}");
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ProfileList failed: {ex.Message}");
            Console.WriteLine($"❌ Failed to list profiles: {ex.Message}");
            Environment.Exit(1);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the profile get command.
    /// </summary>
    public static async Task HandleGetCommand(ParseResult parseResult)
    {
        try
        {
            var profileName = parseResult.GetValue(ProfileCommandOptions.ProfileNameOption);

            if (string.IsNullOrWhiteSpace(profileName))
            {
                // Show current profile if no name specified
                var configService = new CliConfigurationService();
                var currentProfile = configService.GetCurrentProfile();

                if (string.IsNullOrEmpty(currentProfile))
                {
                    Console.WriteLine("No current profile set.");
                    Environment.Exit(1);
                    return;
                }

                var currentConfig = await configService.LoadProfileAsync(currentProfile);
                if (currentConfig == null)
                {
                    Console.WriteLine($"❌ Current profile '{currentProfile}' not found.");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Current profile: {currentProfile}");
                DisplayProfileDetails(currentConfig);
            }
            else
            {
                // Show specific profile
                var configService = new CliConfigurationService();
                var profile = await configService.LoadProfileAsync(profileName);

                if (profile == null)
                {
                    Console.WriteLine($"❌ Profile '{profileName}' not found.");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Profile: {profileName}");
                DisplayProfileDetails(profile);
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to get profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the profile create command.
    /// </summary>
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        // Set debug mode first
        var debug = parseResult.GetValue(ProfileCommandOptions.DebugOption);
        DebugLogger.SetDebugMode(debug);

        DebugLogger.Debug("Command", "Starting profile create command");

        try
        {
            var profileName = parseResult.GetValue(ProfileCommandOptions.ProfileNameRequiredOption);
            var resourceUrl = parseResult.GetValue(ProfileCommandOptions.ResourceUrlOption);
            var setCurrent = parseResult.GetValue(ProfileCommandOptions.SetCurrentOption);

            DebugLogger.Debug("Parameters", $"ProfileName: {profileName}, ResourceUrl: {resourceUrl}, SetCurrent: {setCurrent}");

            if (string.IsNullOrWhiteSpace(profileName))
            {
                Console.WriteLine("❌ Profile name is required.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(resourceUrl))
            {
                Console.WriteLine("❌ Resource URL is required.");
                Environment.Exit(1);
                return;
            }

            // Validate URL format
            if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
            {
                DebugLogger.Debug("Validation", $"Invalid URL format: {resourceUrl}");
                Console.WriteLine("❌ Invalid URL format provided.");
                Environment.Exit(1);
                return;
            }

            var configService = new CliConfigurationService();

            // Check if profile already exists
            var existingProfile = await configService.LoadProfileAsync(profileName);
            if (existingProfile != null)
            {
                DebugLogger.Debug("Validation", $"Profile already exists: {profileName}");
                Console.WriteLine($"❌ Profile '{profileName}' already exists.");
                Environment.Exit(1);
                return;
            }

            // Create new profile configuration
            var config = new CliConfiguration
            {
                ResourceUrl = resourceUrl,
                AuthRequired = !CliConfigurationService.IsLocalhost(resourceUrl),
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            DebugLogger.LogConfig("ResourceUrl", config.ResourceUrl);
            DebugLogger.LogConfig("AuthRequired", config.AuthRequired.ToString());

            // Save the profile
            await configService.SaveProfileAsync(profileName, config);

            Console.WriteLine($"✅ Profile '{profileName}' created successfully!");
            Console.WriteLine($"   Resource URL: {resourceUrl}");
            Console.WriteLine($"   Auth Required: {config.AuthRequired}");

            // Set as current profile if requested
            if (setCurrent)
            {
                await configService.SetCurrentProfileAsync(profileName);
                Console.WriteLine($"   Set as current profile: Yes");
            }

            // Test connection
            Console.WriteLine("\n🔄 Testing connection...");
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);
            Console.WriteLine(response);

            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ProfileCreate failed: {ex.Message}");
            Console.WriteLine($"❌ Failed to create profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the profile set command.
    /// </summary>
    public static async Task HandleSetCommand(ParseResult parseResult)
    {
        try
        {
            var profileName = parseResult.GetValue(ProfileCommandOptions.ProfileNameRequiredOption);

            if (string.IsNullOrWhiteSpace(profileName))
            {
                Console.WriteLine("❌ Profile name is required.");
                Environment.Exit(1);
                return;
            }

            var configService = new CliConfigurationService();

            // Check if profile exists
            var profile = await configService.LoadProfileAsync(profileName);
            if (profile == null)
            {
                Console.WriteLine($"❌ Profile '{profileName}' not found.");
                Environment.Exit(1);
                return;
            }

            // Set as current profile
            await configService.SetCurrentProfileAsync(profileName);

            // Also save as the main configuration for backward compatibility
            await configService.SaveConfigurationAsync(profile);

            Console.WriteLine($"✅ Switched to profile '{profileName}'");
            Console.WriteLine($"   Resource URL: {profile.ResourceUrl}");
            Console.WriteLine($"   Auth Required: {profile.AuthRequired}");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to switch profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the profile delete command.
    /// </summary>
    public static Task HandleDeleteCommand(ParseResult parseResult)
    {
        try
        {
            var profileName = parseResult.GetValue(ProfileCommandOptions.ProfileNameRequiredOption);

            if (string.IsNullOrWhiteSpace(profileName))
            {
                Console.WriteLine("❌ Profile name is required.");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            var configService = new CliConfigurationService();

            // Check if profile exists
            var profilePath = Path.Combine(".sreagent-profiles", $"{profileName}.json");
            if (!File.Exists(profilePath))
            {
                Console.WriteLine($"❌ Profile '{profileName}' not found.");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            // Check if it's the current profile
            var currentProfile = configService.GetCurrentProfile();
            if (currentProfile == profileName)
            {
                Console.WriteLine($"❌ Cannot delete '{profileName}' because it's the current profile.");
                Console.WriteLine("   Switch to another profile first using 'srectl profile set <profile-name>'");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            // Delete the profile file
            File.Delete(profilePath);

            Console.WriteLine($"✅ Profile '{profileName}' deleted successfully.");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to delete profile: {ex.Message}");
            Environment.Exit(1);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Displays profile configuration details.
    /// </summary>
    private static void DisplayProfileDetails(CliConfiguration config)
    {
        Console.WriteLine($"   Resource URL: {config.ResourceUrl}");
        Console.WriteLine($"   Auth Required: {config.AuthRequired}");
        Console.WriteLine($"   Created: {config.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"   Last Updated: {config.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC");
    }
}
