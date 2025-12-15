// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Xunit;

namespace Agent.Tests.Unit;

public class FreezableRateLimiterTests
{
    [Fact]
    public void Constructor_ValidArguments_Succeeds()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        Assert.False(limiter.IsFrozen);
        Assert.Null(limiter.FreezeEndTime);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FreezableRateLimiter(null!));
    }

    [Fact]
    public async Task AcquireAsync_NotFrozen_Succeeds()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        var result = await limiter.AcquireAsync(1);

        Assert.True(result.IsAcquired);
        Assert.Null(result.RetryAfter);
    }

    [Fact]
    public async Task AcquireAsync_NotFrozen_ExceedsCapacity_Fails()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 5,
            TokensPerPeriod = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        // Acquire all tokens
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.AcquireAsync(1);
            Assert.True(result.IsAcquired);
        }

        // Next request should fail (no tokens left)
        var failedResult = await limiter.AcquireAsync(1);
        Assert.False(failedResult.IsAcquired);
        Assert.NotNull(failedResult.RetryAfter);
    }

    [Fact]
    public void Freeze_ValidDuration_SetsFreezed()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(5));

        Assert.True(limiter.IsFrozen);
        Assert.NotNull(limiter.FreezeEndTime);
        Assert.True(limiter.FreezeEndTime.Value > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Freeze_NegativeDuration_Throws()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.Freeze(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Freeze_ZeroDuration_Succeeds()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.Zero);

        // Should not throw
        Assert.False(limiter.IsFrozen);
    }

    [Fact]
    public async Task AcquireAsync_WhileFrozen_AlwaysReturnsFalse()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(10));

        // Try to acquire - should fail even though tokens are available
        var result = await limiter.AcquireAsync(1);

        Assert.False(result.IsAcquired);
        Assert.NotNull(result.RetryAfter);
        Assert.True(result.RetryAfter.Value.TotalSeconds > 0);
    }

    [Fact]
    public async Task AcquireAsync_WhileFrozen_ReturnsRetryAfter()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        var freezeDuration = TimeSpan.FromSeconds(5);
        limiter.Freeze(freezeDuration);

        var result = await limiter.AcquireAsync(1);

        Assert.False(result.IsAcquired);
        Assert.NotNull(result.RetryAfter);
        // RetryAfter should be approximately equal to freeze duration (within 1 second tolerance)
        Assert.True(Math.Abs((result.RetryAfter.Value - freezeDuration).TotalSeconds) < 1);
    }

    [Fact]
    public async Task AcquireAsync_AfterFreezeExpires_Succeeds()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromMilliseconds(200));

        // Verify it's frozen
        Assert.True(limiter.IsFrozen);

        // Wait for freeze to expire
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.False(limiter.IsFrozen);
        Assert.Null(limiter.FreezeEndTime);

        // Should be able to acquire now
        var result = await limiter.AcquireAsync(1);
        Assert.True(result.IsAcquired);
    }

    [Fact]
    public void Freeze_MultipleCalls_ExtendsFreezePeriod()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(5));
        var firstEndTime = limiter.FreezeEndTime;

        // Freeze again with longer duration
        limiter.Freeze(TimeSpan.FromSeconds(10));
        var secondEndTime = limiter.FreezeEndTime;

        Assert.NotNull(firstEndTime);
        Assert.NotNull(secondEndTime);
        Assert.True(secondEndTime.Value > firstEndTime.Value);
    }

    [Fact]
    public void Freeze_MultipleCalls_DoesNotShortenFreezePeriod()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(10));
        var firstEndTime = limiter.FreezeEndTime;

        // Freeze again with shorter duration (should not change end time)
        limiter.Freeze(TimeSpan.FromSeconds(5));
        var secondEndTime = limiter.FreezeEndTime;

        Assert.NotNull(firstEndTime);
        Assert.NotNull(secondEndTime);
        // End time should remain approximately the same (allowing for small time drift)
        Assert.True(Math.Abs((secondEndTime.Value - firstEndTime.Value).TotalMilliseconds) < 100);
    }

    [Fact]
    public async Task AcquireAsync_WhileFrozen_ConsumesUnderlyingTokens()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 5,
            TokensPerPeriod = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10) // Long replenishment period
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromMilliseconds(200));

        // Try to acquire multiple times while frozen
        // This should consume tokens from the underlying limiter
        for (int i = 0; i < 5; i++)
        {
            var result = await limiter.AcquireAsync(1);
            Assert.False(result.IsAcquired); // Always fails while frozen
        }

        // Wait for freeze to expire
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.False(limiter.IsFrozen);

        // Try to acquire - should fail because underlying tokens were consumed
        var finalResult = await limiter.AcquireAsync(1);
        Assert.False(finalResult.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_WithCancellation_RespectsCancellationToken()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await limiter.AcquireAsync(1, cts.Token));
    }

    [Fact]
    public async Task ConcurrentAcquire_WhileFrozen_ThreadSafe()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(10));

        var tasks = new List<Task<AcquiredResult>>();

        // Try to acquire from multiple threads concurrently
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(limiter.AcquireAsync(1).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        // All should fail (frozen)
        Assert.All(results, result =>
        {
            Assert.False(result.IsAcquired);
            Assert.NotNull(result.RetryAfter);
        });
    }

    [Fact]
    public async Task ConcurrentFreeze_ThreadSafe()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        var tasks = new List<Task>();

        // Freeze from multiple threads
        for (int i = 0; i < 10; i++)
        {
            var duration = TimeSpan.FromSeconds(5 + i); // Different durations
            tasks.Add(Task.Run(() => limiter.Freeze(duration)));
        }

        await Task.WhenAll(tasks);

        // Should be frozen with the longest duration
        Assert.True(limiter.IsFrozen);
        Assert.NotNull(limiter.FreezeEndTime);
    }

    [Fact]
    public void Dispose_CompletesSynchronously()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        var limiter = new FreezableRateLimiter(options);

        limiter.Dispose();

        // Should complete without exception
        Assert.True(true);
    }

    [Fact]
    public async Task Dispose_PreventsSubsequentOperations()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        var limiter = new FreezableRateLimiter(options);
        limiter.Dispose();

        // Operations after disposal should throw
        Assert.Throws<ObjectDisposedException>(() => limiter.Freeze(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await limiter.AcquireAsync(1));
    }

    [Fact]
    public void Dispose_MultipleTimesIsIdempotent()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        var limiter = new FreezableRateLimiter(options);

        limiter.Dispose();
        limiter.Dispose(); // Should not throw

        Assert.True(true);
    }

    [Fact]
    public async Task AcquireAsync_MultiplePermits_WorksCorrectly()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        // Acquire multiple permits at once
        var result = await limiter.AcquireAsync(5);

        Assert.True(result.IsAcquired);
        Assert.Null(result.RetryAfter);
    }

    [Fact]
    public async Task AcquireAsync_MultiplePermits_WhileFrozen_Fails()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        limiter.Freeze(TimeSpan.FromSeconds(5));

        // Try to acquire multiple permits while frozen
        var result = await limiter.AcquireAsync(5);

        Assert.False(result.IsAcquired);
        Assert.NotNull(result.RetryAfter);
    }

    [Fact]
    public async Task FreezeEndTime_ReturnsNullWhenNotFrozen()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        Assert.Null(limiter.FreezeEndTime);

        limiter.Freeze(TimeSpan.FromMilliseconds(100));
        Assert.NotNull(limiter.FreezeEndTime);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.Null(limiter.FreezeEndTime);
    }

    [Fact]
    public async Task Integration_FreezeAndUnfreeze_Workflow()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        // Normal operation
        var result1 = await limiter.AcquireAsync(1);
        Assert.True(result1.IsAcquired);

        // Freeze
        limiter.Freeze(TimeSpan.FromMilliseconds(200));
        var result2 = await limiter.AcquireAsync(1);
        Assert.False(result2.IsAcquired);

        // Wait for unfreeze
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        var result3 = await limiter.AcquireAsync(1);
        Assert.True(result3.IsAcquired);
    }

    [Fact]
    public async Task AcquiredResult_StructBehavior()
    {
        var options = new FreezableRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        };

        using var limiter = new FreezableRateLimiter(options);

        var result = await limiter.AcquireAsync(1);

        // Test struct properties
        Assert.True(result.IsAcquired);
        Assert.Null(result.RetryAfter);

        // Create a result manually
        var manualResult = new AcquiredResult(false, TimeSpan.FromSeconds(5));
        Assert.False(manualResult.IsAcquired);
        Assert.Equal(TimeSpan.FromSeconds(5), manualResult.RetryAfter);
    }
}
