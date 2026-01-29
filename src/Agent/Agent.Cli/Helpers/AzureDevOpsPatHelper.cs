// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for generating Azure DevOps Personal Access Tokens using Azure CLI.
/// </summary>
public static partial class AzureDevOpsPatHelper
{
    /// <summary>
    /// Represents the scope of a PAT token.
    /// </summary>
    public enum PatScope
    {
        /// <summary>
        /// Read-only access to code repositories.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// Read and write access to code repositories.
        /// </summary>
        ReadWrite
    }

    /// <summary>
    /// Result of a PAT generation operation.
    /// </summary>
    public record PatGenerationResult(bool Success, string? Token, string? ErrorMessage);

    /// <summary>
    /// Parses an Azure DevOps URL to extract organization, project, and repository information.
    /// Supports both formats:
    /// - https://dev.azure.com/{org}/{project}/_git/{repo}
    /// - https://{org}.visualstudio.com/{project}/_git/{repo}
    /// </summary>
    public record AzureDevOpsUrlInfo(string Organization, string Project, string Repository, string OrganizationUrl);

    /// <summary>
    /// Parses an Azure DevOps URL to extract organization, project, and repository.
    /// </summary>
    /// <param name="url">The Azure DevOps repository URL.</param>
    /// <returns>The parsed URL info, or null if the URL is invalid.</returns>
    public static AzureDevOpsUrlInfo? ParseAzureDevOpsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            var uri = new Uri(url);

