// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading.RateLimiting;

namespace Agent.Core.Services;

/// <summary>
/// Configuration for resource provider specific rate limiting
/// </summary>
public class ResourceProviderRateLimitConfig
{
    /// <summary>
    /// Default rate limit configuration applied to all resource providers
    /// </summary>
    public TokenBucketRateLimiterOptions DefaultOptions { get; set; } = new()
    {
        TokenLimit = 100,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = 100,
        QueueLimit = 200,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true
    };

    /// <summary>
    /// Resource provider specific rate limit overrides
    /// Key: Resource provider name (e.g., "microsoft.compute", "microsoft.storage")
    /// Value: Rate limiter options for that specific provider
    /// </summary>
    public Dictionary<string, TokenBucketRateLimiterOptions> ProviderSpecificOptions { get; set; } = new();

    /// <summary>
    /// Gets the rate limiter options for a specific resource provider
    /// </summary>
    /// <param name="resourceProvider">The resource provider name</param>
    /// <returns>Rate limiter options for the provider, or default if no specific config exists</returns>
    public TokenBucketRateLimiterOptions GetOptionsForProvider(string resourceProvider)
    {
        if (ProviderSpecificOptions.TryGetValue(resourceProvider.ToLowerInvariant(), out var specificOptions))
        {
            return specificOptions;
        }

        return DefaultOptions;
    }
}
