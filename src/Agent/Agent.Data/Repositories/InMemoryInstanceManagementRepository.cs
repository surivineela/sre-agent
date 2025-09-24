// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

public class InMemoryInstanceManagementRepository(
    InstanceManagementSettings settings
) : IInstanceManagementRepository
{
    private LeaderLease? _leaderLease;
    private readonly object _leaderLeaseLock = new();
    private readonly ConcurrentDictionary<string, AgentContextInstanceAssignment> _assignments = new();
    private readonly ConcurrentDictionary<string, WorkerInstance> _workerInstances = new();

    public Task<LeaderLease?> GetLeaderLeaseAsync()
    {
        return Task.FromResult(_leaderLease);
    }

    public Task<(LeaderLease lease, bool isAcquired)> TryAcquireLeaderLeaseAsync(string instanceId)
    {
        lock (_leaderLeaseLock)
        {
            if (_leaderLease == null
                || _leaderLease.LeaseExpiration < DateTimeOffset.UtcNow
                || string.IsNullOrEmpty(_leaderLease.LeaseHolder))
            {
                var lease = new LeaderLease(instanceId, DateTimeOffset.UtcNow.AddSeconds(settings.LeaderLeaseTTLSeconds));
                _leaderLease = lease;
                return Task.FromResult((lease, true));
            }

            if (_leaderLease?.LeaseHolder == instanceId)
            {
                return Task.FromResult((_leaderLease, true));
            }

            return Task.FromResult((_leaderLease!, false));
        }
    }

    public Task<bool> ReleaseLeaderLeaseAsync(string instanceId)
    {
        lock (_leaderLeaseLock)
        {
            if (_leaderLease?.LeaseHolder == instanceId)
            {
                _leaderLease = null;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public Task<(LeaderLease lease, bool isRenewed)> RenewLeaderLeaseAsync(string instanceId)
    {
        lock (_leaderLeaseLock)
        {
            if (_leaderLease?.LeaseHolder == instanceId)
            {
                var lease = new LeaderLease(instanceId, DateTimeOffset.UtcNow.AddSeconds(settings.LeaderLeaseTTLSeconds));
                _leaderLease = lease;
                return Task.FromResult((lease, true));
            }

            return Task.FromResult((_leaderLease!, false));
        }
    }

    public Task<bool> RegisterWorkerInstanceAsync(WorkerInstance instance)
    {
        if (_workerInstances.TryAdd(instance.Id, instance))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<WorkerInstance?> GetWorkerInstanceAsync(string instanceId)
    {
        if (_workerInstances.TryGetValue(instanceId, out var instance))
        {
            return Task.FromResult((WorkerInstance?)instance);
        }

        return Task.FromResult<WorkerInstance?>(null);
    }

    public Task<List<WorkerInstance>> GetAllReadyWorkerInstancesAsync()
    {
        var workerHearbeatThreshold = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(10));

        var instances = _workerInstances.Values
            .Where(i => i.HealthState == WorkerInstanceHealthState.Ready && i.LastHeartbeat > workerHearbeatThreshold)
            .OrderBy(i => i.CurrentAgentCount)
            .ToList();

        return Task.FromResult(instances);
    }

    public Task<bool> UpdateWorkerInstanceAsync(WorkerInstance instance)
    {
        if (_workerInstances.TryGetValue(instance.Id, out var existingInstance))
        {
            var result = _workerInstances.TryUpdate(instance.Id, instance, existingInstance);
            return Task.FromResult(result);
        }

        return Task.FromResult(false);
    }

    public Task<bool> UnregisterWorkerInstanceAsync(string instanceId)
    {
        if (_workerInstances.TryRemove(instanceId, out _))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task CreateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment)
    {
        if (_assignments.TryAdd(assignment.AgentContextId, assignment))
        {
            return Task.CompletedTask;
        }

        return Task.FromException(new InvalidOperationException("Assignment already exists."));
    }

    public Task UpdateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment)
    {
        if (_assignments.TryGetValue(assignment.AgentContextId, out var existingAssignment))
        {
            var result = _assignments.TryUpdate(assignment.AgentContextId, assignment, existingAssignment);
            return Task.FromResult(result);
        }

        return Task.FromException(new InvalidOperationException("Assignment not found."));
    }

    public Task DeleteAgentContextInstanceAssignmentAsync(string agentContextId, string instanceId)
    {
        if (_assignments.TryRemove(agentContextId, out var assignment) && assignment.InstanceId == instanceId)
        {
            return Task.CompletedTask;
        }

        return Task.FromException(new InvalidOperationException("Assignment not found."));
    }

    public Task<List<AgentContextInstanceAssignment>> GetAssignmentsForInstanceAsync(string instanceId)
    {
        var assignments = _assignments.Values
            .Where(a => a.InstanceId == instanceId)
            .ToList();

        return Task.FromResult(assignments);
    }

    public Task<bool> DeleteAllAssignmentsForThreadAsync(Guid threadId)
    {
        // For in-memory implementation, we can't easily filter by ThreadId since assignments don't store ThreadId
        // This is a stub implementation for testing
        return Task.FromResult(true);
    }
}
