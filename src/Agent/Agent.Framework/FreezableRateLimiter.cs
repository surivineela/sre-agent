// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Agent.Framework;

/// <summary>
/// Options for configuring a <see cref="FreezableRateLimiter"/>.
/// </summary>
public class FreezableRateLimiterOptions
{
    /// <summary>
    /// Gets or sets the maximum number of tokens available in the bucket.
    /// </summary>
    public int TokenLimit { get; set; }

    /// <summary>
    /// Gets or sets the period for token replenishment. Defaults to 1 second.
    /// </summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the number of tokens to add per replenishment period. Defaults to 1.
    /// </summary>
    public int TokensPerPeriod { get; set; } = 1;
}

/// <summary>
/// A rate limiter that uses a token bucket approach with freeze capability.
/// </summary>
/// <remarks>
/// This rate limiter can enter a frozen state where tokens are acquired but immediately
/// released and requests fail. After the freeze period, normal operation resumes.
/// All requests fast-fail when tokens are not available.
/// </remarks>
public sealed class FreezableRateLimiter : IDisposable
{
    private readonly TokenBucketRateLimiter _underlyingLimiter;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _freezeEndTime = DateTimeOffset.MinValue;
    private bool _disposed;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FreezableRateLimiter"/> class.
    /// </summary>
    /// <param name="options">Options to configure the rate limiter.</param>
    public FreezableRateLimiter(FreezableRateLimiterOptions options)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));

        _underlyingLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.TokenLimit,
            ReplenishmentPeriod = options.ReplenishmentPeriod,
            TokensPerPeriod = options.TokensPerPeriod,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    /// <summary>
    /// Gets a value indicating whether the rate limiter is currently frozen.
    /// </summary>
    public bool IsFrozen => DateTimeOffset.UtcNow < _freezeEndTime;

    /// <summary>
    /// Gets the time when the freeze period will end, or null if not frozen.
    /// </summary>
    public DateTimeOffset? FreezeEndTime => IsFrozen ? _freezeEndTime : null;

    /// <summary>
    /// Freezes the rate limiter for the specified duration.
    /// </summary>
    /// <param name="duration">The duration to freeze the rate limiter.</param>
    /// <remarks>
    /// Multiple calls to Freeze will extend the freeze period if the new end time is later than the current one.
    /// </remarks>
    public void Freeze(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative.");
        }

        _lock.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var newEndTime = DateTimeOffset.UtcNow + duration;
            if (newEndTime > _freezeEndTime)
            {
                _freezeEndTime = newEndTime;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Attempts to acquire the specified number of permits asynchronously.
    /// </summary>
    /// <param name="permitCount">The number of permits to acquire.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// An <see cref="AcquiredResult"/> indicating whether the permits were acquired and the retry-after duration if frozen.
    /// </returns>
    /// <remarks>
    /// When not frozen, behaves exactly like the underlying TokenBucketRateLimiter.
    /// When frozen, still attempts to acquire from the underlying limiter but always returns failure with retry-after information.
    /// </remarks>
    public async ValueTask<AcquiredResult> AcquireAsync(int permitCount, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var now = DateTimeOffset.UtcNow;
            var isFrozen = now < _freezeEndTime;

            // Always attempt to acquire from underlying limiter
            var lease = await _underlyingLimiter.AcquireAsync(permitCount, cancellationToken).ConfigureAwait(false);

            if (isFrozen)
            {
                // Dispose the lease since we're rejecting the request
                lease.Dispose();

                // Calculate retry-after duration
                var retryAfter = _freezeEndTime - now;
                return new AcquiredResult(false, retryAfter > TimeSpan.Zero ? retryAfter : null);
            }

            // Not frozen - return the actual result
            var isAcquired = lease.IsAcquired;
            TimeSpan? retryAfterValue = null;

            if (!isAcquired && lease.TryGetMetadata(MetadataName.RetryAfter.Name, out var retryAfterMetadata))
            {
                retryAfterValue = retryAfterMetadata as TimeSpan?;

                // Add random jitter (0-5 seconds) to retry-after time
                if (retryAfterValue.HasValue)
                {
                    var jitterSeconds = _random.NextDouble() * 5.0; // Random value between 0 and 5 seconds
                    retryAfterValue = retryAfterValue.Value + TimeSpan.FromSeconds(jitterSeconds);
                }
            }

            // Dispose if not acquired (acquired leases should be disposed by caller)
            if (!isAcquired)
            {
                lease.Dispose();
            }

            return new AcquiredResult(isAcquired, retryAfterValue);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="FreezableRateLimiter"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _underlyingLimiter.Dispose();
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
    }
}

/// <summary>
/// Represents the result of an acquire operation from a <see cref="FreezableRateLimiter"/>.
/// </summary>
public readonly struct AcquiredResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AcquiredResult"/> struct.
    /// </summary>
    /// <param name="isAcquired">Whether the permits were successfully acquired.</param>
    /// <param name="retryAfter">The duration to wait before retrying, if applicable.</param>
    public AcquiredResult(bool isAcquired, TimeSpan? retryAfter)
    {
        IsAcquired = isAcquired;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets a value indicating whether the permits were successfully acquired.
    /// </summary>
    public bool IsAcquired { get; }

    /// <summary>
    /// Gets the duration to wait before retrying, if the permits were not acquired.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
