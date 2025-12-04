using Anthropic.Core;
using Azure.Core;

namespace Agent.Runtime;
using AnthropicClientOptions = Anthropic.Core.ClientOptions;

/// <summary>
/// An Anthropic client that uses TokenCredential for authentication.
/// The only reason this class exists is that the offical Anthropic SDK does not support token refresh.
/// https://github.com/anthropics/anthropic-sdk-csharp/blob/v11.0.0/src/Anthropic.Foundry/AnthropicFoundryIdentityTokenCredentials.cs#L34
///
/// Retire this class when the official SDK supports token refresh.
/// </summary>
public class AnthropicTokenCredentialClient : Anthropic.AnthropicClient
{
    private readonly TokenCredential _credential;

    public AnthropicTokenCredentialClient(TokenCredential credential, AnthropicClientOptions options)
        : base(options)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }


    public override Anthropic.IAnthropicClient WithOptions(Func<AnthropicClientOptions, AnthropicClientOptions> modifier)
    {
        return new AnthropicTokenCredentialClient(_credential, modifier(_options));
    }

    protected override async ValueTask BeforeSend<T>(
        HttpRequest<T> request,
        HttpRequestMessage requestMessage,
        CancellationToken cancellationToken
    )
    {
        var tokenRequestContext = new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" });
        var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
    }
}
