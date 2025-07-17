// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;

namespace Agent.Core;

public sealed class AsyncReaderWriterLock
{
    private const int WriteLockState = unchecked((int)0x80000000);

    private ConcurrentQueue<TaskCompletionSource<ReaderAcquisitionResult>>? _readerQueue;
    private ConcurrentQueue<TaskCompletionSource<WriterAcquisitionResult>>? _writerQueue;

    private int _lockState;

    public WriterAcquisitionResult TryAcquireWriter(out bool isAcquired)
    {
        if (TrySetWriteLockState())
        {
            isAcquired = true;
            return new WriterAcquisitionResult(this);
        }
        else
        {
            isAcquired = false;
            return new WriterAcquisitionResult();
        }
    }

    public ValueTask<WriterAcquisitionResult> AcquireWriterAsync(CancellationToken cancellationToken = default)
    {
        if (TrySetWriteLockState())
        {
            // Fast path for uncontended write lock
            return ValueTask.FromResult(new WriterAcquisitionResult(this));
        }
        else
        {
            var task = EnqueueTask(ref _writerQueue, cancellationToken);
            ProcessQueues();
            return new(task);
        }
    }

    public ValueTask<ReaderAcquisitionResult> AcquireReaderAsync(CancellationToken cancellationToken = default)
    {
        if (TrySetReadLockState())
        {
            // Fast path for uncontended read lock
            return ValueTask.FromResult(new ReaderAcquisitionResult(this));
        }
        else
        {
            var task = EnqueueTask(ref _readerQueue, cancellationToken);
            ProcessQueues();
            return new(task);
        }
    }

    private void ProcessQueues()
    {
        bool shouldRecheckAssumptions;
        do
        {
            shouldRecheckAssumptions = false;

            var hasWriteLock = false;
            while (_lockState == 0
                && _writerQueue != null
                && _writerQueue.TryDequeue(out var writer))
            {
                if (writer.Task.IsCompleted)
                {
                    continue;
                }

                // If we found a write lock request, check
                // whether the write lock can be acquired.
                hasWriteLock = hasWriteLock || TrySetWriteLockState();
                if (!hasWriteLock)
                {
                    // If we failed to acquire the write lock, requeue
                    // the request and retry
                    _writerQueue.Enqueue(writer);
                }
                else if (writer.TrySetResult(new(this)))
                {
                    // Check whether the write lock request can still be fulfilled.
                    // Consume the write lock if the request was fulfilled.
                    return;
                }
            }

            if (hasWriteLock)
            {
                shouldRecheckAssumptions = true;

                // At this point if we still have the write lock,
                // then we must have failed to find a write lock
                // request that can still be fulfilled.
                // Release the write lock in this case.
                ClearWriteLockState();
            }

            var hasReadLock = false;
            while ((_lockState & WriteLockState) == 0
                && _readerQueue != null
                && _readerQueue.TryDequeue(out var reader))
            {
                if (reader.Task.IsCompleted)
                {
                    continue;
                }

                // If we found a read lock request, check
                // whether a read lock can be acquired.
                hasReadLock = hasReadLock || TrySetReadLockState();
                if (!hasReadLock)
                {
                    // If we failed to acquire a read lock, then
                    // the write lock is held. Requeue the request
                    // and retry
                    _readerQueue.Enqueue(reader);
                }
                else if (reader.TrySetResult(new(this)))
                {
                    // Check whether the read lock request can still be fulfilled.
                    // Consume the read lock if the request was fulfilled.
                    hasReadLock = false;
                }
            }

            if (hasReadLock)
            {
                shouldRecheckAssumptions = true;

                // At this point if we still have the read lock,
                // then we must have failed to find a read lock
                // request that can still be fulfilled.
                // Release the read lock in this case.
                Interlocked.Decrement(ref _lockState);
            }
        }
        while (shouldRecheckAssumptions);
    }

    private bool TrySetReadLockState()
    {
        var newLockState = Interlocked.Increment(ref _lockState);
        if ((newLockState & WriteLockState) == 0)
        {
            // A read lock is successfully acquired if the
            // WriteLockState remains cleared after the increment
            // operation.
            return true;
        }
        else
        {
            // Undo increment
            Interlocked.Decrement(ref _lockState);
            return false;
        }
    }

    private bool TrySetWriteLockState()
    {
        if (_lockState == 0)
        {
            // The write lock is acquired if _lockState
            // transitions from 0 to WriteLockState,
            // which implies that the read lock count is zero.
            return Interlocked.CompareExchange(
                ref _lockState,
                WriteLockState,
                0) == 0;
        }
        else
        {
            return false;
        }
    }

    private void ClearWriteLockState()
    {
        Interlocked.And(ref _lockState, ~WriteLockState);
    }

    private void Release(bool isWriter)
    {
        if (isWriter)
        {
            // While we are still under the write lock, check whether
            // we can process another write lock request
            while (_writerQueue != null
                && _writerQueue.TryDequeue(out var writer)
                && writer.TrySetResult(new(this)))
            {
                return;
            }

            // Clear write lock flag when we have no more requests
            ClearWriteLockState();
        }
        else
        {
            Interlocked.Decrement(ref _lockState);
        }

        ProcessQueues();
    }

    private static Task<T> EnqueueTask<T>(
        ref ConcurrentQueue<TaskCompletionSource<T>>? queue,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<T>();

        if (cancellationToken != default)
        {
            // Register the cancellation token if needed.
            var registration = cancellationToken.Register(
                () => tcs.TrySetCanceled(cancellationToken));

            tcs.Task.ContinueWith(
                _ => registration.Dispose(),
                default,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        if (!tcs.Task.IsCompleted)
        {
            LazyAssignNew(ref queue).Enqueue(tcs);
        }

        return tcs.Task;
    }

    private static T LazyAssignNew<T>(ref T? target)
        where T : class, new()
    {
        if (target is not null)
        {
            return target;
        }

        var newValue = new T();

        var existing = Interlocked.CompareExchange(
            location1: ref target,
            value: newValue,
            comparand: null);

        return existing ?? newValue;
    }

    public readonly struct ReaderAcquisitionResult : IDisposable
    {
        private readonly AsyncReaderWriterLock? _owner;

        public ReaderAcquisitionResult(AsyncReaderWriterLock owner)
        {
            _owner = owner;
        }

        public bool IsAcquired => _owner is not null;

        public void Dispose()
        {
            _owner?.Release(isWriter: false);
        }
    }

    public readonly struct WriterAcquisitionResult : IDisposable
    {
        private readonly AsyncReaderWriterLock? _owner;

        public WriterAcquisitionResult(AsyncReaderWriterLock owner)
        {
            _owner = owner;
        }

        public bool IsAcquired => _owner is not null;

        void IDisposable.Dispose()
        {
            _owner?.Release(isWriter: true);
        }
    }
}
