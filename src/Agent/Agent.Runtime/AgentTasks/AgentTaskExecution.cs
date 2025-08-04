// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.AgentTasks;

/// <summary>
/// Wrapper for a function that runs an agent task and can be cancelled.
/// Takes in the function to run and will not run it until the RunAsync method is called.
/// </summary>
public sealed class AgentTaskExecution(
    Func<CancellationToken, Task> execution,
    CancellationToken serviceStopToken
)
{
    private readonly CancellationTokenSource _executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(serviceStopToken);

    private bool _isRunning = false;
    private bool _isCancelled = false;

    public Task RunAsync()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Agent task execution is already running");
        }

        _isRunning = true;
        return execution(_executionCancellationTokenSource.Token);
    }

    public void Cancel()
    {
        if (_isRunning && !_isCancelled)
        {
            _executionCancellationTokenSource.Cancel();
            _isRunning = false;
            _isCancelled = true;
        }
    }
}
