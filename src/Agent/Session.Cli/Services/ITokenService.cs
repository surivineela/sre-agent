using System;
using Session.Cli.Models;

namespace Session.Cli.Services;

public interface ITokenService
{
    public Task<Token?> GetTokenAsync(string resource);
    public Task AddTokensAsync(Dictionary<string, string> tokens);
}
