// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Azure.Core;
using Azure.ResourceManager;
using System.Threading.RateLimiting;

namespace Agent.Core.Services;

public class ArmClientFactory : IArmClientFactory
{
    private readonly IAuthenticationService _authService;

    private Lazy<ArmClient> _crawlerClient;

    public ArmClientFactory(IAuthenticationService authService)
    {
        _authService = authService;

        _crawlerClient = new Lazy<ArmClient>(() => ConstructArmClient(_authService.GetCrawlerCredential()));
    }

    public async Task<ArmClient> GetArmOperationClient()
    {
        var cred = await _authService.GetArmOperationCredential();

        return ConstructArmClient(cred);
    }

    public ArmClient GetCrawlerArmClient()
    {
        return _crawlerClient.Value;
    }

    public async Task<ArmClient> GetArmOperationClientWithRetry(ArmRetryOptions retryOptions)
    {
        var cred = await _authService.GetArmOperationCredential();

        return ConstructArmClient(cred, retryOptions);
    }

    public ArmClient GetCrawlerArmClientWithRetry(ArmRetryOptions retryOptions)
    {
        return ConstructArmClient(_authService.GetCrawlerCredential(), retryOptions);
    }

    private ArmClient ConstructArmClient(TokenCredential cred)
    {
        // Use default retry options for existing APIs
        var defaultRetryOptions = new ArmRetryOptions();
        return ConstructArmClient(cred, defaultRetryOptions);
    }

    private ArmClient ConstructArmClient(TokenCredential cred, ArmRetryOptions retryOptions)
    {
        var options = new ArmClientOptions
        {
            Diagnostics =
            {
#if DEBUG
                // log request and response content
                IsLoggingContentEnabled = true,
                // don't redact any headers for debugging
                LoggedHeaderNames = {"*"},
                LoggedQueryParameters = {"*"}, 

#else
                IsLoggingContentEnabled = false,
#endif
                IsLoggingEnabled = true,
            },
            Retry =
            {
                // Configure retry policy for handling 429 (rate limiting) and other transient failures
                Mode = retryOptions.Mode,
                MaxRetries = retryOptions.MaxRetries,
                Delay = retryOptions.Delay,
                MaxDelay = retryOptions.MaxDelay,
                NetworkTimeout = retryOptions.NetworkTimeout
            }
        };

        // Add the token bucket rate limiting policy with default configuration
        // This will create separate rate limiters for each Azure resource provider
        // 
        // Pipeline execution order with PerCall positioning:
        // 1. Retry Policy (outer wrapper - catches all failures)
        // 2. Rate Limiter Policy (applied per call, not per retry)
        // 3. Request sent to Azure ARM API
        //
        // Benefits:
        // - If rate limiter rejects request, retry policy can retry after delay
        // - If Azure returns 429, retry policy handles it with exponential backoff
        // - Rate limiter queue provides first-level protection
        // - Retry policy provides second-level resilience
        var rateLimiterOptions = new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,          // Allow burst of 10 requests
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 10,     // 10 requests per second per resource provider
            QueueLimit = 20,          // Queue up to 20 requests when rate limit is hit
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };

        var rateLimitPolicy = new TokenBucketRateLimitPolicy(rateLimiterOptions);
        options.AddPolicy(rateLimitPolicy, HttpPipelinePosition.PerCall);

        return new ArmClient(cred, default, options);
    }
}

