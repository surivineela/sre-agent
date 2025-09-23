using System;
using System.IdentityModel.Tokens.Jwt;
using Session.Cli.Models;

namespace Session.Cli.Services;

public class StaticTokenService : ITokenService
{
    private readonly ILogger<StaticTokenService> _logger;
    private readonly IDictionary<string, Token> _tokens;

    public StaticTokenService(ILogger<StaticTokenService> logger)
    {
        _logger = logger;

        _tokens = new Dictionary<string, Token>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<Token?> GetTokenAsync(string resource)
    {
        _logger.LogInformation($"Getting token for resource: {resource}");
        if (_tokens.TryGetValue(resource, out var token))
        {
            return Task.FromResult<Token?>(token);
        }

        foreach (var (key, value) in _tokens)
        {
            if (key.StartsWith(resource, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation($"Found token for resource: {resource} using prefix match: {key}");
                return Task.FromResult<Token?>(value);
            }
        }

        _logger.LogInformation($"No token found for resource: {resource}.");
        return Task.FromResult<Token?>(null);
    }

    public Task AddTokensAsync(Dictionary<string, string> tokens)
    {
        foreach (var (resource, raw) in tokens)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(raw);
            _tokens[resource] = new Token
            {
                JwtToken = jwtToken,
                Raw = raw
            };
        }

        _logger.LogInformation($"Added {tokens.Count} tokens. New resources: {string.Join(", ", _tokens.Keys)}");

        return Task.CompletedTask;
    }
}
