using Session.Identity.Models;

namespace Session.Identity.Services;

/// <summary>
/// Composite token service that first tries to get a token from static tokens,
/// then falls back to managed identity token service.
/// </summary>
public class CompositeTokenService : ITokenService
{
    private readonly ILogger<CompositeTokenService> _logger;
    private readonly StaticTokenService _staticTokenService;
    private readonly ManagedIdentityTokenService _managedIdentityTokenService;

    public CompositeTokenService(
        ILogger<CompositeTokenService> logger,
        StaticTokenService staticTokenService,
        ManagedIdentityTokenService managedIdentityTokenService)
    {
        _logger = logger;
        _staticTokenService = staticTokenService;
        _managedIdentityTokenService = managedIdentityTokenService;
    }

    public async Task<Token?> GetTokenAsync(string resource)
    {
        _logger.LogInformation("Getting token for resource: {Resource}", resource);

        // First, try to get from static tokens
        var token = await _staticTokenService.GetTokenAsync(resource);
        if (token != null)
        {
            _logger.LogInformation("Found static token for resource: {Resource}", resource);
            return token;
        }

        // Fall back to managed identity
        _logger.LogInformation("No static token found for resource: {Resource}. Falling back to managed identity.", resource);
        token = await _managedIdentityTokenService.GetTokenAsync(resource);
        if (token != null)
        {
            _logger.LogInformation("Acquired token from managed identity for resource: {Resource}", resource);
            return token;
        }

        _logger.LogWarning("No token available for resource: {Resource}", resource);
        return null;
    }

    public async Task AddTokensAsync(Dictionary<string, string> tokens)
    {
        // Add tokens to the static token service
        await _staticTokenService.AddTokensAsync(tokens);
    }
}
