using System;
using Session.Proxy.Models;

namespace Session.Proxy.Services;

public interface ITokenService
{
    public Task<Token?> GetTokenAsync(string resource);
    public Task AddTokensAsync(Dictionary<string, string> tokens);
}
