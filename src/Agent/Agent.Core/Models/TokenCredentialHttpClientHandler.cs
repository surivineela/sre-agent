using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;

namespace Agent.Core.Models;
public class TokenCredentialHttpClientHandler : HttpClientHandler
{
    private readonly TokenCredential _credential;
    private readonly string _scope;

    public TokenCredentialHttpClientHandler(TokenCredential credential, string scope)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokenRequestContext = new TokenRequestContext(new[] { _scope + "/.default" });
        var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
