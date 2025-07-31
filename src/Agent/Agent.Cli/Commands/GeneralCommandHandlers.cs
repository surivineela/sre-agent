using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles general CLI commands like init and list.
/// </summary>
public static class GeneralCommandHandlers
{
    /// <summary>
    /// Handles the init command with a specific resource URL.
    /// </summary>
    public static async Task HandleInitCommandWithResourceUrl(string resourceUrl)
    {
        try
        {
            // Validate URL format
            if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
            {
                Console.WriteLine("❌ Invalid URL format provided.");
                Environment.Exit(1);
                return;
            }

            // Create configuration
            var config = new CliConfiguration
            {
                ResourceUrl = resourceUrl,
                AuthRequired = !CliConfigurationService.IsLocalhost(resourceUrl),
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Save configuration
            var configService = new CliConfigurationService();
            await configService.SaveConfigurationAsync(config);

            // Create directory structure
            Directory.CreateDirectory("agents");
            Directory.CreateDirectory("tools");
            Directory.CreateDirectory("connectors");

            // Copy example files
            await ExampleFileManager.CopyExampleFilesAsync();

            // Create instructions.md file in .github folder
            await InstructionsFileService.CreateInstructionsFileAsync();

            Console.WriteLine($"✅ SREAgent CLI initialized successfully!");
            Console.WriteLine($"   Resource URL: {resourceUrl}");
            Console.WriteLine($"   Auth Required: {config.AuthRequired}");
            Console.WriteLine($"   Created directories: agents/, tools/, connectors/, .github/");
            Console.WriteLine($"   Added example files in each directory");
            Console.WriteLine($"   Created comprehensive instructions file: .github/instructions.md");

            // Test connection
            Console.WriteLine("\n🔄 Testing connection...");
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);
            Console.WriteLine(response);

            // Exit with appropriate code, but don't fail initialization for connection issues
            if (!success)
            {
                Console.WriteLine("⚠️  Note: Initialization completed successfully, but connection test failed.");
                Console.WriteLine("   You can still use srectl commands that don't require server connection.");
            }

            Environment.Exit(0); // Always exit successfully if initialization steps completed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Initialization failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the list agents command.
    /// </summary>
    public static async Task HandleListAgentsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListAgentsAsync();
        
        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list tools command.
    /// </summary>
    public static async Task HandleListToolsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListToolsAsync();
        
        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the apply-yaml command.
    /// </summary>
    public static async Task HandleApplyYamlCommand(ParseResult parseResult)
    {
        try
        {
            var filePath = parseResult.GetValue(AgentCommandOptions.ApplyYamlFileOption);

            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine("❌ File path is required.");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File not found: {filePath}");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();
            var (success, response) = await apiService.ApplyYamlFileAsync(filePath);

            Console.WriteLine(response);
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to apply YAML file: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
