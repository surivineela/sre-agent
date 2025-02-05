// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;

namespace Agent.Core.Helpers;

public sealed class AsyncOperationTracker<TDescriptor, TParameter, TProgressDetail>
    where TDescriptor : notnull
{
    private readonly Func<Kernel, TDescriptor, TParameter, Action<TProgressDetail>, CancellationToken, Task<string>> _func;
    private readonly Func<string, bool>? _funcShouldSendTeamsNotification;
    private readonly Dictionary<TDescriptor, AsyncOperationStatus<TDescriptor, TParameter, TProgressDetail>> _status = new();

    public AsyncOperationTracker(
        Func<Kernel, TDescriptor, TParameter, Action<TProgressDetail>, CancellationToken, Task<string>> func,
        Func<string, bool>? funcShouldSendTeamsNotification = null)
    {
        _func = func;
        _funcShouldSendTeamsNotification = funcShouldSendTeamsNotification;
    }

    public Task? GetTask(
        TDescriptor descriptor)
    {
        return _status.GetValueOrDefault(descriptor)?.Task;
    }

    public AsyncOperationStartResult<TDescriptor, TProgressDetail> TryStartOperation(
        Kernel kernel,
        string contextMessage,
        TDescriptor descriptor,
        TParameter parameter)
    {
        lock (_status)
        {
            if (!_status.TryGetValue(descriptor, out var status)
                || status.Task.IsCompleted)
            {
                var newStatus = _status[descriptor] = new AsyncOperationStatus<TDescriptor, TParameter, TProgressDetail>(
                    kernel,
                    contextMessage,
                    descriptor,
                    parameter,
                    func: _func,
                    funcShouldSendTeamsNotification: _funcShouldSendTeamsNotification ?? AlwaysTrue);
                return new AsyncOperationStartResult<TDescriptor, TProgressDetail>(
                    Created: true,
                    Summary: newStatus.Summarize());
            }
            else
            {
                return new AsyncOperationStartResult<TDescriptor, TProgressDetail>(
                    Created: false,
                    Summary: status.Summarize());
            }
        }
    }

    private static bool AlwaysTrue(string _)
    {
        return true;
    }

    public AsyncOperationStatusSummary<TDescriptor, TProgressDetail>? GetOperationSummary(
        TDescriptor descriptor)
    {
        lock (_status)
        {
            if (_status.TryGetValue(descriptor, out var status))
            {
                return status.Summarize();
            }
            return null;
        }
    }

    public AsyncOperationStatusSummary<TDescriptor, TProgressDetail>? CancelOperation(
        TDescriptor descriptor)
    {
        lock (_status)
        {
            if (_status.TryGetValue(descriptor, out var status))
            {
                status.Cancel();
                return status.Summarize();
            }
            return null;
        }
    }
}
