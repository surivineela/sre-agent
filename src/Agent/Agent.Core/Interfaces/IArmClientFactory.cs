// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Azure.Core;
using Azure.ResourceManager;

namespace Agent.Core.Interfaces;

/// <summary>
/// Retry configuration options for ARM client
/// </summary>
public class ArmRetryOptions
{
    /// <summary>
    /// Retry mode (Fixed, Exponential). Default: Exponential
    /// </summary>
    public RetryMode Mode { get; set; } = RetryMode.Exponential;

    /// <summary>
    /// Maximum number of retry attempts. Default: 5
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Initial delay between retries. Default: 2 seconds
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay between retries. Default: 1 minute
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Network timeout per request. Default: 2 minutes
    /// </summary>
    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromMinutes(2);
}

public interface IArmClientFactory
{

    /// <summary>
    /// Get Arm client for arm readonly operations
    /// </summary>
    /// <returns></returns>
    public Task<ArmClient> GetArmOperationClient();

    /// <summary>
    /// Get Arm client for crawling
    /// </summary>
    /// <returns></returns>
    public ArmClient GetCrawlerArmClient();

    /// <summary>
    /// Get Arm client for arm operations with configurable retry policy
    /// </summary>
    /// <param name="retryOptions">Retry configuration options</param>
    /// <returns></returns>
    public Task<ArmClient> GetArmOperationClientWithRetry(ArmRetryOptions retryOptions);

    /// <summary>
    /// Get Arm client for crawling with configurable retry policy
    /// </summary>
    /// <param name="retryOptions">Retry configuration options</param>
    /// <returns></returns>
    public ArmClient GetCrawlerArmClientWithRetry(ArmRetryOptions retryOptions);

}

