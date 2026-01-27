using Anthropic.Foundry;
using Azure.Core;

namespace Agent.Runtime;

/// <summary>
/// An implementation of <see cref="IAnthropicFoundryCredentials"/> that uses <see cref="TokenCredential"/>
/// for authentication with token refresh support.
/// </summary>
/// <remarks>
/// The official SDK's <see cref="Anthropic.Foundry.AnthropicFoundryIdentityTokenCredentials"/> takes a static
/// <see cref="AccessToken"/> and doesn't support token refresh. This implementation wraps a <see cref="TokenCredential"/>
/// to enable automatic token refresh.
///
/// Note: The <see cref="IAnthropicFoundryCredentials.Apply"/> method is synchronous, so we use
/// <see cref="TokenCredential.GetToken"/> instead of <see cref="TokenCredential.GetTokenAsync"/>.
/// The underlying credential implementations typically cache tokens and handle refresh internally.
/// </remarks>
public class AnthropicFoundryTokenCredentials : IAnthropicFoundryCredentials
{
    private readonly TokenCredential _credential;
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    /// <summary>
    /// Creates a new instance of the <see cref="AnthropicFoundryTokenCredentials"/> class.
    /// </summary>
    /// <param name="credential">The <see cref="TokenCredential"/> to use for authentication.</param>
    /// <param name="resourceName">The Azure AI Services resource name.</param>
    public AnthropicFoundryTokenCredentials(TokenCredential credential, string resourceName)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        ResourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
    }

    /// <inheritdoc/>
    public string ResourceName { get; }

    /// <inheritdoc/>
    public void Apply(HttpRequestMessage requestMessage)
    {
        var tokenRequestContext = new TokenRequestContext(Scopes);
        var accessToken = _credential.GetToken(tokenRequestContext, default);
        requestMessage.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
    }
}
