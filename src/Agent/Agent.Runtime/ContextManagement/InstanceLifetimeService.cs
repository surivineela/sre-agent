// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.ContextManagement;

public sealed class InstanceLifetimeService(
    IInstanceManagementRepository instanceManagementRepository,
    ILogger<InstanceLifetimeService> logger,
    InstanceManagementSettings instanceManagementSettings,
    AgentContextDispatchService agentDispatchService,
    AgentContextProcessingService agentContextProcessingService
) : IHostedService, IDisposable
{
    private string InstanceId { get; set; } = $"instance-{Guid.NewGuid()}"; // TODO: maybe get from environment variable? or is generating unique id enough?

    private Timer? _instanceLifetimeLoopTimer = null;
    private bool _isInstanceLifetimeLoopRunning = false;

    private Timer? _leaderLeaseTimer = null;
    private bool _isLeaderLeaseTimerRunning = false;

    private Timer? _instanceAssignmentWatchTimer = null;
    private bool _isInstanceAssignmentWatchTimerRunning = false;

    private bool _isLeader = false;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Starting instance lifetime service for instance {InstanceId}", InstanceId);

        bool registerResult = await instanceManagementRepository.RegisterWorkerInstanceAsync(new WorkerInstance
        {
            Id = InstanceId,
            LastHeartbeat = DateTimeOffset.UtcNow,
            CurrentAgentCount = 0,
            HealthState = WorkerInstanceHealthState.Initializing
        });

        if (!registerResult)
        {
            logger.LogInternalError("Failed to register worker instance {InstanceId}", InstanceId);

            throw new Exception("Failed to register worker instance");
        }

        logger.LogInternalInformation("Successfully registered worker instance {InstanceId}", InstanceId);

        _instanceLifetimeLoopTimer = new Timer(
            async _ => await InstanceLifetimeLoopAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(instanceManagementSettings.InstanceHeartbeatIntervalSeconds));

        _leaderLeaseTimer = new Timer(
            async _ => await LeaderLeaseLoopAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(instanceManagementSettings.LeaderLeaseTimerIntervalSeconds));

        _instanceAssignmentWatchTimer = new Timer(
            async _ => await InstanceAssignmentWatchLoopAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(instanceManagementSettings.LeaderLeaseTimerIntervalSeconds));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Stopping instance lifetime service for instance {InstanceId}", InstanceId);

        _instanceLifetimeLoopTimer?.Change(Timeout.Infinite, 0);
        _leaderLeaseTimer?.Change(Timeout.Infinite, 0);
        _instanceAssignmentWatchTimer?.Change(Timeout.Infinite, 0);

        await agentDispatchService.StopAsync();

        if (_isLeader)
        {
            var successFullyReleased = await instanceManagementRepository.ReleaseLeaderLeaseAsync(InstanceId);

            if (!successFullyReleased)
            {
                logger.LogInternalWarning("Failed to release leader lease on instance {InstanceId}", InstanceId);
            }
            else
            {
                logger.LogInternalInformation("Successfully released leader lease on instance {InstanceId}", InstanceId);
            }
        }

        bool unregisterResult = await instanceManagementRepository.UnregisterWorkerInstanceAsync(InstanceId);

        if (!unregisterResult)
        {
            logger.LogInternalError("Failed to unregister worker instance {InstanceId}", InstanceId);

            throw new Exception("Failed to unregister worker instance");
        }

        logger.LogInternalInformation("Successfully unregistered worker instance {InstanceId}", InstanceId);
    }

    /// <summary>
    /// Updates the worker instance state in the data store
    /// </summary>
    private async Task InstanceLifetimeLoopAsync()
    {
        // Only proceed if _isInstanceLifetimeLoopRunning was false and we successfully set it to true
        if (Interlocked.CompareExchange(ref _isInstanceLifetimeLoopRunning, true, false) == false)
        {
            try
            {
                // Update heartbeat timestamp
                await instanceManagementRepository.UpdateWorkerInstanceAsync(new WorkerInstance
                {
                    Id = InstanceId,
                    LastHeartbeat = DateTimeOffset.UtcNow,
                    CurrentAgentCount = agentContextProcessingService.RunningCount,
                    HealthState = WorkerInstanceHealthState.Ready
                });

                logger.LogDebug("Updated heartbeat for worker instance {InstanceId}", InstanceId);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error executing instance lifetime loop for instance {InstanceId}", InstanceId);
            }
            finally
            {
                // Reset flag to allow next execution
                _isInstanceLifetimeLoopRunning = false;
            }
        }
    }

    /// <summary>
    /// Handles attempting to acquire the leader lease, or renewing the lease if already acquired
    /// </summary>
    private async Task LeaderLeaseLoopAsync()
    {
        // Only proceed if _isLeaderLeaseTimerRunning was false and we successfully set it to true
        if (Interlocked.CompareExchange(ref _isLeaderLeaseTimerRunning, true, false) == false)
        {
            try
            {
                if (_isLeader)
                {
                    // we are the leader, renew the lease
                    var (lease, isRenewed) = await instanceManagementRepository.RenewLeaderLeaseAsync(InstanceId);

                    if (isRenewed && lease.LeaseHolder == InstanceId)
                    {
                        logger.LogDebug("Renewed leader lease on instance {InstanceId}", InstanceId);
                    }
                    else
                    {
                        logger.LogInternalWarning("Failed to renew leader lease on instance {InstanceId}", InstanceId);
                        _isLeader = false;
                    }
                }
                else
                {
                    var (lease, isAcquired) = await instanceManagementRepository.TryAcquireLeaderLeaseAsync(InstanceId);

                    if (isAcquired && lease.LeaseHolder == InstanceId)
                    {
                        _isLeader = true;

                        logger.LogInternalInformation("Acquired leader lease on instance {InstanceId}", InstanceId);
                    }
                }

                if (_isLeader)
                {
                    await agentDispatchService.StartAsync(InstanceId); // no-op if already running
                }
                else
                {
                    await agentDispatchService.StopAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error executing leader lease loop for instance {InstanceId}", InstanceId);
            }
            finally
            {
                // Reset flag to allow next execution
                _isLeaderLeaseTimerRunning = false;
            }
        }
    }

    /// <summary>
    /// Gets all agent context assignments for this instance and passes them to the processing service
    /// </summary>
    private async Task InstanceAssignmentWatchLoopAsync()
    {
        // Only proceed if flag was set to false and we successfully set it to true
        if (Interlocked.CompareExchange(ref _isInstanceAssignmentWatchTimerRunning, true, false) == false)
        {
            if (!instanceManagementSettings.ProcessingEnabled)
            {
                return;
            }

            try
            {
                // list assignments for this instance
                List<AgentContextInstanceAssignment> instanceAssignments = await instanceManagementRepository.GetAssignmentsForInstanceAsync(InstanceId);

                await agentContextProcessingService.HandleUpdateAsync(instanceAssignments);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error updating processing service on instance {InstanceId}", InstanceId);
            }
            finally
            {
                // Reset flag to allow next execution
                _isInstanceAssignmentWatchTimerRunning = false;
            }
        }
    }

    public void Dispose()
    {
        _instanceLifetimeLoopTimer?.Dispose();
        _instanceLifetimeLoopTimer = null;

        _leaderLeaseTimer?.Dispose();
        _leaderLeaseTimer = null;

        _instanceAssignmentWatchTimer?.Dispose();
        _instanceAssignmentWatchTimer = null;
    }
}
