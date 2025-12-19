using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Session.Identity.Models;

namespace Session.Identity.Services;

/// <summary>
/// Token service that stores tokens statically in memory.
/// </summary>
public class StaticTokenService : ITokenService
{
    private readonly ILogger<StaticTokenService> _logger;
    private readonly ConcurrentDictionary<string, Token> _tokens;

    public StaticTokenService(ILogger<StaticTokenService> logger)
    {
        _logger = logger;
        _tokens = new ConcurrentDictionary<string, Token>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<Token?> GetTokenAsync(string resource)
    {
        _logger.LogInformation("Getting token for resource: {Resource}", resource);

        if (_tokens.TryGetValue(resource, out var token))
        {
            return Task.FromResult<Token?>(token);
        }

        foreach (var (key, value) in _tokens)
        {
            if (key.StartsWith(resource, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Found token for resource: {Resource} using prefix match: {Key}", resource, key);
                return Task.FromResult<Token?>(value);
            }
        }

        _logger.LogDebug("No static token found for resource: {Resource}", resource);
        return Task.FromResult<Token?>(null);
    }

    public Task AddTokensAsync(Dictionary<string, string> tokens)
    {
        foreach (var (resource, raw) in tokens)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(raw);
                _tokens[resource] = new Token
                {
                    JwtToken = jwtToken,
                    Raw = raw
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse token for resource: {Resource}", resource);
            }
        }

        _logger.LogInformation("Added {Count} tokens. Resources: {Resources}", tokens.Count, string.Join(", ", _tokens.Keys));
        return Task.CompletedTask;
    }
}
