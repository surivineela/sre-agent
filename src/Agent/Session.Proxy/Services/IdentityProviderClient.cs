using System.Text;
using System.Text.Json;
using Session.Identity.Models;
using Session.Proxy.Configuration;

namespace Session.Proxy.Services;

/// <summary>
/// Client for communicating with the Session.Identity service.
/// </summary>
public class IdentityProviderClient
{
    private readonly ILogger<IdentityProviderClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly IdentityProviderSettings _settings;

    public IdentityProviderClient(
        ILogger<IdentityProviderClient> logger,
        IHttpClientFactory httpClientFactory,
        IdentityProviderSettings settings)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("IdentityProvider");
        _settings = settings;
    }

    /// <summary>
    /// Gets the base URL of the identity provider.
    /// </summary>
    public string BaseUrl => _settings.BaseUrl;

    /// <summary>
    /// Gets the MSI endpoint URL.
    /// </summary>
    public string MsiEndpoint => $"{_settings.BaseUrl}/msi/token";

    /// <summary>
    /// Adds tokens to the identity provider.
    /// </summary>
    public async Task<(bool Success, string? Error)> AddTokensAsync(Dictionary<string, string> tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            return (true, null);
        }

        try
        {
            _logger.LogInformation("Adding {Count} tokens to identity provider", tokens.Count);

            var url = $"{_settings.BaseUrl}/bootstrap/tokens";
            var requestBody = new AddTokensRequest { Tokens = tokens };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to add tokens to identity provider. Status: {StatusCode}", response.StatusCode);
                return (false, errorContent);
            }

            _logger.LogInformation("Successfully added {Count} tokens to identity provider", tokens.Count);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tokens to identity provider");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Adds a managed identity configuration to the identity provider.
    /// </summary>
    public async Task<(bool Success, string? Response, string? Error)> AddManagedIdentityAsync(ManagedIdentityInfo? managedIdentity)
    {
        if (managedIdentity == null)
        {
            return (true, null, null);
        }
        try
        {
            _logger.LogInformation("Adding managed identity to identity provider. Type: {Type}, ClientId: {ClientId}, PrincipalId: {PrincipalId}, AuthenticationEndpoint: {AuthenticationEndpoint}",
                managedIdentity.Type, managedIdentity.ClientId, managedIdentity.PrincipalId, managedIdentity.AuthenticationEndpoint);

            var request = new ManagedIdentityRequest
            {
                ManagedIdentity = managedIdentity,
            };

            var url = $"{_settings.BaseUrl}/bootstrap/managedIdentity";
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to add managed identity to identity provider. Status: {StatusCode}", response.StatusCode);
                return (false, null, responseContent);
            }

            _logger.LogInformation("Managed identity added successfully");
            return (true, responseContent, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding managed identity to identity provider");
            return (false, null, ex.Message);
        }
    }
}