            // New format: https://dev.azure.com/{org}/{project}/_git/{repo}
            if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 4 && segments[2].Equals("_git", StringComparison.OrdinalIgnoreCase))
                {
                    var org = segments[0];
                    var project = segments[1];
                    var repo = segments[3];
                    return new AzureDevOpsUrlInfo(org, project, repo, $"https://dev.azure.com/{org}");
                }
            }
            // Legacy format: https://{org}.visualstudio.com/{project}/_git/{repo}
            else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                var org = uri.Host.Replace(".visualstudio.com", "", StringComparison.OrdinalIgnoreCase);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3 && segments[1].Equals("_git", StringComparison.OrdinalIgnoreCase))
                {
                    var project = segments[0];
                    var repo = segments[2];
                    return new AzureDevOpsUrlInfo(org, project, repo, $"https://dev.azure.com/{org}");
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that a URL is a valid Azure DevOps repository URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is valid, false otherwise.</returns>
    public static bool IsValidAzureDevOpsUrl(string url)
    {
        return ParseAzureDevOpsUrl(url) != null;
    }

    /// <summary>
    /// Azure DevOps resource ID for getting access tokens.
    /// </summary>
    private const string AzureDevOpsResourceId = "499b84ac-1321-427f-aa17-267ca6975798";

    /// <summary>
    /// Generates a PAT token using Azure CLI to get an access token, then calls the Azure DevOps REST API.
    /// </summary>
    /// <param name="organizationUrl">The Azure DevOps organization URL (e.g., https://dev.azure.com/myorg).</param>
    /// <param name="repoName">The repository name (used for naming the PAT).</param>
    /// <param name="scope">The scope of the PAT (ReadOnly or ReadWrite).</param>
    /// <param name="validDays">Number of days the PAT should be valid (default: 90).</param>
    /// <returns>The result of the PAT generation.</returns>
    public static async Task<PatGenerationResult> GeneratePatAsync(
        string organizationUrl,
        string repoName,
        PatScope scope = PatScope.ReadWrite,
        int validDays = 90)
    {
        try
        {
            // Check if az CLI is available
            var azCheckResult = await ExecuteCommandAsync("az", "--version");
            if (azCheckResult.ExitCode != 0)
            {
                return new PatGenerationResult(false, null, "Azure CLI (az) is not installed or not in PATH. Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli");
            }

            // Check if user is logged in
            var accountResult = await ExecuteCommandAsync("az", "account show");
            if (accountResult.ExitCode != 0)
            {
                return new PatGenerationResult(false, null, "Not logged in to Azure CLI. Please run 'az login' first.");
            }

            // Get access token for Azure DevOps
            DebugLogger.Debug("PAT", "Getting access token for Azure DevOps...");
            var tokenResult = await ExecuteCommandAsync("az", $"account get-access-token --resource {AzureDevOpsResourceId} --output json");

            if (tokenResult.ExitCode != 0)
            {
                var errorMsg = !string.IsNullOrWhiteSpace(tokenResult.Error) ? tokenResult.Error : tokenResult.Output;
                return new PatGenerationResult(false, null, $"Failed to get access token: {errorMsg}");
            }

            // Parse the access token
            string accessToken;
            try
            {
                var tokenJson = JsonDocument.Parse(tokenResult.Output);
                accessToken = tokenJson.RootElement.GetProperty("accessToken").GetString()!;
            }
            catch (Exception ex)
            {
                return new PatGenerationResult(false, null, $"Failed to parse access token: {ex.Message}");
            }

            // Extract organization name from URL
            var orgName = ExtractOrganizationName(organizationUrl);
            if (string.IsNullOrEmpty(orgName))
            {
                return new PatGenerationResult(false, null, "Could not extract organization name from URL");
            }

            // Build the PAT name
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var patName = $"srectl-{SanitizeName(repoName)}-{timestamp}";

            // Determine scope - vso.code = read-only, vso.code_write = read-write
            var scopeString = scope == PatScope.ReadOnly ? "vso.code" : "vso.code_write";

            // Calculate expiration date (ISO 8601 format)
            var validTo = DateTime.UtcNow.AddDays(validDays).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            // Call Azure DevOps REST API to create PAT
            var apiUrl = $"https://vssps.dev.azure.com/{orgName}/_apis/tokens/pats?api-version=7.1-preview.1";

            var requestBody = new
            {
                displayName = patName,
                scope = scopeString,
                validTo = validTo,
                allOrgs = false
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);

            DebugLogger.Debug("PAT", $"Creating PAT via REST API: {apiUrl}");
            DebugLogger.Debug("PAT", $"Request body: {jsonBody}");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            DebugLogger.Debug("PAT", $"Response status: {response.StatusCode}");
            DebugLogger.Debug("PAT", $"Response body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                return new PatGenerationResult(false, null, $"Failed to create PAT: {response.StatusCode} - {responseBody}");
            }

            // Parse the response to extract the token
            try
            {
                var jsonDoc = JsonDocument.Parse(responseBody);

                // The token is in patToken.token
                if (jsonDoc.RootElement.TryGetProperty("patToken", out var patTokenElement))
                {
                    if (patTokenElement.TryGetProperty("token", out var tokenElement))
                    {
                        var token = tokenElement.GetString();
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            return new PatGenerationResult(true, token, null);
                        }
                    }
                }

                return new PatGenerationResult(false, null, "PAT was created but token was not found in response. Please check Azure DevOps portal for the token.");
            }
            catch (JsonException ex)
            {
                return new PatGenerationResult(false, null, $"Failed to parse PAT response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new PatGenerationResult(false, null, $"Failed to generate PAT: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the organization name from an Azure DevOps URL.
    /// </summary>
    private static string? ExtractOrganizationName(string organizationUrl)
    {
        try
        {
            var uri = new Uri(organizationUrl);

            // https://dev.azure.com/{org}
            if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                return segments.Length > 0 ? segments[0] : null;
            }

            // https://{org}.visualstudio.com
            if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                return uri.Host.Replace(".visualstudio.com", "", StringComparison.OrdinalIgnoreCase);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Prompts the user to select a PAT scope.
    /// </summary>
    /// <returns>The selected scope.</returns>
    public static PatScope PromptForScope()
    {
        ConsoleUI.WriteInline("Generate PAT with scope (rw=read-write, ro=read-only) [rw]: ", ConsoleColor.Yellow);
        var input = (ConsoleUI.ReadLineHandler?.Invoke() ?? Console.ReadLine())?.Trim().ToLowerInvariant();

        return input switch
        {
            "ro" => PatScope.ReadOnly,
            "r" => PatScope.ReadOnly,
            "read-only" => PatScope.ReadOnly,
            "readonly" => PatScope.ReadOnly,
            _ => PatScope.ReadWrite // Default to read-write
        };
    }

    /// <summary>
    /// Sanitizes a name for use in PAT naming.
    /// </summary>
    private static string SanitizeName(string name)
    {
        // Replace invalid characters with dashes
        var sanitized = InvalidCharsRegex().Replace(name, "-");
        // Remove consecutive dashes
        sanitized = ConsecutiveDashesRegex().Replace(sanitized, "-");
        // Trim dashes from start and end
        return sanitized.Trim('-');
    }

    [GeneratedRegex("[^a-zA-Z0-9-]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex("-+")]
    private static partial Regex ConsecutiveDashesRegex();

    /// <summary>
    /// Executes a command and returns the result.
    /// On Windows, uses cmd.exe to properly resolve commands like 'az' which are batch scripts.
    /// </summary>
    private static async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            string fileName;
            string fullArguments;

            // On Windows, az is actually az.cmd, so we need to run it through cmd.exe
            if (OperatingSystem.IsWindows())
            {
                fileName = "cmd.exe";
                fullArguments = $"/c {command} {arguments}";
            }
            else
            {
                fileName = command;
                fullArguments = arguments;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = fullArguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return (-1, string.Empty, "Failed to start process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, $"Exception executing command: {ex.Message}");
        }
    }
}
