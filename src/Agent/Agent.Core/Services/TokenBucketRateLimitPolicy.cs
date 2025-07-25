// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// HTTP policy that implements token bucket rate limiting for Azure ARM operations per resource provider
/// </summary>
public class TokenBucketRateLimitPolicy : HttpPipelinePolicy, IDisposable
{
    private readonly ResourceProviderRateLimitConfig _config;
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _rateLimiters;
    private readonly Regex _resourceProviderRegex;
    private readonly ILogger<ArmClientFactory>? _logger;
    private bool _disposed;

    public TokenBucketRateLimitPolicy(TokenBucketRateLimiterOptions options, ILogger<ArmClientFactory>? logger)
        : this(new ResourceProviderRateLimitConfig { DefaultOptions = options }, logger)
    {
    }

    public TokenBucketRateLimitPolicy(ResourceProviderRateLimitConfig config, ILogger<ArmClientFactory>? logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rateLimiters = new ConcurrentDictionary<string, TokenBucketRateLimiter>();
        _logger = logger;

        // Regex to extract resource provider from ARM URLs, patterns:
        // resource group: /subscriptions/{subscriptionId}?api-version=2022-12-01 or /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}
        // default: /tenants
        // default: /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}?api-version=2022-09-01
        // app: /subscriptions/{subscriptionId}/providers/microsoft.app?api-version=2022-09-01
        // network: /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/microsoft.network/networksecuritygroups
        _resourceProviderRegex = new Regex(@"(?i)providers/([^/?]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TokenBucketRateLimitPolicy));
        }

        // Extract resource provider from the request URL
        var resourceProvider = ExtractResourceProvider(message);

        try
        {
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
                _logger?.LogWarning("Rate limit exceeded for resource provider '{Provider}'. Request '{Uri}' was throttled",
                    resourceProvider, message.Request.Uri);

                throw new RequestFailedException(
                    (int)HttpStatusCode.TooManyRequests,
                    $"Rate limit exceeded for resource provider '{resourceProvider}'. Request '{message.Request.Uri}' was throttled by token bucket rate limiter.",
                    "TokenBucketRateLimit", null);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Rate limit acquisition cancelled for '{Provider}'", resourceProvider);
            throw;
        }
        catch (Exception ex) when (ex is not RequestFailedException)
        {
            _logger?.LogError(ex, "Unexpected error during rate limiting for '{Provider}'", resourceProvider);
            throw;
        }
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        // For sync operations, we should avoid blocking async calls
        // Consider using a synchronous rate limiter or queue the operation
        _logger?.LogWarning("Synchronous HTTP pipeline processing is not recommended with async rate limiting");

        // Fallback to async processing to avoid deadlocks
        ProcessAsync(message, pipeline).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _logger?.LogDebug("Disposing TokenBucketRateLimitPolicy and {Count} rate limiters", _rateLimiters.Count);

            foreach (var rateLimiter in _rateLimiters.Values)
            {
                try
                {
                    rateLimiter?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing rate limiter");
                }
            }

            _rateLimiters.Clear();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
