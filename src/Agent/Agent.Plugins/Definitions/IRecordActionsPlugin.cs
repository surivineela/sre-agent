using System;
using System.Threading.Tasks;
using Agent.Core.Models.Api.v1;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Interface for plugins that record important agent actions
    /// </summary>
    public interface IRecordActionsPlugin
    {
        /// <summary>
        /// Records an important action taken by the agent
        /// </summary>
        /// <param name="threadId">The thread ID this action is associated with</param>
        /// <param name="title">Title describing the action being recorded</param>
        /// <param name="status">Current status of the action</param>
        /// <returns>The recorded action</returns>
        Task<Action> RecordAction(Guid threadId, string title, ActionStatus status = ActionStatus.Pending);

        /// <summary>
        /// Gets details about a specific action
        /// </summary>
        /// <param name="threadId">The thread ID this action is associated with</param>
        /// <param name="actionId">The ID of the action to retrieve</param>
        /// <returns>The action details</returns>
        Task<Action> GetAction(Guid threadId, Guid actionId);
    }
}
