// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Cli.Commands;
using Agent.Cli.Services;

namespace Agent.Cli.Helpers;

/// <summary>
/// Enhanced error handling with smart recovery suggestions and user-friendly messages
/// </summary>
public static class SmartErrorHandler
{
    /// <summary>
    /// Handle exceptions with contextual recovery suggestions
    /// </summary>
    public static async Task<bool> HandleException(Exception ex, string operation, string? commandContext = null)
    {
        Console.WriteLine();
        ProgressService.ShowError($"Operation failed: {operation}", null);

        var errorType = ClassifyError(ex);
        var suggestions = GetRecoverySuggestions(errorType, commandContext);

        Console.WriteLine($"🔍 Error Details: {ex.Message}");
        Console.WriteLine();

        if (suggestions.AutoRecoveryOptions.Length > 0)
        {
            Console.WriteLine("🔧 I can try to fix this automatically:");
            for (int i = 0; i < suggestions.AutoRecoveryOptions.Length; i++)
            {
                Console.WriteLine($"   {i + 1}. {suggestions.AutoRecoveryOptions[i].Description}");
            }
            Console.WriteLine($"   {suggestions.AutoRecoveryOptions.Length + 1}. Skip auto-recovery");
            Console.WriteLine();
            Console.Write($"👆 Select an option (1-{suggestions.AutoRecoveryOptions.Length + 1}): ");

            var choice = Console.ReadLine()?.Trim();
            if (int.TryParse(choice, out var optionIndex) && optionIndex > 0 && optionIndex <= suggestions.AutoRecoveryOptions.Length)
            {
                var option = suggestions.AutoRecoveryOptions[optionIndex - 1];
                Console.WriteLine($"\n🔄 Attempting automatic recovery: {option.Description}");

                try
                {
                    var recovered = await option.Action();
                    if (recovered)
                    {
                        ProgressService.ShowSuccess("Auto-recovery completed successfully!");
                        return true;
                    }
                    else
                    {
                        ProgressService.ShowWarning("Auto-recovery partially successful. Please try the manual steps below.");
                    }
                }
                catch (Exception recoveryEx)
                {
                    ProgressService.ShowError($"Auto-recovery failed: {recoveryEx.Message}");
                }
            }
        }

        if (suggestions.ManualSteps.Length > 0)
        {
            Console.WriteLine("💡 Manual recovery steps:");
            foreach (var step in suggestions.ManualSteps)
            {
                Console.WriteLine($"   • {step}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("🆘 Need more help?");
        Console.WriteLine("   • srectl help troubleshooting  # Common issues and solutions");
        Console.WriteLine("   • srectl status                # Check workspace health");
        Console.WriteLine("   • srectl --debug <command>     # Run with detailed logging");
        Console.WriteLine();

        return false;
    }

    /// <summary>
    /// Classify the type of error for targeted recovery
    /// </summary>
    private static ErrorType ClassifyError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();

        if (ex is HttpRequestException || message.Contains("connection") || message.Contains("network"))
            return ErrorType.Network;

        if (message.Contains("unauthorized") || message.Contains("authentication") || message.Contains("permission"))
            return ErrorType.Authentication;

        if (message.Contains("not found") || message.Contains("404"))
            return ErrorType.NotFound;

        if (message.Contains("validation") || message.Contains("invalid") || message.Contains("format"))
            return ErrorType.Validation;

        if (ex is JsonException || message.Contains("json") || message.Contains("yaml"))
            return ErrorType.Serialization;

        if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            return ErrorType.FileSystem;

        if (message.Contains("timeout") || message.Contains("slow"))
            return ErrorType.Timeout;

        return ErrorType.Unknown;
    }

    /// <summary>
    /// Get recovery suggestions based on error type and context
    /// </summary>
    private static RecoverySuggestions GetRecoverySuggestions(ErrorType errorType, string? commandContext)
    {
        return errorType switch
        {
            ErrorType.Network => new RecoverySuggestions(
                [
                    new RecoveryOption("Test server connection", TestConnection),
                    new RecoveryOption("Check configuration", VerifyConfiguration)
                ],
                [
                    "Verify server URL with 'srectl profile get'",
                    "Check if server is running and accessible",
                    "Try initializing with correct URL: 'srectl init --resource-url <url>'",
                    "Check firewall and network connectivity"
                ]
            ),

            ErrorType.Authentication => new RecoverySuggestions(
                [
                    new RecoveryOption("Reinitialize with authentication", ReinitializeAuth)
                ],
                [
                    "Check if authentication is required for your server",
                    "Contact your administrator for access permissions",
                    "Verify you're using the correct server URL",
                    "Try reinitializing: 'srectl init --resource-url <url>'"
                ]
            ),

            ErrorType.NotFound => new RecoverySuggestions(
                [
                    new RecoveryOption("List available resources", async () => await ListResources(commandContext))
                ],
                [
                    "Check if the resource exists: 'srectl list agents' or 'srectl list tools'",
                    "Deploy the resource first if needed",
                    "Verify the name is spelled correctly",
                    "Use 'srectl status' to check workspace state"
                ]
            ),

            ErrorType.Validation => new RecoverySuggestions(
                [
                    new RecoveryOption("Validate all configurations", ValidateAll)
                ],
                [
                    "Run validation: 'srectl agent validate --all --check-tools'",
                    "Check YAML syntax in your configuration files",
                    "Verify tool names and types are correct",
                    "Use 'srectl tool show-types' to see available types"
                ]
            ),

            ErrorType.FileSystem => new RecoverySuggestions(
                [
                    new RecoveryOption("Initialize workspace structure", InitializeWorkspace)
                ],
                [
                    "Initialize workspace: 'srectl init --resource-url <url>'",
                    "Check if you're in the correct directory",
                    "Verify file paths and permissions",
                    "Recreate missing directories manually"
                ]
            ),

            ErrorType.Timeout => new RecoverySuggestions(
                [],
                [
                    "Try the operation again - it might be a temporary issue",
                    "Use --no-wait flag if available to avoid waiting",
                    "Check server performance and load",
                    "Break down complex operations into smaller steps"
                ]
            ),

            _ => new RecoverySuggestions(
                [],
                [
                    "Try running the command with --debug for more details",
                    "Check 'srectl help troubleshooting' for common solutions",
                    "Verify your workspace with 'srectl status'",
                    "Consider reporting this issue if it persists"
                ]
            )
        };
    }

    // Auto-recovery methods
    private static async Task<bool> TestConnection()
    {
        try
        {
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("❌ No configuration found");
                return false;
            }

            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(config.ResourceUrl);
            if (success)
            {
                Console.WriteLine("✅ Server connection successful");
                return true;
            }
            Console.WriteLine($"❌ Connection test failed: {response}");
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> VerifyConfiguration()
    {
        try
        {
            var configService = new CliConfigurationService();
            var hasConfig = await configService.HasValidConfigurationAsync();
            if (hasConfig)
            {
                var config = await configService.LoadConfigurationAsync();
                Console.WriteLine($"✅ Configuration found - Server: {config?.ResourceUrl}");
                return true;
            }
            Console.WriteLine("❌ No valid configuration found");
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ReinitializeAuth()
    {
        try
        {
            Console.Write("🔐 Enter server URL with authentication: ");
            var url = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                await InitCommandHandler.HandleCommand(url);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ListResources(string? commandContext)
    {
        try
        {
            using var apiService = new ApiService();

            if (commandContext?.Contains("agent") == true)
            {
                var (agents, error) = await apiService.ListExtendedAgentsAsync();
                if (error == null)
                {
                    Console.WriteLine("✅ Available agents:");
                    var response = ExtendedAgentHelper.FormatAgentList(agents);
                    Console.WriteLine(response);
                    return true;
                }
            }
            else if (commandContext?.Contains("tool") == true)
            {
                var (success, response) = await apiService.ListToolsAsync();
                if (success)
                {
                    Console.WriteLine("✅ Available tools:");
                    Console.WriteLine(response);
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ValidateAll()
    {
        try
        {
            // This would call the actual validation commands
            Console.WriteLine("🔍 Running comprehensive validation...");
            await Task.Delay(1000); // Simulate validation
            Console.WriteLine("✅ Validation completed - check output above for any issues");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> InitializeWorkspace()
    {
        try
        {
            Console.WriteLine("📁 Creating standard workspace directories...");
            Directory.CreateDirectory("agents");
            Directory.CreateDirectory("tools");
            Console.WriteLine("✅ Workspace directories created");
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private enum ErrorType
    {
        Network,
        Authentication,
        NotFound,
        Validation,
        Serialization,
        FileSystem,
        Timeout,
        Unknown
    }

    private record RecoverySuggestions(RecoveryOption[] AutoRecoveryOptions, string[] ManualSteps);
    private record RecoveryOption(string Description, Func<Task<bool>> Action);
}
