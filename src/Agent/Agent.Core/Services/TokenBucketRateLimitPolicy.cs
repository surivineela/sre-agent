// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

namespace Agent.Core.Services;

/// <summary>
/// HTTP policy that implements token bucket rate limiting for Azure ARM operations per resource provider
/// </summary>
public class TokenBucketRateLimitPolicy : HttpPipelinePolicy
{
    private readonly ResourceProviderRateLimitConfig _config;
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _rateLimiters;
    private readonly Regex _resourceProviderRegex;

    public TokenBucketRateLimitPolicy(TokenBucketRateLimiterOptions options)
        : this(new ResourceProviderRateLimitConfig { DefaultOptions = options })
    {
    }

    public TokenBucketRateLimitPolicy(ResourceProviderRateLimitConfig config)
    {
        _config = config;
        _rateLimiters = new ConcurrentDictionary<string, TokenBucketRateLimiter>();

        // Regex to extract resource provider from ARM URLs, patterns:
        // resource group: /subscriptions/{subscriptionId}?api-version=2022-12-01 or /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}
        // network: /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/microsoft.network/networksecuritygroups
        _resourceProviderRegex = new Regex(@"/providers/([^/]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private string ExtractResourceProvider(HttpMessage message)
    {
        var uri = message.Request.Uri.ToString();
        var match = _resourceProviderRegex.Match(uri);

        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant();
        }

        // Fallback to a default bucket for non-provider specific operations
        return "default";
    }

    private TokenBucketRateLimiter GetRateLimiterForResourceProvider(string resourceProvider)
    {
        return _rateLimiters.GetOrAdd(resourceProvider, rp =>
        {
            var options = _config.GetOptionsForProvider(rp);
            return new TokenBucketRateLimiter(options);
        });
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        // Extract resource provider from the request URL
        var resourceProvider = ExtractResourceProvider(message);
        var rateLimiter = GetRateLimiterForResourceProvider(resourceProvider);

        // Try to acquire a permit from the appropriate token bucket
        using var lease = await rateLimiter.AcquireAsync(permitCount: 1, message.CancellationToken);

        if (lease.IsAcquired)
        {
            // Permit acquired, proceed with the request
            await ProcessNextAsync(message, pipeline);
        }
        else
        {
            // Rate limit exceeded, throw a request failed exception
            throw new RequestFailedException(429, $"Rate limit exceeded for resource provider '{resourceProvider}'. Request '{message.Request.Uri}' was throttled by token bucket rate limiter.",
                "TokenBucketRateLimit", null);
        }
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        // Extract resource provider from the request URL
        var resourceProvider = ExtractResourceProvider(message);
        var rateLimiter = GetRateLimiterForResourceProvider(resourceProvider);

        // For synchronous calls, we'll use a simple blocking acquire
        using var lease = rateLimiter.AcquireAsync(permitCount: 1, message.CancellationToken).AsTask().GetAwaiter().GetResult();

        if (lease.IsAcquired)
        {
            // Permit acquired, proceed with the request
            ProcessNext(message, pipeline);
        }
        else
        {
            // Rate limit exceeded, throw a request failed exception
            throw new RequestFailedException(429, $"Rate limit exceeded for resource provider '{resourceProvider}'. Request '{message.Request.Uri}' was throttled by token bucket rate limiter.",
                "TokenBucketRateLimit", null);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var rateLimiter in _rateLimiters.Values)
            {
                rateLimiter?.Dispose();
            }
            _rateLimiters.Clear();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
