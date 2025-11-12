using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Cloud.Metrics.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins;

/// <summary>
/// Managed Identity Access Token Provider implementing IAccessTokenProvider from Microsoft.Cloud.Metrics.Client.
/// Follows the pattern from the managed identity authentication documentation.
/// </summary>
internal sealed class MdmMetricsAccessTokenProvider : IAccessTokenProvider
{
    private readonly MdmMetricsSettings _settings;
    private readonly ILogger _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IHostEnvironment _hostEnvironment;
    private AccessToken _accessToken;
    private readonly TokenCredential? _credential;
    private readonly bool _useUserToken;
    private readonly string? _tokenEndpoint;
    private string? _cachedUserToken;
    private DateTimeOffset _userTokenExpiration;

    public MdmMetricsAccessTokenProvider(
        MdmMetricsSettings settings,
        ILogger logger,
        IAuthenticationService authenticationService,
        IHostEnvironment hostEnvironment)
    {
        _settings = settings;
        _logger = logger;
        _authenticationService = authenticationService;
        _hostEnvironment = hostEnvironment;

        // Check if UseUserToken is enabled (for local development with web app endpoint)
        _useUserToken = !string.IsNullOrWhiteSpace(_settings.TokenEndpoint) && !string.IsNullOrWhiteSpace(_settings.ClientId);
        _tokenEndpoint = _settings.TokenEndpoint;

        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInternalInformation($"Using user token via Azure Default Credential and token endpoint: {_tokenEndpoint} with ClientId: {_settings.ClientId}");
        }
        else
        {
            _credential = _authenticationService.GetMdmMetricsCredential();
            if (_credential == null)
            {
                throw new InvalidOperationException("Failed to obtain TokenCredential for MDM access from AuthenticationService.");
            }
            var identityType = _hostEnvironment.IsDevelopment() ? "DefaultAzureCredential" : "Action Identity";
        }
    }

    /// <summary>
    /// Acquires an access token for the specified resource.
    /// Uses web app endpoint method if UseUserToken is true (local dev), otherwise uses managed identity via AuthenticationService.
    /// </summary>
    public async Task<string> AcquireTokenAsync(string resource)
    {
        // Use web app endpoint method for local development when UseUserToken is enabled
        if (_credential != null)
        {
            _accessToken = await _credential!.GetTokenAsync(
            new TokenRequestContext(new[] { resource }),
            CancellationToken.None).ConfigureAwait(false);
            return _accessToken.Token;
        }
        else // _credential is null only when _hostEnvironment.IsDevelopment() is true
        {
            if (_useUserToken)
            {
                return await AcquireDevUserTokenAsync().ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException("TokenEndpoint and ClientId must be provided in MdmMetricsSettings for local dev environment to use MdmMetricTools.");
            }
        }
    }

    #region local dev user token acquisition logic, won't be used in production

    /// <summary>
    /// Acquires a user token by first getting Azure management token via DefaultAzureCredential,
    /// then calling the web app command endpoint to retrieve the MDM token.
    /// Caches the token for 45 minutes.
    ///
    /// Only for local development/testing purpose
    /// </summary>
    private async Task<string> AcquireDevUserTokenAsync()
    {
        // Check if we have a cached token that's still valid
        if (!string.IsNullOrWhiteSpace(_cachedUserToken) && DateTimeOffset.UtcNow < _userTokenExpiration)
        {
            _logger.LogInternalInformation($"Using cached user token (expires at {_userTokenExpiration}).");
            return _cachedUserToken;
        }

#pragma warning disable CUSTOM003 // This is only used in development/testing scenarios when UseUserToken is true
        var credential = new DefaultAzureCredential(); // CodeQL [SM05137] Development-only code path
#pragma warning restore CUSTOM003
        var managementToken = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://management.core.windows.net/" }),
            CancellationToken.None).ConfigureAwait(false);

        _logger.LogInternalInformation("Obtained Azure management token, calling web app SCM endpoint.");

        using var httpClient = new HttpClient();
        var psCommand = BuildPowerShellCommand(_settings.ClientId!);
        var commandPayload = new { command = psCommand, dir = "site\\repository" };

        var content = new StringContent(
            JsonSerializer.Serialize(commandPayload),
            Encoding.UTF8,
            "application/json");

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {managementToken.Token}");

        var response = await httpClient.PostAsync(_tokenEndpoint, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var token = await ExtractTokenFromResponse(response).ConfigureAwait(false);
        _cachedUserToken = token;
        _userTokenExpiration = DateTimeOffset.UtcNow.AddMinutes(45);

        _logger.LogInternalInformation($"Successfully acquired and cached MDM token (expires at {_userTokenExpiration}).");
        return token;
    }

    /// <summary>
    /// Builds the PowerShell command to fetch MDM token from the web app's managed identity.
    /// </summary>
    private static string BuildPowerShellCommand(string clientId)
    {
        return $"powershell -NoProfile -Command \"" +
               $"$resourceURI='https://microsoftmetrics.com'; " +
               $"$client_id='{clientId}'; " +
               $"$tokenAuthURI=$env:IDENTITY_ENDPOINT+'?resource='+$resourceURI+'&client_id='+$client_id+'&api-version=2019-08-01'; " +
               $"$tokenResponse=Invoke-RestMethod -Method Get -Headers @{{ 'X-IDENTITY-HEADER' = $env:IDENTITY_HEADER }} -Uri $tokenAuthURI; " +
               $"$accessToken=$tokenResponse.access_token; " +
               $"echo $accessToken\"";
    }

    /// <summary>
    /// Extracts the access token from the web app SCM endpoint response.
    /// </summary>
    private async Task<string> ExtractTokenFromResponse(HttpResponseMessage response)
    {
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        using var jsonDoc = JsonDocument.Parse(responseBody);
        if (!jsonDoc.RootElement.TryGetProperty("Output", out var outputElement))
        {
            throw new InvalidOperationException("Token endpoint response does not contain 'Output' field.");
        }

        var token = outputElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Token endpoint returned empty Output field.");
        }

        return token;
    }

    public TimeSpan FallbackTokenRefreshInterval
    {
        get
        {
            if (_accessToken.ExpiresOn != default)
            {
                return TimeSpan.FromSeconds(0.8 * (_accessToken.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds);
            }

            return TimeSpan.FromMinutes(60);
        }
    }
    #endregion local dev user token acquisition logic, won't be used in production
}
