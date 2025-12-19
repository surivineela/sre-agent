using Session.Identity.Models;

namespace Session.Identity.Services;

public interface ITokenService
{
    Task<Token?> GetTokenAsync(string resource);
    Task AddTokensAsync(Dictionary<string, string> tokens);
}
