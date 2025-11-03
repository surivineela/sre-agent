namespace Agent.Portal.Services;

/// <summary>
/// Factory for retrieving token providers by identifier.
/// </summary>
public interface ITokenProviderFactory
{
    /// <summary>
    /// Gets a token provider by its identifier.
    /// </summary>
    /// <param name="identifier">The token provider identifier (e.g., "arm", "graph").</param>
    /// <returns>The token provider instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the identifier is not recognized.</exception>
    ITokenProvider GetProvider(string identifier);
}

/// <summary>
/// Default implementation of the token provider factory.
/// </summary>
public class TokenProviderFactory : ITokenProviderFactory
{
    private readonly IEnumerable<ITokenProvider> _providers;

    public TokenProviderFactory(IEnumerable<ITokenProvider> providers)
    {
        _providers = providers;
    }

    public ITokenProvider GetProvider(string identifier)
    {
        var provider = _providers.FirstOrDefault(p => 
            string.Equals(p.Identifier, identifier, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            throw new ArgumentException($"Unknown token provider identifier: {identifier}", nameof(identifier));
        }

        return provider;
    }
}
