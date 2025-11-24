using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Core.Services.LinuxAppService.Validators;

/// <summary>
/// Validates LinuxFxVersion configuration for Linux App Services using AI-based validation.
/// </summary>
public class LinuxFxVersionValidator : ILinuxAppServiceConfigValidator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<LinuxFxVersionValidator> _logger;

    public LinuxAppServiceConfigIssueType IssueType => LinuxAppServiceConfigIssueType.InvalidLinuxFxVersion;

    private static readonly HashSet<string> SupportedRuntimes =
    [
        "PYTHON", "NODE", "DOTNETCORE", "PHP"
    ];

    public LinuxFxVersionValidator(
        IChatClientProvider chatClientProvider,
        ILogger<LinuxFxVersionValidator> logger)
    {
        _chatClient = chatClientProvider.SmallFastModel;
        _logger = logger;
    }

    /// <summary>
    /// Validates the LinuxFxVersion format and runtime configuration.
    /// </summary>
    /// <param name="siteConfig">The Linux App Service configuration to validate</param>
    /// <returns>LinuxAppServiceConfigIssue if validation fails; otherwise, null.</returns>
    public async Task<LinuxAppServiceConfigIssue?> ValidateAsync(LinuxAppServiceConfiguration siteConfig)
    {
        var issue = new LinuxAppServiceConfigIssue(
            ResourceId: siteConfig.ResourceId,
            SiteName: siteConfig.Name,
            Location: siteConfig.Location,
            Type: IssueType,
            Details: $"Invalid LinuxFxVersion format '{siteConfig.LinuxFxVersion}'",
            Recommendation: string.Empty);

        // Check if it's a Linux App Service
        if (string.IsNullOrEmpty(siteConfig.AppKind)
            || !siteConfig.AppKind.Contains("linux", StringComparison.OrdinalIgnoreCase))
        {
            // Not a Linux App Service - skipping LinuxFxVersion validation
            return null;
        }

        // Check if LinuxFxVersion is empty or null
        if (string.IsNullOrEmpty(siteConfig.LinuxFxVersion))
        {
            // LinuxFxVersion is empty - defaulting to PHP|8.2
            return null;
        }

        // Check for invalid or malformed LinuxFxVersion formats
        if (!siteConfig.LinuxFxVersion.Contains('|'))
        {
            return issue with
            {
                Recommendation = "Specify LinuxFxVersion in the format : RUNTIME|VERSION (e.g., 'PYTHON|3.11', 'NODE|18-lts', 'DOTNETCORE|8.0')"
            };
        }

        var parts = siteConfig.LinuxFxVersion.Split('|');
        if (parts.Length != 2)
        {
            return issue with
            {
                Recommendation = "Specify LinuxFxVersion in the format : RUNTIME|VERSION (e.g., 'PYTHON|3.11', 'NODE|18-lts', 'DOTNETCORE|8.0')"
            };
        }

        var result = await ValidateLinuxFxVersionAsync(siteConfig.LinuxFxVersion);

        if (result.IsValid)
        {
            return null;
        }

        return issue with
        {
            Details = result.Reason,
            Recommendation = result.RecommendedValue ?? string.Empty
        };
    }

    private async Task<ValidationResult> ValidateLinuxFxVersionAsync(string linuxFxVersion)
    {
        var parts = linuxFxVersion.Split('|');
        var runtime = parts[0].ToUpperInvariant();
        var version = parts[1];

        if (!SupportedRuntimes.Contains(runtime))
        {
            return new ValidationResult()
            {
                IsValid = false,
                Reason = $"LinuxFxVersion Validation is not supported for {runtime}",
            };
        }

        var systemPrompt = $@"
            You are LinuxFxVersion Validator agent. Always address yourself as 'LinuxFxVersion Validator'.

            LinuxFxVersion always has the format RUNTIME|VERSION, where RUNTIME is one of: {string.Join(", ", SupportedRuntimes)}.

            For each RUNTIME, the VERSION must follow specific formats:
            - PYTHON: Must be in the format '3.x' (e.g., '3.8', '3.9', '3.10', '3.11', '3.12').
            - NODE: Must be in the format 'x-lts' (e.g., '16-lts', '18-lts', '20-lts', '22-lts').
            - DOTNETCORE: Must be in the format 'x.0' (e.g., '6.0', '7.0', '8.0', '9.0').
            - PHP: Must be in the format 'x.y' (e.g., '7.4', '8.0', '8.1', '8.2', '8.3').

            Patch versions (e.g., '3.11.2', '18.16.0') are not supported in LinuxFxVersion. Use only major.minor versions (e.g., '3.11', '18-lts', '8.0').

            IMPORTANT: 
            1. Only validate the FORMAT of the version. Do NOT check for end-of-life (EOL) status or deprecated versions.
            2. When providing recommendations, PRESERVE the user's intended major version. For example:
               - If they use 'NODE|22', recommend 'NODE|22-lts' (NOT 'NODE|20-lts')
               - If they use 'PYTHON|3.12.5', recommend 'PYTHON|3.12' (NOT 'PYTHON|3.11')
               - If they use 'DOTNETCORE|8', recommend 'DOTNETCORE|8.0' (NOT 'DOTNETCORE|7.0')
            3. Only suggest a different major version if the provided version is clearly invalid or unsupported.

            Examples:
            - Input: RUNTIME='PYTHON', VERSION='3.11' 
              Output: {{ ""IsValid"": true, ""Reason"": ""Valid as it correctly follows the forma RUNTIME|VERSION"", ""RecommendedValue"": null }}
            
            - Input: RUNTIME='NODE', VERSION='18-lts' 
              Output: {{ ""IsValid"": true, ""Reason"": ""Valid as it correctly follows the forma RUNTIME|VERSION"", ""RecommendedValue"": null }}
            
            - Input: RUNTIME='PYTHON', VERSION='3.12.5' 
              Output: {{ ""IsValid"": false, ""Reason"": ""Invalid Python version format. Patch versions are not supported in LinuxFxVersion. Use major.minor format only."", ""RecommendedValue"": ""PYTHON|3.12"" }}
            
            - Input: RUNTIME='NODE', VERSION='22' 
              Output: {{ ""IsValid"": false, ""Reason"": ""Invalid Node.js version format. Node.js versions must include the '-lts' suffix."", ""RecommendedValue"": ""NODE|22-lts"" }}
            
            - Input: RUNTIME='DOTNETCORE', VERSION='8' 
              Output: {{ ""IsValid"": false, ""Reason"": ""Invalid .NET Core version format. Version must be in 'x.0' format (e.g., '8.0')."", ""RecommendedValue"": ""DOTNETCORE|8.0"" }}
            
            - Input: RUNTIME='PHP', VERSION='8.2.10' 
              Output: {{ ""IsValid"": false, ""Reason"": ""Invalid PHP version format. Patch versions are not supported in LinuxFxVersion. Use major.minor format only."", ""RecommendedValue"": ""PHP|8.2"" }}
        ";

        var messages = new List<AIChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"Validate the VERSION '{version}' for RUNTIME '{runtime}' in LinuxFxVersion '{linuxFxVersion}'. If invalid, provide a recommendation that preserves the user's intended version. Return JSON with IsValid, Reason, and RecommendedValue properties.")
        };

        try
        {
            var response = await _chatClient.GetResponseAsync<ValidationResult>(messages);

            if (response?.Result == null)
            {
                _logger.LogInternalWarning("Failed to get validation response from AI service for LinuxFxVersion: {LinuxFxVersion}", linuxFxVersion);
                return new ValidationResult()
                {
                    IsValid = true,
                    Reason = "Validation could not be performed; assuming valid.",
                };
            }

            var validationResult = response.Result;

            _logger.LogInternalInformation("LinuxFxVersion validation result for {LinuxFxVersion}: {IsValid}, Reason: {Reason}, RecommendedValue: {RecommendedValue}",
                linuxFxVersion, validationResult.IsValid, validationResult.Reason, validationResult.RecommendedValue);

            return validationResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error occurred during LinuxFxVersion validation for {LinuxFxVersion}", linuxFxVersion);

            return new ValidationResult()
            {
                IsValid = true,
                Reason = "Validation could not be performed due to an error; assuming valid.",
            };
        }
    }

    private class ValidationResult
    {
        [Description("Indicates whether the LinuxFxVersion is valid")]
        public bool IsValid { get; set; }

        [Description("Explanation of the validation result")]
        public string Reason { get; set; } = string.Empty;

        [Description("Recommended LinuxFxVersion if the original is invalid")]
        public string? RecommendedValue { get; set; }
    }
}
