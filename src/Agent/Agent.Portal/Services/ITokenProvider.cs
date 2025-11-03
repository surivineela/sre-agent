namespace Agent.Portal.Services;

/// <summary>
/// Interface for acquiring access tokens for different Azure services.
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Gets the identifier for this token provider (e.g., "arm", "graph").
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Gets the scope(s) required for this token.
    /// </summary>
    string[] Scopes { get; }

    /// <summary>
    /// Acquires an access token for the configured scope.
    /// </summary>
    /// <returns>The access token response.</returns>
    Task<TokenResponse> GetTokenAsync();
}

/// <summary>
/// Response containing the access token and metadata.
/// </summary>
public class TokenResponse
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required string Scope { get; init; }
}
