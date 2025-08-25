using System.CommandLine;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for profile commands.
/// </summary>
public static class ProfileCommandOptions
{
    /// <summary>
    /// Global debug option for all profile commands.
    /// </summary>
    public static readonly Option<bool> DebugOption = new("--debug")
    {
        Description = "Enable verbose debug logging for network calls and operations"
    };

    /// <summary>
    /// Profile name option - optional for get command (shows current if not specified).
    /// </summary>
    public static readonly Option<string> ProfileNameOption = new("--name")
    {
        Required = false,
        Description = "Name of the profile"
    };

    /// <summary>
    /// Profile name option - required for create, set, and delete commands.
    /// </summary>
    public static readonly Option<string> ProfileNameRequiredOption = new("--name")
    {
        Required = true,
        Description = "Name of the profile"
    };

    /// <summary>
    /// Resource URL option for profile create command.
    /// </summary>
    public static readonly Option<string> ResourceUrlOption = new("--url")
    {
        Required = true,
        Description = "Resource URL for the profile"
    };

    /// <summary>
    /// Set current option for profile create command.
    /// </summary>
    public static readonly Option<bool> SetCurrentOption = new("--set-current")
    {
        Description = "Set this profile as the current profile after creation"
    };
}
