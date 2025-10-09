// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Agent.Cli.Helpers;
using Azure.Core;
using Azure.Identity;

namespace Agent.Cli.Services;

public interface ITokenService
{
    Task<string?> GetAccessTokenAsync();
}

public class TokenService : ITokenService
{
    private static readonly string[] Scopes = { "https://azuresre.dev/.default" };

    public async Task<string?> GetAccessTokenAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            DebugLogger.LogAuth("Attempting to get Azure CLI credentials");
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(new TokenRequestContext(Scopes));

            stopwatch.Stop();
            DebugLogger.LogAuth($"Successfully obtained access token (expires: {token.ExpiresOn})");
            DebugLogger.LogTiming("GetAccessToken", stopwatch.Elapsed);

            return token.Token;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DebugLogger.LogAuth($"Failed to get access token: {ex.Message}");
            DebugLogger.LogTiming("GetAccessToken (failed)", stopwatch.Elapsed);
            return null;
        }
    }
}
