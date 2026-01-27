// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>Exception thrown when a rate limit is exceeded (HTTP 429).</summary>
public sealed class RateLimitExceededException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RateLimitExceededException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="retryAfterSeconds">The retry-after duration in seconds.</param>
    public RateLimitExceededException(string message, Exception? innerException = null, int? retryAfterSeconds = null)
        : base(message, innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>Gets the retry-after duration in seconds, if available.</summary>
    public int? RetryAfterSeconds { get; }
}



/// <summary>A delegating chat client that implements rate limiting with freeze capability.</summary>
/// <remarks>
/// The provided implementation of <see cref="IChatClient"/> is thread-safe for concurrent use and uses
/// <see cref="FreezableRateLimiter"/> to control request throughput.
/// Tokens are refilled automatically based on the configured replenishment period.
/// When a 429 (rate limit) error is encountered, the limiter freezes for the retry-after duration.
/// </remarks>
public sealed class RateLimitChatClient : DelegatingChatClient
{
    private const string RateLimitExceededMessage = "Rate limit capacity exceeded. Retry after {0} seconds.";
    private readonly FreezableRateLimiter _rateLimiter;
    private readonly Random _random = new();

    /// <summary>Initializes a new instance of the <see cref="RateLimitChatClient"/> class.</summary>
    /// <param name="innerClient">The underlying <see cref="IChatClient"/>.</param>
    /// <param name="maxTokens">The maximum number of concurrent requests allowed. Default is 20.</param>
    public RateLimitChatClient(IChatClient innerClient, int maxTokens = 20)
        : base(innerClient)
    {
        _rateLimiter = new FreezableRateLimiter(new FreezableRateLimiterOptions
        {
            TokenLimit = maxTokens,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = 1
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rateLimiter?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await _rateLimiter.AcquireAsync(1, cancellationToken);

        if (!result.IsAcquired)
        {
            var retryAfterSeconds = _random.Next(5, 16); // Random between 5 and 15 seconds
            if (result.RetryAfter.HasValue)
            {
                retryAfterSeconds = (int)Math.Max(1, result.RetryAfter.Value.TotalSeconds);
            }

            throw new RateLimitExceededException(
                string.Format(RateLimitExceededMessage, retryAfterSeconds),
                null,
                retryAfterSeconds);
        }

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            return response;
        }
        catch (ClientResultException ex) when (IsRateLimit(ex))
        {
            var retryAfter = TryGetRetryAfterDelay(ex);
            _rateLimiter.Freeze(retryAfter);

            var retryAfterSecs = (int)retryAfter.TotalSeconds;
            throw new RateLimitExceededException(
                string.Format(RateLimitExceededMessage, retryAfterSecs),
                ex,
                retryAfterSecs);
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await _rateLimiter.AcquireAsync(1, cancellationToken);

        if (!result.IsAcquired)
        {
            var retryAfterSeconds = _random.Next(1, 6); // Random between 1 and 5 seconds
            if (result.RetryAfter.HasValue)
            {
                retryAfterSeconds = (int)Math.Max(1, result.RetryAfter.Value.TotalSeconds);
            }

            throw new RateLimitExceededException(
                string.Format(RateLimitExceededMessage, retryAfterSeconds),
                null,
                retryAfterSeconds);
        }

        IAsyncEnumerator<ChatResponseUpdate> e;
        try
        {
            e = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
        catch (ClientResultException ex) when (IsRateLimit(ex))
        {
            var retryAfter = TryGetRetryAfterDelay(ex);
            _rateLimiter.Freeze(retryAfter);

            var retryAfterSecs = (int)retryAfter.TotalSeconds;
            throw new RateLimitExceededException(
                string.Format(RateLimitExceededMessage, retryAfterSecs),
                ex,
                retryAfterSecs);
        }

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await e.MoveNextAsync())
                    {
                        break;
                    }

                    update = e.Current;
                }
                catch (ClientResultException ex) when (IsRateLimit(ex))
                {
                    var retryAfter = TryGetRetryAfterDelay(ex);
                    _rateLimiter.Freeze(retryAfter);

                    var retryAfterSecs = (int)retryAfter.TotalSeconds;
                    throw new RateLimitExceededException(
                        string.Format(RateLimitExceededMessage, retryAfterSecs),
                        ex,
                        retryAfterSecs);
                }

                yield return update;
            }
        }
        finally
        {
            await e.DisposeAsync();
        }
    }

    private static TimeSpan TryGetRetryAfterDelay(ClientResultException exception)
    {
        // First try to parse retry-after from exception message
        if (TryParseRetryAfterSeconds(exception, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(20);
    }

    public static bool IsRateLimit(System.ClientModel.ClientResultException ex)
    {
        try
        {
            if (ex.Status == 429)
            {
                return true;
            }
        }
        catch
        {
            // Fallback to message checks if Status is not available
        }

        var msg = ex.Message ?? string.Empty;
        return msg.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("too_many_requests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRetryAfterSeconds(System.ClientModel.ClientResultException ex, out int seconds)
    {
        seconds = 0;
        var msg = ex.Message ?? string.Empty;
        var m = Regex.Match(msg, @"Try again in\s+(\d+)\s+seconds", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var s))
        {
            seconds = s;
            return true;
        }
        return false;
    }
}
