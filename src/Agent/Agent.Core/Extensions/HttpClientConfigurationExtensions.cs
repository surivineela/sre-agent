// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Core.Extensions
{
    public static class HttpClientConfigurationExtensions
    {
        public static void AddArmHelperHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient(nameof(ArmHelper)).AddHttpMessageHandler(sp =>
            {
                var authSvc = sp.GetRequiredService<IAuthenticationService>();
                var cred = authSvc.GetArmOperationCredential();

                return new ArmHelperAccessTokenHandler(cred);
            });
        }

        public static void AddRazorHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient("Razor", (sp, client) =>
            {
                var httpClientSvc = sp.GetRequiredService<HttpClientService>();
                client.BaseAddress = new Uri(httpClientSvc.BaseUrl);
            });
        }
    }

    public class ArmHelperAccessTokenHandler : DelegatingHandler
    {
        private readonly TokenCache _tokenCache;

        public ArmHelperAccessTokenHandler(TokenCredential cred)
        {
            _tokenCache = new TokenCache(cred);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var accessToken = await _tokenCache.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken.Token);

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Caches token for TokenCredential
    /// </summary>
    public class TokenCache
    {
        private class CachedAccessToken
        {
            public string Token { get; }
            public DateTimeOffset ExpiresOn { get; }

            public CachedAccessToken(AccessToken accessToken)
            {
                Token = accessToken.Token;
                ExpiresOn = accessToken.ExpiresOn;
            }

            public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiresOn.AddMinutes(-5);
        }

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, CachedAccessToken> _cache = new Dictionary<string, CachedAccessToken>();
        private readonly TokenCredential _credential;

        public TokenCache(TokenCredential credential)
        {
            _credential = credential;
        }

        public async Task<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            string cacheKey = GetCacheKey(requestContext);

            if (_cache.TryGetValue(cacheKey, out var cachedToken) && !cachedToken.IsExpired())
            {
                return new AccessToken(cachedToken.Token, cachedToken.ExpiresOn);
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedToken) && !cachedToken.IsExpired())
                {
                    return new AccessToken(cachedToken.Token, cachedToken.ExpiresOn);
                }

                var newToken = await _credential.GetTokenAsync(requestContext, cancellationToken);
                _cache[cacheKey] = new CachedAccessToken(newToken);

                return newToken;
            }
            finally
            {
                _lock.Release();
            }
        }

        private string GetCacheKey(TokenRequestContext context)
        {
            return string.Join("|", context.Scopes);
        }
    }
}

