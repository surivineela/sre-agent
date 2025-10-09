// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

public class FailedToGetAccessTokenException : Exception
{
    public FailedToGetAccessTokenException() : base("Failed to get access token. Please run 'az login' first.") { }
}

public class AuthenticationHandler : DelegatingHandler
{
    private readonly ITokenService _tokenService;

    public AuthenticationHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Only add auth for non-localhost requests
        if (request.RequestUri != null && !CliConfigurationService.IsLocalhost(request.RequestUri.ToString()))
        {
            var token = await _tokenService.GetAccessTokenAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                DebugLogger.LogAuth("No token found, attempting to refresh");

                throw new FailedToGetAccessTokenException();
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            DebugLogger.LogAuth("Added Bearer token to request");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
