// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

namespace Agent.Core.Extensions
{
    public static class HttpClientConfigurationExtensions
    {
        public static void AddArmHelperHttpClient(this IServiceCollection services)
        {
            services.AddTransient<ArmOperationAccessTokenHandler>();
            services.AddHttpClient(Constants.HttpClientForArmOperation, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
            }).AddHttpMessageHandler<ArmOperationAccessTokenHandler>();
        }

        public static void AddRazorHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient(Constants.HttpClientForRazor, (sp, client) =>
            {
                var httpClientSvc = sp.GetRequiredService<HttpClientService>();
                client.BaseAddress = new Uri(httpClientSvc.BaseUrl);
            });
        }

        public static void AddCrawlerHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient(Constants.HttpClientForCrawler, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
            }).AddHttpMessageHandler(sp =>
            {
                var authSvc = sp.GetRequiredService<IAuthenticationService>();
                var cred = authSvc.GetCrawlerCredential();
                return new CrawlerAccessTokenHandler(cred);
            });
        }

        public static void AddSearchEndpointHttpClient(this IServiceCollection services)
        {
            services.AddTransient<SearchEndpointAccessTokenHandler>();
            services.AddHttpClient(Constants.HttpClientForSearchEndpoint, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
            }).AddHttpMessageHandler<SearchEndpointAccessTokenHandler>();
        }

        public static void AddSessionPoolHttpClient(this IServiceCollection services)
        {
            services.AddTransient<SessionPoolAccessTokenHandler>();
            services.AddHttpClient(Constants.HttpClientForSessionPool, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
            }).AddHttpMessageHandler<SessionPoolAccessTokenHandler>();
        }
    }

    // This handler targets for Agent's arm operation
    // It will decide to use OBO or agent identity for arm operation according to context at runtime
    public class ArmOperationAccessTokenHandler : DelegatingHandler
    {
        private readonly IAuthenticationService _authenticationService;

        public ArmOperationAccessTokenHandler(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Get a fresh new credential as the ArmHelperAccessTokenHandler is not recreated when calling CreateClient()
            var cred = await _authenticationService.GetArmOperationCredential();
            var accessToken = await cred.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken.Token);

            return await base.SendAsync(request, cancellationToken);
        }
    }

    // This handler targets for Agents crawler
    // It caches crawler identity token
    public class CrawlerAccessTokenHandler : DelegatingHandler
    {
        private readonly TokenCache _tokenCache;

        public CrawlerAccessTokenHandler(TokenCredential cred)
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

    public class SearchEndpointAccessTokenHandler : DelegatingHandler
    {
        private readonly IAuthenticationService _authenticationService;

        public SearchEndpointAccessTokenHandler(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var cred = _authenticationService.GetSearchEndpointCredential();
            var accessToken = await cred.GetTokenAsync(
                new TokenRequestContext(new[] { "https://azuresre.ai/.default" }),
                cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken.Token);

            return await base.SendAsync(request, cancellationToken);
        }
    }

    public class SessionPoolAccessTokenHandler : DelegatingHandler
    {
        private readonly IAuthenticationService _authenticationService;

        public SessionPoolAccessTokenHandler(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var cred = _authenticationService.GetSessionPoolCredential();
            var accessToken = await cred.GetTokenAsync(
                new TokenRequestContext(new[] { "https://dynamicsessions.io/.default" }),
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
