// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;

namespace Agent.Core.Helpers;

public static class ChatClientHelper
{
    /// <summary>
    /// Executes a chat client operation with retry logic for handling transient failures.
    /// </summary>
    /// <typeparam name="T">The return type of the chat operation</typeparam>
    /// <param name="chatOperation">The asynchronous chat operation to execute</param>
    /// <param name="logger">Optional logger for recording retry attempts</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelaySeconds">Initial delay in seconds between retries (default: 1)</param>
    /// <returns>The result of the chat operation</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> chatOperation,
        ILogger logger = null,
        int maxRetries = 3,
        double initialDelaySeconds = 1)
    {
        int retryCount = 0;
        TimeSpan delay = TimeSpan.FromSeconds(initialDelaySeconds);

        while (true)
        {
            try
            {
                return await chatOperation();
            }
            catch (System.ClientModel.ClientResultException ex) when (ex.Message.Contains("HTTP 503"))
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    logger?.LogError("Failed to get chat response after {RetryCount} retries due to service unavailability (HTTP 503)", maxRetries);
                    throw;
                }

                logger?.LogWarning("Received 503 error from chat client. Retry {Current}/{Max} after {Delay}s delay", retryCount, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2)); // Exponential backoff capped at 30s
            }
        }
    }
}

