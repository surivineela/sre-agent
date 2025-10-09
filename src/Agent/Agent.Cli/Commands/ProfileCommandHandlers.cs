// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
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
        DebugLogger.Debug("Command", "Starting profile list command");

        try
        {
            var configService = new CliConfigurationService();
            var profiles = configService.GetAvailableProfiles().ToList();
            var currentProfile = configService.GetCurrentProfile();

            DebugLogger.Debug("Profiles", $"Found {profiles.Count} profiles, current: {currentProfile ?? "none"}");

            if (!profiles.Any())
            {
                ConsoleUI.WriteInfo("No profiles found.", ConsoleColor.Gray);
                return Task.CompletedTask;
            }

            ConsoleUI.WriteSection("Available profiles");
            foreach (var profile in profiles)
            {
                var marker = profile == currentProfile ? " (current)" : "";
                ConsoleUI.WriteBullet($"{profile}{marker}", ConsoleColor.White);
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ProfileList failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to list profiles: {ex.Message}");
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
                    ConsoleUI.WriteStatus(false, $"Current profile '{currentProfile}' not found.");
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
                    ConsoleUI.WriteStatus(false, $"Profile '{profileName}' not found.");
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
            ConsoleUI.WriteStatus(false, $"Failed to get profile: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the profile create command.
    /// </summary>
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting profile create command");

        try
        {
            var profileName = parseResult.GetValue(ProfileCommandOptions.ProfileNameRequiredOption);
            var resourceUrl = parseResult.GetValue(ProfileCommandOptions.ResourceUrlOption);
            var setCurrent = parseResult.GetValue(ProfileCommandOptions.SetCurrentOption);

            DebugLogger.Debug("Parameters", $"ProfileName: {profileName}, ResourceUrl: {resourceUrl}, SetCurrent: {setCurrent}");

            if (string.IsNullOrWhiteSpace(profileName))
            {
                ConsoleUI.WriteStatus(false, "Profile name is required.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(resourceUrl))
            {
                ConsoleUI.WriteStatus(false, "Resource URL is required.");
                Environment.Exit(1);
                return;
            }

            // Validate URL format
            if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
            {
                DebugLogger.Debug("Validation", $"Invalid URL format: {resourceUrl}");
                ConsoleUI.WriteStatus(false, "Invalid URL format provided.");
                Environment.Exit(1);
                return;
            }

            var configService = new CliConfigurationService();

            // Check if profile already exists
            var existingProfile = await configService.LoadProfileAsync(profileName);
            if (existingProfile != null)
            {
                DebugLogger.Debug("Validation", $"Profile already exists: {profileName}");

                // Test connection to verify the existing profile is still valid
                ConsoleUI.WriteInfo("Profile exists, testing connection...", ConsoleColor.Cyan);
                using var existingApiService = new ApiService();
                var (existingSuccess, existingResponse) = await existingApiService.TestConnectionAsync(existingProfile.ResourceUrl);

                if (!existingSuccess)
                {
                    ConsoleUI.WriteStatus(false, $"Existing profile '{profileName}' has connection issues");
                    Console.WriteLine($"   Resource URL: {existingProfile.ResourceUrl}");
                    Console.WriteLine($"   Error: {existingResponse}");
                    Environment.Exit(1);
                    return;
                }

                // Profile exists and connection is successful
                ConsoleUI.WriteStatus(true, $"Profile '{profileName}' already exists and connection is valid");
                Console.WriteLine($"   Resource URL: {existingProfile.ResourceUrl}");
                Console.WriteLine($"   Auth Required: {existingProfile.AuthRequired}");
                Console.WriteLine($"   Connection successful");

                // Set as current profile if requested
                if (setCurrent)
                {
                    await configService.SetCurrentProfileAsync(profileName);
                    Console.WriteLine($"   Set as current profile: Yes");
                }

                Environment.Exit(0);
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

            // Test connection first before saving
            ConsoleUI.WriteInfo("Testing connection...", ConsoleColor.Cyan);
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Profile creation failed: Unable to connect to server");
                Console.WriteLine($"   Resource URL: {resourceUrl}");
                Console.WriteLine($"   Error: {response}");
                Environment.Exit(1);
                return;
            }

            // Save the profile only after successful connection test
            await configService.SaveProfileAsync(profileName, config);

            // Set as current profile if requested
            if (setCurrent)
            {
                await configService.SetCurrentProfileAsync(profileName);
            }

            // Show success message only after everything succeeds
            ConsoleUI.WriteStatus(true, $"Profile '{profileName}' created successfully!");
            Console.WriteLine($"   Resource URL: {resourceUrl}");
            Console.WriteLine($"   Auth Required: {config.AuthRequired}");
            if (setCurrent)
            {
                Console.WriteLine($"   Set as current profile: Yes");
            }
            Console.WriteLine($"   Connection: {response}");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ProfileCreate failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to create profile: {ex.Message}");
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
                ConsoleUI.WriteStatus(false, "Profile name is required.");
                Environment.Exit(1);
                return;
            }

            var configService = new CliConfigurationService();

            // Check if profile exists
            var profile = await configService.LoadProfileAsync(profileName);
            if (profile == null)
            {
                ConsoleUI.WriteStatus(false, $"Profile '{profileName}' not found.");
                Environment.Exit(1);
                return;
            }

            // Set as current profile
            await configService.SetCurrentProfileAsync(profileName);

            // Also save as the main configuration for backward compatibility
            await configService.SaveConfigurationAsync(profile);

            ConsoleUI.WriteStatus(true, $"Switched to profile '{profileName}'");
            Console.WriteLine($"   Resource URL: {profile.ResourceUrl}");
            Console.WriteLine($"   Auth Required: {profile.AuthRequired}");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to switch profile: {ex.Message}");
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
                ConsoleUI.WriteStatus(false, "Profile name is required.");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            var configService = new CliConfigurationService();

            // Check if profile exists
            if (!configService.ProfileExists(profileName))
            {
                ConsoleUI.WriteStatus(false, $"Profile '{profileName}' not found.");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            // Check if it's the current profile
            var currentProfile = configService.GetCurrentProfile();
            if (currentProfile == profileName)
            {
                ConsoleUI.WriteStatus(false, $"Cannot delete '{profileName}' because it's the current profile.");
                Console.WriteLine("   Switch to another profile first using 'srectl profile set <profile-name>'");
                Environment.Exit(1);
                return Task.CompletedTask;
            }

            // Delete the profile
            configService.DeleteProfile(profileName);

            ConsoleUI.WriteStatus(true, $"Profile '{profileName}' deleted successfully.");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to delete profile: {ex.Message}");
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
