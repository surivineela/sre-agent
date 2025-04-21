// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

public interface IInstanceManagementRepository
{
    #region Leader Lease Management
    /// <summary>
    /// Gets the current leader lease
    /// </summary>
    /// <returns>The current leader lease</returns>
    Task<LeaderLease?> GetLeaderLeaseAsync();

    /// <summary>
    /// Acquires a leader lease
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>The acquired leader lease and a boolean indicating if the lease was acquired</returns>
    Task<(LeaderLease lease, bool isAcquired)> TryAcquireLeaderLeaseAsync(string instanceId);

    /// <summary>
    /// Releases a leader lease
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>True if the leader lease was released, false otherwise</returns>
    Task<bool> ReleaseLeaderLeaseAsync(string instanceId);

    /// <summary>
    /// Renews a leader lease
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>The renewed leader lease and a boolean indicating if the lease was renewed</returns>
    Task<(LeaderLease lease, bool isRenewed)> RenewLeaderLeaseAsync(string instanceId);
    #endregion

    #region Instance Lifetime
    /// <summary>
    /// Tries to register a worker instance
    /// </summary>
    /// <param name="instance">The worker instance</param>
    /// <returns>True if the worker instance was registered, false otherwise</returns>
    Task<bool> RegisterWorkerInstanceAsync(WorkerInstance instance);

    /// <summary>
    /// Gets a worker instance
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>The worker instance</returns>
    Task<WorkerInstance?> GetWorkerInstanceAsync(string instanceId);

    /// <summary>
    /// Gets all ready worker instances
    /// </summary>
    /// <returns></returns>
    Task<List<WorkerInstance>> GetAllReadyWorkerInstancesAsync();

    /// <summary>
    /// Updates a worker instance
    /// </summary>
    /// <param name="instance">The worker instance</param>
    /// <returns>True if the worker instance was updated, false otherwise</returns>
    Task<bool> UpdateWorkerInstanceAsync(WorkerInstance instance);

    /// <summary>
    /// Unregisters a worker instance
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <returns>True if the worker instance was unregistered, false otherwise</returns>
    Task<bool> UnregisterWorkerInstanceAsync(string instanceId);
    #endregion

    #region Instance Assignments
    /// <summary>
    /// Creates a new agent context instance assignment
    /// </summary>
    /// <param name="assignment">The assignment info</param>
    Task CreateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment);

    /// <summary>
    /// Updates an agent context instance assignment
    /// </summary>
    /// <param name="assignment">The assignment info</param>
    Task UpdateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment);

    /// <summary>
    /// Deletes an existing agent context instance assignment
    /// </summary>
    /// <param name="agentContextId">Agent context ID</param>
    /// <param name="instanceId">Instance ID</param>
    Task DeleteAgentContextInstanceAssignmentAsync(string agentContextId, string instanceId);

    /// <summary>
    /// Gets all assignments for a given instance
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <returns>List of assignment records</returns>
    Task<List<AgentContextInstanceAssignment>> GetAssignmentsForInstanceAsync(string instanceId);
    #endregion
}
